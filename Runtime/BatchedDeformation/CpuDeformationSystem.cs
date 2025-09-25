using Unity.Jobs;
using UnityEngine.Assertions;
using UnityEngine.U2D.Common;

namespace UnityEngine.U2D.Animation
{
    internal class CpuDeformationSystem : BaseDeformationSystem
    {
        const string k_GpuSkinningShaderKeyword = "SKINNED_SPRITE";
        JobHandle m_CopyJobHandle;

        public override DeformationMethods deformationMethod => DeformationMethods.Cpu;

        internal override void Cleanup()
        {
            base.Cleanup();

            m_CopyJobHandle.Complete();
        }

        internal override void UpdateMaterial(SpriteSkin spriteSkin)
        {
            Material sharedMaterial = spriteSkin.spriteRenderer.sharedMaterial;
            if (sharedMaterial.IsKeywordEnabled(k_GpuSkinningShaderKeyword))
                sharedMaterial.DisableKeyword(k_GpuSkinningShaderKeyword);
        }

        internal override void Update()
        {
            BatchRemoveSpriteSkins();
            BatchAddSpriteSkins();

            int count = m_SpriteSkins.Count;
            if (count == 0)
            {
                m_LocalToWorldTransformAccessJob.ResetCache();
                m_WorldToLocalTransformAccessJob.ResetCache();
                return;
            }

            Assert.AreEqual(m_IsSpriteSkinActiveForDeform.Length, count);
            Assert.AreEqual(m_PerSkinJobData.Length, count);
            Assert.AreEqual(m_SpriteSkinData.Length, count);
            Assert.AreEqual(m_BoundsData.Length, count);
            Assert.AreEqual(m_SpriteRenderers.Length, count);
            Assert.AreEqual(m_Buffers.Length, count);
            Assert.AreEqual(m_BufferSizes.Length, count);

            PrepareDataForDeformation(out JobHandle localToWorldJobHandle, out JobHandle worldToLocalJobHandle);

            if (!GotVerticesToDeform(out int vertexBufferSize))
            {
                localToWorldJobHandle.Complete();
                worldToLocalJobHandle.Complete();
                DeactivateDeformableBuffers();
                return;
            }

            int frameCount = Time.frameCount;

            PerSkinJobData skinBatch = m_SkinBatchArray[0];
            ResizeBuffers(vertexBufferSize, in skinBatch);

            int batchCount = m_SpriteSkinData.Length;
            JobHandle jobHandle = SchedulePrepareJob(batchCount);

            Profiling.scheduleJobs.Begin();
            jobHandle = JobHandle.CombineDependencies(localToWorldJobHandle, worldToLocalJobHandle, jobHandle);
            jobHandle = ScheduleBoneJobBatched(jobHandle, skinBatch);

            m_DeformJobHandle = ScheduleSkinDeformBatchedJob(jobHandle, skinBatch, batchCount, frameCount);
            m_CopyJobHandle = ScheduleCopySpriteRendererBuffersJob(jobHandle, batchCount);
            Profiling.scheduleJobs.End();

            JobHandle.ScheduleBatchedJobs();
            jobHandle = JobHandle.CombineDependencies(m_DeformJobHandle, m_CopyJobHandle);
            jobHandle.Complete();

            using (Profiling.setBatchDeformableBufferAndLocalAABB.Auto())
            {
                InternalEngineBridge.SetBatchDeformableBufferAndLocalAABBArray(m_SpriteRenderers, m_Buffers, m_BufferSizes, m_BoundsData);
            }

            foreach (SpriteSkin spriteSkin in m_SpriteSkins)
                // Check if the sprite skin was deformed this frame
                if (m_IsSpriteSkinActiveForDeform[spriteSkin.dataIndex] && m_LastDeformedFrame[spriteSkin.dataIndex] == frameCount)
                    spriteSkin.PostDeform();

            DeactivateDeformableBuffers();
        }
    }
}
