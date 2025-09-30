using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace UnityEngine.U2D.Animation
{
    internal static class SpriteSkinBoneBoundsUtility
    {
        [BurstCompile]
        private static unsafe void CalculateBoneBoundsBurst(
            ref NativeSlice<Vector3> vertices,
            ref NativeSlice<BoneWeight> boneWeights,
            ref NativeSlice<Matrix4x4> bindPoses,
            int boneCount,
            ref NativeArray<Bounds> boneBounds)
        {
            // Calculate bounds for each bone in bone local space (like SkinnedMeshRenderer)
            for (int boneIndex = 0; boneIndex < boneCount; ++boneIndex)
            {
                float3 min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
                float3 max = new float3(float.MinValue, float.MinValue, float.MinValue);

                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight weight = boneWeights[i];
                    float influence = 0f;
                    if (weight.boneIndex0 == boneIndex) influence += weight.weight0;
                    if (weight.boneIndex1 == boneIndex) influence += weight.weight1;
                    if (weight.boneIndex2 == boneIndex) influence += weight.weight2;
                    if (weight.boneIndex3 == boneIndex) influence += weight.weight3;
                    if (influence > 0f)
                    {
                        // Transform sprite local vertex to bone local coordinates using bindPose matrix
                        float3 spriteVertex = vertices[i];
                        float3 boneLocalVertex = math.transform(bindPoses[boneIndex], spriteVertex);
                        min = math.min(min, boneLocalVertex);
                        max = math.max(max, boneLocalVertex);
                    }
                }

                float3 ext = (max - min) * 0.5f;
                float3 ctr = min + ext;
                boneBounds[boneIndex] = new Bounds(ctr, ext * 2);
            }
        }

        /// <summary>
        /// Calculates bounds for each bone in bone local space.
        /// For each bone, vertices are transformed using the bind pose to bone local space.
        /// The returned NativeArray is allocated with Allocator.Persistent and must be disposed by the caller.
        /// This method assumes the sprite has bones - use vertex-based bounds calculation mode for sprites without bones.
        /// </summary>
        /// <param name="sprite">The Sprite to calculate bone AABBs for.</param>
        /// <returns>NativeArray of Bounds, one per bone in bone local space.</returns>
        public static unsafe NativeArray<Bounds> CalculateBoneBounds(Sprite sprite)
        {
            if (sprite == null)
                return default;

            NativeSlice<Vector3> vertices = sprite.GetVertexAttribute<Vector3>(Rendering.VertexAttribute.Position);
            NativeSlice<BoneWeight> boneWeights = sprite.GetVertexAttribute<BoneWeight>(Rendering.VertexAttribute.BlendWeight);
            NativeSlice<Matrix4x4> bindPoses = sprite.GetBindPoses();
            int boneCount = bindPoses.Length;

            NativeArray<Bounds> boneBounds = new NativeArray<Bounds>(boneCount, Allocator.Persistent);

            CalculateBoneBoundsBurst(ref vertices, ref boneWeights, ref bindPoses, boneCount, ref boneBounds);

            return boneBounds;
        }
    }
}
