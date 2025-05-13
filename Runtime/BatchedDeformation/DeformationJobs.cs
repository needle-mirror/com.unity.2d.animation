using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.U2D.Animation
{
    internal struct PerSkinJobData
    {
        public int deformVerticesStartPos;
        public int2 bindPosesIndex;
        public int2 verticesIndex;
    }

    internal struct SpriteSkinData
    {
        public NativeCustomSlice<Vector3> vertices;
        public NativeCustomSlice<BoneWeight> boneWeights;
        public NativeCustomSlice<Matrix4x4> bindPoses;
        public NativeCustomSlice<Vector4> tangents;
        public bool hasTangents;
        public int spriteVertexStreamSize;
        public int spriteVertexCount;
        public int tangentVertexOffset;
        public int deformVerticesStartPos;
        public int transformId;
        public NativeCustomSlice<int> boneTransformId;
    }

    [BurstCompile]
    internal struct PrepareDeformJob : IJob
    {
        [ReadOnly]
        public NativeArray<PerSkinJobData> perSkinJobData;
        [ReadOnly]
        public int batchDataSize;
        [WriteOnly]
        public NativeArray<int2> boneLookupData;
        [WriteOnly]
        public NativeArray<int2> vertexLookupData;

        public void Execute()
        {
            for (int i = 0; i < batchDataSize; ++i)
            {
                PerSkinJobData jobData = perSkinJobData[i];
                for (int k = 0, j = jobData.bindPosesIndex.x; j < jobData.bindPosesIndex.y; ++j, ++k)
                {
                    boneLookupData[j] = new int2(i, k);
                }

                for (int k = 0, j = jobData.verticesIndex.x; j < jobData.verticesIndex.y; ++j, ++k)
                {
                    vertexLookupData[j] = new int2(i, k);
                }
            }
        }
    }

    [BurstCompile]
    internal struct BoneDeformBatchedJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float4x4> boneTransform;
        [ReadOnly]
        public NativeArray<float4x4> rootTransform;
        [ReadOnly]
        public NativeArray<int2> boneLookupData;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeHashMap<int, TransformAccessJob.TransformData> rootTransformIndex;
        [ReadOnly]
        public NativeHashMap<int, TransformAccessJob.TransformData> boneTransformIndex;
        [WriteOnly]
        public NativeArray<float4x4> finalBoneTransforms;

        public void Execute(int i)
        {
            int x = boneLookupData[i].x;
            int y = boneLookupData[i].y;
            SpriteSkinData ssd = spriteSkinData[x];
            int v = ssd.boneTransformId[y];
            int index = boneTransformIndex[v].transformIndex;
            if (index < 0)
                return;
            float4x4 aa = boneTransform[index];
            Matrix4x4 bb = ssd.bindPoses[y];
            int cc = rootTransformIndex[ssd.transformId].transformIndex;
            finalBoneTransforms[i] = math.mul(rootTransform[cc], math.mul(aa, bb));
        }
    }

    [BurstCompile]
    internal struct SkinDeformBatchedJob : IJobParallelFor
    {
        public NativeSlice<byte> vertices;

        [ReadOnly]
        public NativeArray<float4x4> finalBoneTransforms;
        [ReadOnly]
        public NativeArray<PerSkinJobData> perSkinJobData;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeArray<int2> vertexLookupData;

        public unsafe void Execute(int i)
        {
            int j = vertexLookupData[i].x;
            int k = vertexLookupData[i].y;

            PerSkinJobData perSkinData = perSkinJobData[j];
            SpriteSkinData spriteSkin = spriteSkinData[j];
            float3 srcVertex = (float3)spriteSkin.vertices[k];
            float4 tangents = (float4)spriteSkin.tangents[k];
            BoneWeight influence = spriteSkin.boneWeights[k];

            int bone0 = influence.boneIndex0 + perSkinData.bindPosesIndex.x;
            int bone1 = influence.boneIndex1 + perSkinData.bindPosesIndex.x;
            int bone2 = influence.boneIndex2 + perSkinData.bindPosesIndex.x;
            int bone3 = influence.boneIndex3 + perSkinData.bindPosesIndex.x;

            byte* deformedPosOffset = (byte*)vertices.GetUnsafePtr();
            byte* deformedPosStart = deformedPosOffset + spriteSkin.deformVerticesStartPos;
            NativeSlice<float3> deformableVerticesFloat3 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float3>(deformedPosStart, spriteSkin.spriteVertexStreamSize, spriteSkin.spriteVertexCount);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformableVerticesFloat3, NativeSliceUnsafeUtility.GetAtomicSafetyHandle(vertices));
#endif
            if (spriteSkin.hasTangents)
            {
                byte* deformedTanOffset = deformedPosStart + spriteSkin.tangentVertexOffset;
                NativeSlice<float4> deformableTangentsFloat4 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float4>(deformedTanOffset, spriteSkin.spriteVertexStreamSize, spriteSkin.spriteVertexCount);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformableTangentsFloat4, NativeSliceUnsafeUtility.GetAtomicSafetyHandle(vertices));
#endif
                float4 tangent = new float4(tangents.xyz, 0.0f);
                tangent =
                    math.mul(finalBoneTransforms[bone0], tangent) * influence.weight0 +
                    math.mul(finalBoneTransforms[bone1], tangent) * influence.weight1 +
                    math.mul(finalBoneTransforms[bone2], tangent) * influence.weight2 +
                    math.mul(finalBoneTransforms[bone3], tangent) * influence.weight3;
                deformableTangentsFloat4[k] = new float4(math.normalize(tangent.xyz), tangents.w);
            }

            deformableVerticesFloat3[k] =
                math.transform(finalBoneTransforms[bone0], srcVertex) * influence.weight0 +
                math.transform(finalBoneTransforms[bone1], srcVertex) * influence.weight1 +
                math.transform(finalBoneTransforms[bone2], srcVertex) * influence.weight2 +
                math.transform(finalBoneTransforms[bone3], srcVertex) * influence.weight3;
        }
    }

    [BurstCompile]
    internal struct CalculateSpriteSkinAABBJob : IJobParallelFor
    {
        public NativeSlice<byte> vertices;
        [ReadOnly]
        public NativeArray<bool> isSpriteSkinValidForDeformArray;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;

        [WriteOnly]
        public NativeArray<Bounds> bounds;

        public unsafe void Execute(int i)
        {
            if (!isSpriteSkinValidForDeformArray[i])
                return;

            SpriteSkinData spriteSkin = spriteSkinData[i];
            byte* deformedPosOffset = (byte*)vertices.GetUnsafePtr();
            NativeSlice<float3> deformableVerticesFloat3 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float3>(deformedPosOffset + spriteSkin.deformVerticesStartPos, spriteSkin.spriteVertexStreamSize, spriteSkin.spriteVertexCount);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformableVerticesFloat3, NativeSliceUnsafeUtility.GetAtomicSafetyHandle(vertices));
#endif

            bounds[i] = SpriteSkinUtility.CalculateSpriteSkinBounds(deformableVerticesFloat3);
        }
    }

    [BurstCompile]
    internal struct FillPerSkinJobSingleThread : IJob
    {
        public PerSkinJobData combinedSkinBatch;

        [ReadOnly]
        public NativeArray<bool> isSpriteSkinValidForDeformArray;

        public NativeArray<SpriteSkinData> spriteSkinDataArray;
        public NativeArray<PerSkinJobData> perSkinJobDataArray;

        public NativeArray<PerSkinJobData> combinedSkinBatchArray;

        public void Execute()
        {
            int startIndex = 0;
            int endIndex = spriteSkinDataArray.Length;
            for (int index = startIndex; index < endIndex; ++index)
            {
                SpriteSkinData spriteSkinData = spriteSkinDataArray[index];
                spriteSkinData.deformVerticesStartPos = -1;
                int vertexBufferSize = 0;
                int vertexCount = 0;
                int bindPoseCount = 0;
                if (isSpriteSkinValidForDeformArray[index])
                {
                    spriteSkinData.deformVerticesStartPos = combinedSkinBatch.deformVerticesStartPos;
                    vertexBufferSize = spriteSkinData.spriteVertexCount * spriteSkinData.spriteVertexStreamSize;
                    vertexCount = spriteSkinData.spriteVertexCount;
                    bindPoseCount = spriteSkinData.bindPoses.Length;
                }

                combinedSkinBatch.verticesIndex.x = combinedSkinBatch.verticesIndex.y;
                combinedSkinBatch.verticesIndex.y = combinedSkinBatch.verticesIndex.x + vertexCount;
                combinedSkinBatch.bindPosesIndex.x = combinedSkinBatch.bindPosesIndex.y;
                combinedSkinBatch.bindPosesIndex.y = combinedSkinBatch.bindPosesIndex.x + bindPoseCount;
                spriteSkinDataArray[index] = spriteSkinData;
                perSkinJobDataArray[index] = combinedSkinBatch;
                combinedSkinBatch.deformVerticesStartPos += vertexBufferSize;
            }

            combinedSkinBatchArray[0] = combinedSkinBatch;
        }
    }

    [BurstCompile]
    internal struct CopySpriteRendererBuffersJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<bool> isSpriteSkinValidForDeformArray;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly, NativeDisableUnsafePtrRestriction]
        public IntPtr ptrVertices;

        [WriteOnly]
        public NativeArray<IntPtr> buffers;
        [WriteOnly]
        public NativeArray<int> bufferSizes;

        public void Execute(int i)
        {
            SpriteSkinData skinData = spriteSkinData[i];
            IntPtr startVertices = default(IntPtr);
            int vertexBufferLength = 0;
            if (isSpriteSkinValidForDeformArray[i])
            {
                startVertices = ptrVertices + skinData.deformVerticesStartPos;
                vertexBufferLength = skinData.spriteVertexCount * skinData.spriteVertexStreamSize;
            }

            buffers[i] = startVertices;
            bufferSizes[i] = vertexBufferLength;
        }
    }

    [BurstCompile]
    internal struct CopySpriteRendererBoneTransformBuffersJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<bool> isSpriteSkinValidForDeformArray;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeArray<PerSkinJobData> perSkinJobData;

        [ReadOnly, NativeDisableUnsafePtrRestriction]
        public IntPtr ptrBoneTransforms;

        [WriteOnly]
        public NativeArray<IntPtr> buffers;
        [WriteOnly]
        public NativeArray<int> bufferSizes;

        public void Execute(int i)
        {
            SpriteSkinData skinData = spriteSkinData[i];
            PerSkinJobData skinJobData = perSkinJobData[i];
            IntPtr startMatrix = default(IntPtr);
            int matrixLength = 0;
            if (isSpriteSkinValidForDeformArray[i])
            {
                startMatrix = ptrBoneTransforms + (skinJobData.bindPosesIndex.x * 64);
                matrixLength = skinData.boneTransformId.Length;
            }

            buffers[i] = startMatrix;
            bufferSizes[i] = matrixLength;
        }
    }
}
