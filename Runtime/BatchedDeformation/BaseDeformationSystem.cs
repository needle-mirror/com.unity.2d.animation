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
            public static readonly ProfilerMarker getSpriteSkinBatchData = new ProfilerMarker("BaseDeformationSystem.GetSpriteSkinBatchData");
            public static readonly ProfilerMarker scheduleJobs = new ProfilerMarker("BaseDeformationSystem.ScheduleJobs");
            public static readonly ProfilerMarker setBatchDeformableBufferAndLocalAABB = new ProfilerMarker("BaseDeformationSystem.SetBatchDeformableBufferAndLocalAABB");
            public static readonly ProfilerMarker setBoneTransformsArray = new ProfilerMarker("BaseDeformationSystem.SetBoneTransformsArray");
        }

        protected int m_ObjectId;
        
        protected readonly List<SpriteSkin> m_SpriteSkins = new List<SpriteSkin>();
        protected SpriteRenderer[] m_SpriteRenderers = new SpriteRenderer[0];
        
        readonly List<SpriteSkin> m_SpriteSkinsToAdd = new List<SpriteSkin>();
        readonly List<SpriteSkin> m_SpriteSkinsToRemove = new List<SpriteSkin>();
        readonly List<int> m_TransformIdsToRemove = new List<int>();
        
        protected NativeByteArray m_DeformedVerticesBuffer;
        protected NativeArray<float4x4> m_FinalBoneTransforms;
        
        protected NativeArray<bool> m_IsSpriteSkinActiveForDeform;
        protected NativeArray<SpriteSkinData> m_SpriteSkinData;
        protected NativeArray<PerSkinJobData> m_PerSkinJobData;
        protected NativeArray<Bounds> m_BoundsData;
        protected NativeArray<IntPtr> m_Buffers;
        protected NativeArray<int> m_BufferSizes;
        protected NativeArray<IntPtr> m_BoneTransformBuffers;

        protected NativeArray<int2> m_BoneLookupData;
        protected NativeArray<int2> m_VertexLookupData;
        protected NativeArray<PerSkinJobData> m_SkinBatchArray;
        
        TransformAccessJob m_LocalToWorldTransformAccessJob;
        TransformAccessJob m_WorldToLocalTransformAccessJob;
        
        protected JobHandle m_DeformJobHandle;
        
        internal bool DoesSystemContainSpriteSkin(SpriteSkin skin) => DoesCollectionContainSpriteSkin(in m_SpriteSkins, skin) || DoesCollectionContainSpriteSkin(in m_SpriteSkinsToAdd, skin);

        internal void RemoveBoneTransforms(SpriteSkin spriteSkin)
        {
            if (!DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin))
                return;
            
            m_LocalToWorldTransformAccessJob.RemoveTransformById(spriteSkin.rootBoneTransformId);
            var boneTransforms = spriteSkin.boneTransformId;
            if (boneTransforms == default || !boneTransforms.IsCreated)
                return;
            
            for (var i = 0; i < boneTransforms.Length; ++i)
                m_LocalToWorldTransformAccessJob.RemoveTransformById(boneTransforms[i]);
        }

        internal void AddBoneTransforms(SpriteSkin spriteSkin)
        {
            if (!DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin))
                return;
            
            if (spriteSkin.boneTransforms != null)
            {
                foreach (var t in spriteSkin.boneTransforms)
                {
                    if(t != null)
                        m_LocalToWorldTransformAccessJob.AddTransform(t);
                }
            }
        }

        internal void AddRootBoneTransform(SpriteSkin spriteSkin)
        {
            if (spriteSkin.rootBone == null)
                return;
            if (!DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin))
                return;
            
            m_LocalToWorldTransformAccessJob.AddTransform(spriteSkin.rootBone);
        }

        internal virtual void UpdateMaterial(SpriteSkin spriteSkin)
        {
        }

        internal virtual void AddSpriteSkin(SpriteSkin spriteSkin)
        {
            if (!DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin) && !DoesCollectionContainSpriteSkin(in m_SpriteSkinsToAdd, spriteSkin))
                m_SpriteSkinsToAdd.Add(spriteSkin);
            else if (DoesCollectionContainSpriteSkin(in m_SpriteSkinsToRemove, spriteSkin))
                m_SpriteSkinsToAdd.Add(spriteSkin);
        }
        
        internal void CopyToSpriteSkinData(SpriteSkin spriteSkin)
        {
            var index = m_SpriteSkins.IndexOf(spriteSkin);
            if (index < 0)
                return;
            CopyToSpriteSkinData(index);
        }

        void CopyToSpriteSkinData(int index)
        {
            if (index < 0 || index >= m_SpriteSkins.Count || m_SpriteSkins[index] == null)
                return;
            if (!m_SpriteSkinData.IsCreated)
                return;
            
            var spriteSkinData = default(SpriteSkinData);
            var spriteSkin = m_SpriteSkins[index];
            spriteSkin.CopyToSpriteSkinData(ref spriteSkinData, index);
            m_SpriteSkinData[index] = spriteSkinData;
            m_SpriteRenderers[index] = spriteSkin.spriteRenderer;
        }
        
        internal void RemoveSpriteSkins(SpriteSkin[] spriteSkins)
        {
            for (var i = 0; i < spriteSkins.Length; ++i)
                RemoveSpriteSkin(spriteSkins[i]);
        }

        internal void RemoveSpriteSkin(SpriteSkin spriteSkin)
        {
            if (spriteSkin == null)
                return;

            if (DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin) && !DoesCollectionContainSpriteSkin(in m_SpriteSkinsToRemove, spriteSkin))
            {
                m_SpriteSkinsToRemove.Add(spriteSkin);
                m_TransformIdsToRemove.Add(spriteSkin.transform.GetInstanceID());
            }

            if (DoesCollectionContainSpriteSkin(in m_SpriteSkinsToAdd, spriteSkin))
                m_SpriteSkinsToAdd.Remove(spriteSkin);
            
            RemoveBoneTransforms(spriteSkin);
        }

        static bool DoesCollectionContainSpriteSkin(in List<SpriteSkin> collection, SpriteSkin spriteSkin)
        {
            var index = collection.IndexOf(spriteSkin);
            if (index < 0)
                return false;
            return collection[index] != null;
        } 
        
        internal SpriteSkin[] GetSpriteSkins()
        {
            return m_SpriteSkins.ToArray();
        }

        internal void Initialize(int objectId)
        {
            m_ObjectId = objectId;

            if (m_LocalToWorldTransformAccessJob == null)
                m_LocalToWorldTransformAccessJob = new TransformAccessJob();
            if (m_WorldToLocalTransformAccessJob == null)
                m_WorldToLocalTransformAccessJob = new TransformAccessJob();
            
            InitializeArrays();
            BatchRemoveSpriteSkins();
            BatchAddSpriteSkins();

            // Initialise all existing SpriteSkins as execution order is indeterminate
            for (var i = 0; i < m_SpriteSkins.Count; ++i)
                CopyToSpriteSkinData(i);
        }

        protected virtual void InitializeArrays()
        {
            const int startingCount = 0;
            
            m_FinalBoneTransforms = new NativeArray<float4x4>(startingCount, Allocator.Persistent);
            m_BoneLookupData = new NativeArray<int2>(startingCount, Allocator.Persistent);
            m_VertexLookupData = new NativeArray<int2>(startingCount, Allocator.Persistent);
            m_SkinBatchArray = new NativeArray<PerSkinJobData>(startingCount, Allocator.Persistent);
            
            m_IsSpriteSkinActiveForDeform = new NativeArray<bool>(startingCount, Allocator.Persistent);
            m_PerSkinJobData = new NativeArray<PerSkinJobData>(startingCount, Allocator.Persistent);
            m_SpriteSkinData = new NativeArray<SpriteSkinData>(startingCount, Allocator.Persistent);
            m_BoundsData = new NativeArray<Bounds>(startingCount, Allocator.Persistent);
            m_Buffers = new NativeArray<IntPtr>(startingCount, Allocator.Persistent);
            m_BufferSizes = new NativeArray<int>(startingCount, Allocator.Persistent);            
        }  
        
        protected void BatchRemoveSpriteSkins()
        {
            if (m_SpriteSkinsToRemove.Count == 0)
                return;
            
            m_WorldToLocalTransformAccessJob.RemoveTransformsByIds(m_TransformIdsToRemove);

            var updatedCount = Mathf.Max(m_SpriteSkins.Count - m_SpriteSkinsToRemove.Count, 0);
            if (updatedCount == 0)
            {
                m_SpriteSkins.Clear();
            }
            else
            {
                foreach (var spriteSkin in m_SpriteSkinsToRemove)
                {
                    var index = m_SpriteSkins.IndexOf(spriteSkin);
                    if (index < 0)
                        continue;

                    // Check if it is not the last SpriteSkin
                    if (index < m_SpriteSkins.Count - 1)
                    {
                        m_SpriteSkins.RemoveAtSwapBack(index);
                        CopyToSpriteSkinData(index);
                    }
                    else
                        m_SpriteSkins.RemoveAt(index);
                }
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
            
            var updatedCount = m_SpriteSkins.Count + m_SpriteSkinsToAdd.Count;
            Array.Resize(ref m_SpriteRenderers, updatedCount);
            if (m_IsSpriteSkinActiveForDeform.IsCreated)
                ResizeAndCopyArrays(updatedCount);

            foreach (var spriteSkin in m_SpriteSkinsToAdd)
            {
                if (DoesCollectionContainSpriteSkin(in m_SpriteSkins, spriteSkin))
                {
                    Debug.LogError($"Skin already exists! Name={spriteSkin.name}");
                    continue;
                }
                m_SpriteSkins.Add(spriteSkin);
                UpdateMaterial(spriteSkin);
                var count = m_SpriteSkins.Count;
                
                m_SpriteRenderers[count - 1] = spriteSkin.spriteRenderer;
                m_WorldToLocalTransformAccessJob.AddTransform(spriteSkin.transform);

                if (m_IsSpriteSkinActiveForDeform.IsCreated)
                {
                    AddRootBoneTransform(spriteSkin);
                    AddBoneTransforms(spriteSkin);
                    CopyToSpriteSkinData(count - 1);
                }
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
            NativeArrayHelpers.ResizeAndCopyIfNeeded(ref m_BoundsData, updatedCount);
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
            m_VertexLookupData.DisposeIfCreated();
            m_SkinBatchArray.DisposeIfCreated();
            m_FinalBoneTransforms.DisposeIfCreated();
            m_BoundsData.DisposeIfCreated();

            m_LocalToWorldTransformAccessJob.Destroy();
            m_WorldToLocalTransformAccessJob.Destroy();
        }

        internal abstract void Update();

        protected void PrepareDataForDeformation(out JobHandle localToWorldJobHandle, out JobHandle worldToLocalJobHandle)
        {
            ValidateSpriteSkinData();

            using (Profiling.transformAccessJob.Auto())
            {
                localToWorldJobHandle = m_LocalToWorldTransformAccessJob.StartLocalToWorldJob();
                worldToLocalJobHandle = m_WorldToLocalTransformAccessJob.StartWorldToLocalJob();
            }

            using (Profiling.getSpriteSkinBatchData.Auto())
            {
                NativeArrayHelpers.ResizeIfNeeded(ref m_SkinBatchArray, 1);
                var fillPerSkinJobSingleThread = new FillPerSkinJobSingleThread()
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
            for (var i = 0; i < m_SpriteSkins.Count; ++i)
            {
                var spriteSkin = m_SpriteSkins[i];
                m_IsSpriteSkinActiveForDeform[i] = spriteSkin.BatchValidate();
                if (m_IsSpriteSkinActiveForDeform[i] && spriteSkin.NeedToUpdateDeformationCache())
                {
                    CopyToSpriteSkinData(i);
                }
            }
        }
        
        protected bool GotVerticesToDeform(out int vertexBufferSize)
        {
            vertexBufferSize = m_SkinBatchArray[0].deformVerticesStartPos;
            return vertexBufferSize > 0;
        }
        
        protected JobHandle SchedulePrepareJob(int batchCount)
        {
            var prepareJob = new PrepareDeformJob
            {
                batchDataSize = batchCount,
                perSkinJobData = m_PerSkinJobData,
                boneLookupData = m_BoneLookupData,
                vertexLookupData = m_VertexLookupData
            };
            return prepareJob.Schedule();
        }

        protected JobHandle ScheduleBoneJobBatched(JobHandle jobHandle, PerSkinJobData skinBatch)
        {
            var boneJobBatched = new BoneDeformBatchedJob()
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

        protected JobHandle ScheduleSkinDeformBatchedJob(JobHandle jobHandle, PerSkinJobData skinBatch)
        {
            var skinJobBatched = new SkinDeformBatchedJob()
            {
                vertices = m_DeformedVerticesBuffer.array,
                vertexLookupData = m_VertexLookupData,
                spriteSkinData = m_SpriteSkinData,
                perSkinJobData = m_PerSkinJobData,
                finalBoneTransforms = m_FinalBoneTransforms,
            };
            return skinJobBatched.Schedule(skinBatch.verticesIndex.y, 16, jobHandle);            
        }

        protected unsafe JobHandle ScheduleCopySpriteRendererBuffersJob(JobHandle jobHandle, int batchCount)
        {
            var copySpriteRendererBuffersJob = new CopySpriteRendererBuffersJob()
            {
                isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                spriteSkinData = m_SpriteSkinData,
                ptrVertices = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(m_DeformedVerticesBuffer.array),
                buffers = m_Buffers,
                bufferSizes = m_BufferSizes,
            };
            return copySpriteRendererBuffersJob.Schedule(batchCount, 16, jobHandle);
        }
        
        protected JobHandle ScheduleCalculateSpriteSkinAABBJob(JobHandle jobHandle, int batchCount)
        {
            var updateBoundJob = new CalculateSpriteSkinAABBJob
            {
                vertices = m_DeformedVerticesBuffer.array,
                isSpriteSkinValidForDeformArray = m_IsSpriteSkinActiveForDeform,
                spriteSkinData = m_SpriteSkinData,
                bounds = m_BoundsData,
            };
            return updateBoundJob.Schedule(batchCount, 4, jobHandle);
        }           

        protected void DeactivateDeformableBuffers()
        {
            for (var i = 0; i < m_IsSpriteSkinActiveForDeform.Length; ++i)
            {
                if (m_IsSpriteSkinActiveForDeform[i] || InternalEngineBridge.IsUsingDeformableBuffer(m_SpriteRenderers[i], IntPtr.Zero))
                    continue;
                m_SpriteRenderers[i].DeactivateDeformableBuffer();
            }
        }
        
        internal bool IsSpriteSkinActiveForDeformation(SpriteSkin spriteSkin)
        {
            var index = m_SpriteSkins.IndexOf(spriteSkin);
            if (index < 0 )
                return false;
            return m_IsSpriteSkinActiveForDeform[index];
        }        
        
        internal unsafe NativeArray<byte> GetDeformableBufferForSpriteSkin(SpriteSkin spriteSkin)
        {
            var index = m_SpriteSkins.IndexOf(spriteSkin);
            if (index < 0)
                return default;
            
            if (!m_DeformJobHandle.IsCompleted)
                m_DeformJobHandle.Complete();

            var skinData = m_SpriteSkinData[index];
            if (skinData.deformVerticesStartPos < 0)
                return default;

            var vertexBufferLength = skinData.spriteVertexCount * skinData.spriteVertexStreamSize;
            var ptrVertices = (byte*)m_DeformedVerticesBuffer.array.GetUnsafeReadOnlyPtr();
            ptrVertices += skinData.deformVerticesStartPos; 
            var buffer = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptrVertices, vertexBufferLength, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref buffer, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(m_DeformedVerticesBuffer.array));
#endif
            return buffer;
        }   
        
        // ---- For tests
        internal TransformAccessJob GetWorldToLocalTransformAccessJob() => m_WorldToLocalTransformAccessJob;
        internal TransformAccessJob GetLocalToWorldTransformAccessJob() => m_LocalToWorldTransformAccessJob;
        // ---- End For tests        
    }
}