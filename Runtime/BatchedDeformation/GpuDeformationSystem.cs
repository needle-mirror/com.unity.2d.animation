using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.U2D.Animation.Profiler;
using UnityEngine.U2D.Common;

namespace UnityEngine.U2D.Animation
{
    internal class GpuDeformationSystem : BaseDeformationSystem
    {
        const string k_GpuSkinningShaderKeyword = SkinnedSpriteKeyword.keyword;
        const string k_GlobalSpriteBoneBufferId = "_SpriteBoneTransforms";

        readonly HashSet<EntityId> m_AcquiredKeywordMaterials = new HashSet<EntityId>();
        static readonly HashSet<EntityId> s_MaterialsInUse = new HashSet<EntityId>();

        NativeArray<int> m_BoneTransformIndices;
        ComputeBuffer m_BoneTransformsComputeBuffer;
        static ComputeBuffer s_FallbackBuffer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateFallbackBuffer()
        {
            if (s_FallbackBuffer == null)
                s_FallbackBuffer = new ComputeBuffer(UnsafeUtility.SizeOf<float4x4>(), UnsafeUtility.SizeOf<float4x4>(), ComputeBufferType.Default);

            Shader.SetGlobalBuffer(k_GlobalSpriteBoneBufferId, s_FallbackBuffer);
        }

        static void ClearFallbackBuffer()
        {
            if (s_FallbackBuffer != null)
                s_FallbackBuffer.Release();

            s_FallbackBuffer = null;
        }

        public override DeformationMethods deformationMethod => DeformationMethods.Gpu;

        internal static bool DoesShaderSupportGpuDeformation(Material material)
        {
            if (material == null)
                return false;
            Shader shader = material.shader;
            if (shader == null)
                return false;

            Rendering.LocalKeyword[] supportedKeywords = shader.keywordSpace.keywords;
            for (int i = 0; i < supportedKeywords.Length; ++i)
            {
                if (supportedKeywords[i].name == k_GpuSkinningShaderKeyword)
                    return true;
            }

            return false;
        }

        static bool IsComputeBufferValid(ComputeBuffer buffer) => buffer != null && buffer.IsValid();

        protected override void InitializeArrays()
        {
            base.InitializeArrays();

            m_BoneTransformIndices = new NativeArray<int>(0, Allocator.Persistent);

            CreateFallbackBuffer();
        }

        internal override void Cleanup()
        {
            base.Cleanup();

            m_BoneTransformIndices.DisposeIfCreated();

            CleanupComputeResources();

            ClearFallbackBuffer();
        }

