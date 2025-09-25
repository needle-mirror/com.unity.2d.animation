using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.U2D.Common;

namespace UnityEngine.U2D.Animation
{
    internal abstract class BaseDeformationSystem
    {
        protected static class Profiling
        {
            public static readonly ProfilerMarker transformAccessJob = new ProfilerMarker("BaseDeformationSystem.TransformAccessJob");
            public static readonly ProfilerMarker boneTransformsChangeDetection = new ProfilerMarker("BaseDeformationSystem.BoneTransformsChangeDetection");
            public static readonly ProfilerMarker getSpriteSkinBatchData = new ProfilerMarker("BaseDeformationSystem.GetSpriteSkinBatchData");
            public static readonly ProfilerMarker scheduleJobs = new ProfilerMarker("BaseDeformationSystem.ScheduleJobs");
            public static readonly ProfilerMarker setBatchDeformableBufferAndLocalAABB = new ProfilerMarker("BaseDeformationSystem.SetBatchDeformableBufferAndLocalAABB");
            public static readonly ProfilerMarker setBoneTransformsArray = new ProfilerMarker("BaseDeformationSystem.SetBoneTransformsArray");
        }

        public abstract DeformationMethods deformationMethod { get; }

        protected int m_ObjectId;

        protected readonly HashSet<SpriteSkin> m_SpriteSkins = new HashSet<SpriteSkin>();

#if UNITY_INCLUDE_TESTS
        internal HashSet<SpriteSkin> SpriteSkins => m_SpriteSkins;
#endif

        protected SpriteRenderer[] m_SpriteRenderers = new SpriteRenderer[0];

        // This is a queue of sprite skins which will be added into m_SpriteSkins
        // at the correct time in batch processing BatchAddSpriteSkins
        readonly HashSet<SpriteSkin> m_SpriteSkinsToAdd = new HashSet<SpriteSkin>();

        // This is a queue of sprite skins which will be removed from m_SpriteSkins
        // at the correct time in batch processing BatchRemoveSpriteSkins
        readonly HashSet<SpriteSkin> m_SpriteSkinsToRemove = new HashSet<SpriteSkin>();

        readonly List<int> m_TransformIdsToRemove = new List<int>();

        protected NativeByteArray m_DeformedVerticesBuffer;
        protected NativeByteArray m_PreviousDeformedVerticesBuffer;
        protected NativeArray<float4x4> m_FinalBoneTransforms;

        protected NativeArray<bool> m_IsSpriteSkinActiveForDeform;
        protected NativeArray<SpriteSkinData> m_SpriteSkinData;
        protected NativeArray<PerSkinJobData> m_PerSkinJobData;
        protected NativeArray<Bounds> m_BoundsData;
        protected NativeArray<IntPtr> m_Buffers;
        protected NativeArray<int> m_BufferSizes;
        protected NativeArray<IntPtr> m_BoneTransformBuffers;

        protected NativeArray<int2> m_BoneLookupData;
        protected NativeArray<PerSkinJobData> m_SkinBatchArray;

        // Indicates whether bone transforms have changed for each SpriteSkin in the current frame.
        // Set to true if any bone transform changes require the deformation job to run.
        protected NativeArray<bool> m_HasBoneTransformsChanged;

        // The last frame when deformation occurred for each SpriteSkin.
        // Used to determine if cached deformation data is still valid or needs to be updated.
        protected NativeArray<int> m_LastDeformedFrame;

        protected TransformAccessJob m_LocalToWorldTransformAccessJob;
        protected TransformAccessJob m_WorldToLocalTransformAccessJob;

        protected JobHandle m_DeformJobHandle;

        internal void RemoveBoneTransforms(SpriteSkin spriteSkin)
        {
            // if the sprite skin is not in the list, we don't need to remove it
            if (!m_SpriteSkins.Contains(spriteSkin))
                return;

            m_LocalToWorldTransformAccessJob.RemoveTransformById(spriteSkin.rootBoneTransformId);
            NativeArray<int> boneTransforms = spriteSkin.boneTransformId;
            if (boneTransforms == default || !boneTransforms.IsCreated)
                return;

            for (int i = 0; i < boneTransforms.Length; ++i)
                m_LocalToWorldTransformAccessJob.RemoveTransformById(boneTransforms[i]);
        }

