using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.U2D.Animation
{
    /// Each skin is processed differently based on this metadata.
    /// deformVerticesStartPos: The starting position of the deformable vertices in the buffer.
    // bindPosesIndex: A range (int2) indicating the start and end indices of the bind poses for the sprite skin.
    // verticesIndex: A range (int2) indicating the start and end indices of the vertices for the sprite skin.
    internal struct PerSkinJobData
    {
        public int deformVerticesStartPos;
        public int2 bindPosesIndex;
        public int2 verticesIndex;
    }

    /// Contains the data required for deforming a sprite.
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
        public int previousDeformVerticesStartPos;
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

        public void Execute()
        {
            for (int i = 0; i < batchDataSize; ++i)
            {
                PerSkinJobData jobData = perSkinJobData[i];
                for (int k = 0, j = jobData.bindPosesIndex.x; j < jobData.bindPosesIndex.y; ++j, ++k)
                {
                    boneLookupData[j] = new int2(i, k);
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
        public NativeSlice<byte> previousVertices;

        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeArray<PerSkinJobData> perSkinJobData;
        [ReadOnly]
        public NativeArray<float4x4> finalBoneTransforms;
        [ReadOnly]
        public NativeArray<bool> isSpriteSkinValidForDeformArray;
        [ReadOnly]
        public NativeArray<bool> hasBoneTransformsChanged;

        [WriteOnly]
        public NativeArray<Bounds> bounds;

        // The last frame when deformation occurred for each SpriteSkin
        [WriteOnly]
        public NativeArray<int> lastDeformedFrame;

        // The current frame count
        public int frameCount;

        [BurstCompile]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static unsafe void CopyBuffer(byte* currentPosStart, byte* previousPosStart, int streamSize, int vertexCount)
        {
            for (int i = 0; i < vertexCount; ++i)
            {
                byte* src = previousPosStart + i * streamSize;
                byte* dst = currentPosStart + i * streamSize;
                UnsafeUtility.MemCpy(dst, src, streamSize);
            }
        }

        public unsafe void Execute(int spriteIndex)
        {
            if (!isSpriteSkinValidForDeformArray[spriteIndex])
                return;

            SpriteSkinData spriteSkin = spriteSkinData[spriteIndex];
            PerSkinJobData perSkinData = perSkinJobData[spriteIndex];

            // If deformation is not needed and previous frame cache is available
            if (!hasBoneTransformsChanged[spriteIndex] && spriteSkin.previousDeformVerticesStartPos >= 0)
            {
                // Copy previous frame's vertex data (all attributes)
                byte* currentPosStart = (byte*)vertices.GetUnsafePtr() + spriteSkin.deformVerticesStartPos;
                byte* previousPosStart = (byte*)previousVertices.GetUnsafePtr() + spriteSkin.previousDeformVerticesStartPos;

                int streamSize = spriteSkin.spriteVertexStreamSize;
                int vertexCount = spriteSkin.spriteVertexCount;

                // Using a fixed streamSize enables Burst/LLVM to optimize memcpy as a constant-size copy (SIMD/unrolled).
                if (streamSize == 12) // Postion
                    CopyBuffer(currentPosStart, previousPosStart, 12, vertexCount);
                else if (streamSize == 28) // Position + Tangent
                    CopyBuffer(currentPosStart, previousPosStart, 28, vertexCount);
                else // Other custom formats
                    CopyBuffer(currentPosStart, previousPosStart, streamSize, vertexCount);

                // AABB (bounds) is not recalculated here because both the bone transforms and vertex data are unchanged.
                // The bounds array is persistent and already contains the correct value from the previous frame at this index.

                return;
            }

            // Deformation is performed, so record the frame
            lastDeformedFrame[spriteIndex] = frameCount;

            byte* deformedPosOffset = (byte*)vertices.GetUnsafePtr();
            byte* deformedPosStart = deformedPosOffset + spriteSkin.deformVerticesStartPos;
            NativeSlice<float3> deformableVerticesFloat3 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float3>(deformedPosStart, spriteSkin.spriteVertexStreamSize, spriteSkin.spriteVertexCount);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformableVerticesFloat3, NativeSliceUnsafeUtility.GetAtomicSafetyHandle(vertices));
#endif

            byte* deformedTanOffset = deformedPosStart + spriteSkin.tangentVertexOffset;
            NativeSlice<float4> deformableTangentsFloat4 = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float4>(deformedTanOffset, spriteSkin.spriteVertexStreamSize, spriteSkin.spriteVertexCount);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref deformableTangentsFloat4, NativeSliceUnsafeUtility.GetAtomicSafetyHandle(vertices));
#endif
            // Find min and max positions of all vertices
            float3 min = float.MaxValue;
            float3 max = float.MinValue;

            if (spriteSkin.boneTransformId.Length != 1)
            {
                for (int i = 0; i < spriteSkin.spriteVertexCount; ++i)
                {
                    float3 srcVertex = (float3)spriteSkin.vertices[i];
                    float4 tangents = (float4)spriteSkin.tangents[i];
                    BoneWeight influence = spriteSkin.boneWeights[i];

                    int bone0 = influence.boneIndex0 + perSkinData.bindPosesIndex.x;
                    int bone1 = influence.boneIndex1 + perSkinData.bindPosesIndex.x;
                    int bone2 = influence.boneIndex2 + perSkinData.bindPosesIndex.x;
                    int bone3 = influence.boneIndex3 + perSkinData.bindPosesIndex.x;

                    if (spriteSkin.hasTangents)
                    {
                        float4 tangent = new float4(tangents.xyz, 0.0f);
                        tangent =
                            math.mul(finalBoneTransforms[bone0], tangent) * influence.weight0 +
                            math.mul(finalBoneTransforms[bone1], tangent) * influence.weight1 +
                            math.mul(finalBoneTransforms[bone2], tangent) * influence.weight2 +
                            math.mul(finalBoneTransforms[bone3], tangent) * influence.weight3;
                        deformableTangentsFloat4[i] = new float4(math.normalize(tangent.xyz), tangents.w);
                    }

                    deformableVerticesFloat3[i] =
                        math.transform(finalBoneTransforms[bone0], srcVertex) * influence.weight0 +
                        math.transform(finalBoneTransforms[bone1], srcVertex) * influence.weight1 +
                        math.transform(finalBoneTransforms[bone2], srcVertex) * influence.weight2 +
                        math.transform(finalBoneTransforms[bone3], srcVertex) * influence.weight3;

                    min = math.min(min, deformableVerticesFloat3[i]);
                    max = math.max(max, deformableVerticesFloat3[i]);
                }
            }
            else
            {
                int bone0 = spriteSkin.boneWeights[0].boneIndex0 + perSkinData.bindPosesIndex.x;

                if (spriteSkin.hasTangents)
                {
                    for (int i = 0; i < spriteSkin.spriteVertexCount; ++i)
                    {
                        float4 tangents = (float4)spriteSkin.tangents[i];

                        float4 tangent = new float4(tangents.xyz, 0.0f);
                        tangent = math.mul(finalBoneTransforms[bone0], tangent);
                        deformableTangentsFloat4[i] = new float4(math.normalize(tangent.xyz), tangents.w);
                    }
                }

                for (int i = 0; i < spriteSkin.spriteVertexCount; ++i)
                {
                    float3 srcVertex = (float3)spriteSkin.vertices[i];
                    deformableVerticesFloat3[i] = math.transform(finalBoneTransforms[bone0], srcVertex);

                    min = math.min(min, deformableVerticesFloat3[i]);
                    max = math.max(max, deformableVerticesFloat3[i]);
                }
            }

            // Calculate center and extents from min/max
            float3 ext = (max - min) * 0.5F;
            float3 ctr = min + ext;

            bounds[spriteIndex] = new Bounds(ctr, ext * 2);
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

                // Save previous frame's valid buffer position (for cache)
                spriteSkinData.previousDeformVerticesStartPos = spriteSkinData.deformVerticesStartPos;

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