        protected override void ResizeAndCopyArrays(int updatedCount)
        {
            base.ResizeAndCopyArrays(updatedCount);

            // No need to copy or initialize values as they are completely overwritten each frame by the job
            NativeArrayHelpers.ResizeIfNeeded(ref m_BoneTransformIndices, updatedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            if (updatedCount == 0)
                CleanupComputeResources();
        }

        void CleanupComputeResources()
        {
            if (IsComputeBufferValid(m_BoneTransformsComputeBuffer))
                m_BoneTransformsComputeBuffer.Release();
            m_BoneTransformsComputeBuffer = null;

            SkinnedSpriteKeyword.ReleaseAll(m_AcquiredKeywordMaterials);
            Shader.SetGlobalBuffer(k_GlobalSpriteBoneBufferId, s_FallbackBuffer);
        }

        // Own the SKINNED_SPRITE keyword of exactly the materials the current Sprite Skins use:
        // acquire the ones that appeared and release the ones no longer used, so ownership
        // converges on every batch change instead of being held until the whole system empties.
        protected override void OnMembershipChanged()
        {
            s_MaterialsInUse.Clear();
            foreach (SpriteSkin spriteSkin in m_SpriteSkins)
            {
                Material material = spriteSkin.spriteRenderer != null ? spriteSkin.spriteRenderer.sharedMaterial : null;
                if (material != null && s_MaterialsInUse.Add(material.GetEntityId()))
                    SkinnedSpriteKeyword.Acquire(material, m_AcquiredKeywordMaterials);
            }

            SkinnedSpriteKeyword.ReleaseUnused(m_AcquiredKeywordMaterials, s_MaterialsInUse);
        }

        internal override void Update()
        {
            int previousCount = m_SpriteSkins.Count;

            BatchRemoveSpriteSkins();
            BatchAddSpriteSkins();

            int count = m_SpriteSkins.Count;

            if (count == 0)
            {
                if (previousCount > 0)
                {
                    m_LocalToWorldTransformAccessJob.ResetCache();
                    m_WorldToLocalTransformAccessJob.ResetCache();
                }
                return;
            }

            Assert.AreEqual(m_IsSpriteSkinActiveForDeform.Length, count);
            Assert.AreEqual(m_PerSkinJobData.Length, count);
            Assert.AreEqual(m_SpriteSkinData.Length, count);
            Assert.AreEqual(m_BoundsData.Length, count);
            Assert.AreEqual(m_SpriteRenderers.Length, count);
            Assert.AreEqual(m_Buffers.Length, count);
            Assert.AreEqual(m_BufferSizes.Length, count);

            Assert.AreEqual(m_BoneTransformIndices.Length, count);

            PrepareDataForDeformation(out JobHandle localToWorldJobHandle, out JobHandle worldToLocalJobHandle);

            if (!GotVerticesToDeform(out int vertexBufferSize))
            {
                localToWorldJobHandle.Complete();
                worldToLocalJobHandle.Complete();
#if ENABLE_PROFILER && PROFILING_INSTALLED
                Animation2DProfilerMarkers.s_SpriteSkinGPUVertexProcessed.Value = 0;
#endif
                return;
            }

            PerSkinJobData skinBatch = m_SkinBatchArray[0];
            ResizeBuffers(vertexBufferSize, in skinBatch);

            int batchCount = m_SpriteSkinData.Length;
            JobHandle jobHandle = SchedulePrepareJob(batchCount);

            Profiling.scheduleJobs.Begin();
            jobHandle = JobHandle.CombineDependencies(localToWorldJobHandle, worldToLocalJobHandle, jobHandle);
            jobHandle = ScheduleBoneJobBatched(jobHandle, skinBatch);
            m_DeformJobHandle = ScheduleSkinDeformBatchedJobGpu(jobHandle, skinBatch, batchCount, Time.frameCount);
            jobHandle = ScheduleCalculateBoneTransformIndicesJob(jobHandle);
            Profiling.scheduleJobs.End();

            JobHandle.ScheduleBatchedJobs();
            jobHandle = JobHandle.CombineDependencies(jobHandle, m_DeformJobHandle);
            jobHandle.Complete();

#if ENABLE_PROFILER && PROFILING_INSTALLED
            if (UnityEngine.Profiling.Profiler.enabled)
            {
                for (int i = 0; i < m_SpriteSkinData.Length; ++i)
                {
                    if (m_HasBoneTransformsChanged[i])
                    {
                        Animation2DProfilerMarkers.s_SpriteSkinGPUProcessed.Value++;
                        if(!(new GpuDeformationMode().ShouldSkipVertexDeformation(m_SpriteSkinData[i], m_IsOutlineDataRequired[i])))
                            Animation2DProfilerMarkers.s_SpriteSkinGPUVertexProcessed.Value += m_SpriteSkinData[i].spriteVertexCount;
                    }
                }
            }
#endif



            using (Profiling.setBatchBoneTransformIndexAndLocalAABB.Auto())
            {
                InternalEngineBridge.SetBatchBoneTransformIndexAndLocalAABBArray(m_SpriteRenderers, m_BoneTransformIndices, m_BoundsData);
            }

            SetComputeBuffer();

            // NOTE: In GPU deformation system, SpriteSkin.PostDeform() does nothing (no cache or state update needed).
        }

        protected override void ResizeBuffers(int vertexBufferSize, in PerSkinJobData skinBatch)
        {
            base.ResizeBuffers(vertexBufferSize, in skinBatch);

            int noOfBones = skinBatch.bindPosesIndex.y;

            if (!IsComputeBufferValid(m_BoneTransformsComputeBuffer) || m_BoneTransformsComputeBuffer.count < noOfBones)
                CreateComputeBuffer(noOfBones);
        }

        void CreateComputeBuffer(int bufferSize)
        {
            if (IsComputeBufferValid(m_BoneTransformsComputeBuffer))
                m_BoneTransformsComputeBuffer.Release();

            m_BoneTransformsComputeBuffer = new ComputeBuffer(bufferSize, UnsafeUtility.SizeOf<float4x4>(), ComputeBufferType.Default);
            SetComputeBuffer();
        }

        void SetComputeBuffer()
        {
            m_BoneTransformsComputeBuffer.SetData(m_FinalBoneTransforms, 0, 0, m_FinalBoneTransforms.Length);
            Shader.SetGlobalBuffer(k_GlobalSpriteBoneBufferId, m_BoneTransformsComputeBuffer);
        }

        protected JobHandle ScheduleCalculateBoneTransformIndicesJob(JobHandle jobHandle)
        {
            CalculateBoneTransformIndicesJob calculateBoneTransformIndicesJob = new CalculateBoneTransformIndicesJob()
            {
                isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                spriteSkinData = m_SpriteSkinData,
                boneTransformIndices = m_BoneTransformIndices,
            };
            return calculateBoneTransformIndicesJob.Schedule(jobHandle);
        }
    }
}