        internal void AddBoneTransforms(SpriteSkin spriteSkin)
        {
            // if we are not handling this spriteskin, we don't need to add it to the job.
            if (!m_SpriteSkins.Contains(spriteSkin))
                return;

            m_LocalToWorldTransformAccessJob.AddTransform(spriteSkin.rootBone);
            if (spriteSkin.boneTransforms != null)
            {
                foreach (Transform t in spriteSkin.boneTransforms)
                {
                    if (t != null)
                        m_LocalToWorldTransformAccessJob.AddTransform(t);
                }
            }
        }

        internal virtual void UpdateMaterial(SpriteSkin spriteSkin) { }

        // This is called when the SpriteSkin is created or enabled
        internal virtual bool AddSpriteSkin(SpriteSkin spriteSkin)
        {
            // if we do not have the sprite skin and it is already in the m_SpriteSkinsToAdd list, we don't need to add it again
            if (!m_SpriteSkins.Contains(spriteSkin) && m_SpriteSkinsToAdd.Add(spriteSkin))
            {
                return true;
            }
            // if the skin is scheduled to be removed, cancel that.
            if (!m_SpriteSkinsToRemove.Contains(spriteSkin)) return false;
            m_SpriteSkinsToAdd.Add(spriteSkin);
            return true;

        }

        internal void CopyToSpriteSkinData(SpriteSkin spriteSkin)
        {
            if (!m_SpriteSkinData.IsCreated)
                throw new InvalidOperationException("Sprite Skin Data not initialized.");

            int dataIndex = spriteSkin.dataIndex;
            if (dataIndex < 0 || dataIndex >= m_SpriteSkinData.Length)
                return;

            SpriteSkinData spriteSkinData = default(SpriteSkinData);
            spriteSkin.CopyToSpriteSkinData(ref spriteSkinData);

            m_SpriteSkinData[dataIndex] = spriteSkinData;
            m_SpriteRenderers[dataIndex] = spriteSkin.spriteRenderer;
        }

        /// This is called when the SpriteSkin is destroyed or disabled
        internal void RemoveSpriteSkin(SpriteSkin spriteSkin)
        {
            if (spriteSkin == null)
                return;

            // if we are currently handling the spritekin and we have not yet removed it, mark it as being removed
            // by adding it to the m_SpriteSkinsToRemove list
            if (m_SpriteSkins.Contains(spriteSkin) && m_SpriteSkinsToRemove.Add(spriteSkin))
            {
                // records the transform id to remove
                m_TransformIdsToRemove.Add(spriteSkin.transform.GetInstanceID());
            }
            // if is scheduled for removal, also remove it from the m_SpriteSkinsToAdd list
            m_SpriteSkinsToAdd.Remove(spriteSkin);

            // remove bone transforms from the transform access job
            RemoveBoneTransforms(spriteSkin);
        }

        internal HashSet<SpriteSkin> GetSpriteSkins()
        {
            return m_SpriteSkins;
        }

        internal void Initialize(int objectId)
        {
            m_ObjectId = objectId;

            // These two jobs have the same type, but can be configured to return localToWorld or worldToLocal matrices
            if (m_LocalToWorldTransformAccessJob == null)
                m_LocalToWorldTransformAccessJob = new TransformAccessJob();
            if (m_WorldToLocalTransformAccessJob == null)
                m_WorldToLocalTransformAccessJob = new TransformAccessJob();

            InitializeArrays();
            BatchRemoveSpriteSkins();
            BatchAddSpriteSkins();

            // Initialise all existing SpriteSkins as execution order is indeterminate
            int count = 0;
            foreach (SpriteSkin spriteSkin in m_SpriteSkins)
            {
                spriteSkin.SetDataIndex(count++);

                CopyToSpriteSkinData(spriteSkin);
            }
        }

        protected virtual void InitializeArrays()
        {
            const int startingCount = 0;

            m_FinalBoneTransforms = new NativeArray<float4x4>(startingCount, Allocator.Persistent);
            m_BoneLookupData = new NativeArray<int2>(startingCount, Allocator.Persistent);
            m_SkinBatchArray = new NativeArray<PerSkinJobData>(startingCount, Allocator.Persistent);

            m_IsSpriteSkinActiveForDeform = new NativeArray<bool>(startingCount, Allocator.Persistent);
            m_PerSkinJobData = new NativeArray<PerSkinJobData>(startingCount, Allocator.Persistent);
            m_SpriteSkinData = new NativeArray<SpriteSkinData>(startingCount, Allocator.Persistent);
            m_BoundsData = new NativeArray<Bounds>(startingCount, Allocator.Persistent);
            m_Buffers = new NativeArray<IntPtr>(startingCount, Allocator.Persistent);
            m_BufferSizes = new NativeArray<int>(startingCount, Allocator.Persistent);

            m_HasBoneTransformsChanged = new NativeArray<bool>(startingCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_LastDeformedFrame = new NativeArray<int>(startingCount, Allocator.Persistent);
        }

        protected void BatchRemoveSpriteSkins()
        {
            m_WorldToLocalTransformAccessJob.RemoveTransformsIfNull();

            int spritesToRemoveCount = m_SpriteSkinsToRemove.Count;
            if (spritesToRemoveCount == 0)
                return;

            m_WorldToLocalTransformAccessJob.RemoveTransformsByIds(m_TransformIdsToRemove);

            int updatedCount = Math.Max(m_SpriteSkins.Count - spritesToRemoveCount, 0);
            if (updatedCount == 0)
            {
                m_SpriteSkins.Clear();
            }
            else
            {
                foreach (SpriteSkin spriteSkin in m_SpriteSkinsToRemove)
                    m_SpriteSkins.Remove(spriteSkin);
            }

            int count = 0;
            foreach (SpriteSkin spriteSkin in m_SpriteSkins)
            {
                spriteSkin.SetDataIndex(count++);
                CopyToSpriteSkinData(spriteSkin);
            }

            Array.Resize(ref m_SpriteRenderers, updatedCount);
            ResizeAndCopyArrays(updatedCount);

            m_TransformIdsToRemove.Clear();
            m_SpriteSkinsToRemove.Clear();
        }

        protected void BatchAddSpriteSkins()
        {
            if (m_SpriteSkinsToAdd.Count == 0)
                return;

            if (!m_IsSpriteSkinActiveForDeform.IsCreated)
                throw new InvalidOperationException("SpriteSkinActiveForDeform not initialized.");

            int updatedCount = m_SpriteSkins.Count + m_SpriteSkinsToAdd.Count;
            Array.Resize(ref m_SpriteRenderers, updatedCount);
            ResizeAndCopyArrays(updatedCount);

            foreach (SpriteSkin spriteSkin in m_SpriteSkinsToAdd)
            {
                if (!m_SpriteSkins.Add(spriteSkin))
                {
                    Debug.LogError($"Skin already exists! Name={spriteSkin.name}");
                    continue;
                }

                UpdateMaterial(spriteSkin);
                int count = m_SpriteSkins.Count;

                m_SpriteRenderers[count - 1] = spriteSkin.spriteRenderer;
                m_WorldToLocalTransformAccessJob.AddTransform(spriteSkin.transform);

                AddBoneTransforms(spriteSkin);

                spriteSkin.SetDataIndex(count - 1);
                CopyToSpriteSkinData(spriteSkin);
            }

            m_SpriteSkinsToAdd.Clear();
        }

        protected virtual void ResizeAndCopyArrays(int updatedCount)
        {
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_IsSpriteSkinActiveForDeform, updatedCount);
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_PerSkinJobData, updatedCount);
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_Buffers, updatedCount);
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_BufferSizes, updatedCount);
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_SpriteSkinData, updatedCount);

            // Bounds may be reusable if index is unchanged.
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_BoundsData, updatedCount);

            // No need to copy or initialize values as they are completely overwritten each frame by the job
            NativeArrayHelpers.ResizeIfNeeded(ref m_HasBoneTransformsChanged, updatedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Must be initialized to 0, because 0 means "not deformed yet"
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_LastDeformedFrame, updatedCount);
        }

        protected virtual void ResizeBuffers(int vertexBufferSize, in PerSkinJobData skinBatch)
        {
            if (m_DeformedVerticesBuffer != null)
                m_PreviousDeformedVerticesBuffer = m_DeformedVerticesBuffer;
            else
                m_PreviousDeformedVerticesBuffer = BufferManager.instance.GetBuffer(m_ObjectId, vertexBufferSize);

            m_DeformedVerticesBuffer = BufferManager.instance.GetBuffer(m_ObjectId, vertexBufferSize);

            NativeArrayHelpers.ResizeIfNeeded(ref m_FinalBoneTransforms, skinBatch.bindPosesIndex.y);
            NativeArrayHelpers.ResizeIfNeeded(ref m_BoneLookupData, skinBatch.bindPosesIndex.y);
        }

        internal virtual void Cleanup()
        {
            m_DeformJobHandle.Complete();

            m_SpriteSkins.Clear();
            m_SpriteRenderers = new SpriteRenderer[0];
            BufferManager.instance.ReturnBuffer(m_ObjectId);
            m_IsSpriteSkinActiveForDeform.DisposeIfCreated();
            m_PerSkinJobData.DisposeIfCreated();
            m_Buffers.DisposeIfCreated();
            m_BufferSizes.DisposeIfCreated();
            m_SpriteSkinData.DisposeIfCreated();
            m_BoneLookupData.DisposeIfCreated();
            m_SkinBatchArray.DisposeIfCreated();
            m_FinalBoneTransforms.DisposeIfCreated();
            m_BoundsData.DisposeIfCreated();
            m_HasBoneTransformsChanged.DisposeIfCreated();
            m_LastDeformedFrame.DisposeIfCreated();

            m_LocalToWorldTransformAccessJob.Destroy();
            m_WorldToLocalTransformAccessJob.Destroy();
        }

        internal abstract void Update();

        protected void PrepareDataForDeformation(out JobHandle localToWorldJobHandle, out JobHandle worldToLocalJobHandle)
        {
            ValidateSpriteSkinData();

            using (Profiling.transformAccessJob.Auto())
            {
                localToWorldJobHandle = m_LocalToWorldTransformAccessJob.StartLocalToWorldAndChangeDetectionJob();
                worldToLocalJobHandle = m_WorldToLocalTransformAccessJob.StartWorldToLocalJob();
            }

            using (Profiling.boneTransformsChangeDetection.Auto())
            {
                BoneTransformsChangeDetectionJob boneTransformChangeDetectionJob = new BoneTransformsChangeDetectionJob
                {
                    transformChanged = m_LocalToWorldTransformAccessJob.transformChanged,
                    boneTransformIndex = m_LocalToWorldTransformAccessJob.transformData,
                    spriteSkinData = m_SpriteSkinData,
                    hasBoneTransformsChanged = m_HasBoneTransformsChanged
                };
                // Use 64 as the batch size to avoid false sharing
                boneTransformChangeDetectionJob.Schedule(m_SpriteSkinData.Length, 64, localToWorldJobHandle).Complete();
            }

            using (Profiling.getSpriteSkinBatchData.Auto())
            {
                NativeArrayHelpers.ResizeIfNeeded(ref m_SkinBatchArray, 1);
                FillPerSkinJobSingleThread fillPerSkinJobSingleThread = new FillPerSkinJobSingleThread()
                {
                    isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                    combinedSkinBatchArray = m_SkinBatchArray,
                    spriteSkinDataArray = m_SpriteSkinData,
                    perSkinJobDataArray = m_PerSkinJobData,
                };
                fillPerSkinJobSingleThread.Run();
            }
        }

        void ValidateSpriteSkinData()
        {
            foreach (SpriteSkin spriteSkin in m_SpriteSkins)
            {
                int index = spriteSkin.dataIndex;
                m_IsSpriteSkinActiveForDeform[index] = spriteSkin.BatchValidate();
                if (m_IsSpriteSkinActiveForDeform[index] && spriteSkin.NeedToUpdateDeformationCache())
                    CopyToSpriteSkinData(spriteSkin);
            }
        }

        protected bool GotVerticesToDeform(out int vertexBufferSize)
        {
            vertexBufferSize = m_SkinBatchArray[0].deformVerticesStartPos;
            return vertexBufferSize > 0;
        }

        protected JobHandle SchedulePrepareJob(int batchCount)
        {
            PrepareDeformJob prepareJob = new PrepareDeformJob
            {
                batchDataSize = batchCount,
                perSkinJobData = m_PerSkinJobData,
                boneLookupData = m_BoneLookupData
            };
            return prepareJob.Schedule();
        }

        protected JobHandle ScheduleBoneJobBatched(JobHandle jobHandle, PerSkinJobData skinBatch)
        {
            BoneDeformBatchedJob boneJobBatched = new BoneDeformBatchedJob()
            {
                boneTransform = m_LocalToWorldTransformAccessJob.transformMatrix,
                rootTransform = m_WorldToLocalTransformAccessJob.transformMatrix,
                spriteSkinData = m_SpriteSkinData,
                boneLookupData = m_BoneLookupData,
                finalBoneTransforms = m_FinalBoneTransforms,
                rootTransformIndex = m_WorldToLocalTransformAccessJob.transformData,
                boneTransformIndex = m_LocalToWorldTransformAccessJob.transformData
            };
            jobHandle = boneJobBatched.Schedule(skinBatch.bindPosesIndex.y, 8, jobHandle);
            return jobHandle;
        }

        protected JobHandle ScheduleSkinDeformBatchedJob(JobHandle jobHandle, PerSkinJobData skinBatch, int spriteCount, int frameCount)
        {
            SkinDeformBatchedJob skinJobBatched = new SkinDeformBatchedJob
            {
                spriteSkinData = m_SpriteSkinData,
                perSkinJobData = m_PerSkinJobData,
                finalBoneTransforms = m_FinalBoneTransforms,
                vertices = m_DeformedVerticesBuffer.array,
                previousVertices = m_PreviousDeformedVerticesBuffer.array,
                isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                hasBoneTransformsChanged = m_HasBoneTransformsChanged,
                bounds = m_BoundsData,
                lastDeformedFrame = m_LastDeformedFrame,
                frameCount = frameCount
            };
            return skinJobBatched.Schedule(spriteCount, 1, jobHandle);
        }

        protected unsafe JobHandle ScheduleCopySpriteRendererBuffersJob(JobHandle jobHandle, int batchCount)
        {
            CopySpriteRendererBuffersJob copySpriteRendererBuffersJob = new CopySpriteRendererBuffersJob()
            {
                isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                spriteSkinData = m_SpriteSkinData,
                ptrVertices = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(m_DeformedVerticesBuffer.array),
                buffers = m_Buffers,
                bufferSizes = m_BufferSizes,
            };
            return copySpriteRendererBuffersJob.Schedule(batchCount, 16, jobHandle);
        }

        protected void DeactivateDeformableBuffers()
        {
            for (int i = 0; i < m_IsSpriteSkinActiveForDeform.Length; ++i)
            {
                if (m_IsSpriteSkinActiveForDeform[i] || InternalEngineBridge.IsUsingDeformableBuffer(m_SpriteRenderers[i], IntPtr.Zero))
                    continue;
                m_SpriteRenderers[i].DeactivateDeformableBuffer();
            }
        }

        internal bool IsSpriteSkinActiveForDeformation(SpriteSkin spriteSkin)
        {
            return m_IsSpriteSkinActiveForDeform[spriteSkin.dataIndex];
        }

        internal int GetLastDeformedFrame(SpriteSkin spriteSkin)
        {
            return m_LastDeformedFrame[spriteSkin.dataIndex];
        }

        internal unsafe NativeArray<byte> GetDeformableBufferForSpriteSkin(SpriteSkin spriteSkin)
        {
            if (!m_SpriteSkins.Contains(spriteSkin))
                return default;

            if (!m_DeformJobHandle.IsCompleted)
                m_DeformJobHandle.Complete();

            SpriteSkinData skinData = m_SpriteSkinData[spriteSkin.dataIndex];
            if (skinData.deformVerticesStartPos < 0)
                return default;

            int vertexBufferLength = skinData.spriteVertexCount * skinData.spriteVertexStreamSize;
            byte* ptrVertices = (byte*)m_DeformedVerticesBuffer.array.GetUnsafeReadOnlyPtr();
            ptrVertices += skinData.deformVerticesStartPos;
            NativeArray<byte> buffer = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptrVertices, vertexBufferLength, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref buffer, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(m_DeformedVerticesBuffer.array));
#endif
            return buffer;
        }

#if UNITY_INCLUDE_TESTS
        internal TransformAccessJob GetWorldToLocalTransformAccessJob() => m_WorldToLocalTransformAccessJob;
        internal TransformAccessJob GetLocalToWorldTransformAccessJob() => m_LocalToWorldTransformAccessJob;
#endif
    }
}
