using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.U2D.Animation
{
    [BurstCompile]
    internal struct UpdateBoundJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int> rootTransformId;
        [ReadOnly]
        public NativeArray<int> rootBoneTransformId;
        [ReadOnly]
        public NativeArray<float4x4> rootTransform;
        [ReadOnly]
        public NativeArray<float4x4> boneTransform;
        [ReadOnly]
        public NativeHashMap<int, TransformAccessJob.TransformData> rootTransformIndex;
        [ReadOnly]
        public NativeHashMap<int, TransformAccessJob.TransformData> boneTransformIndex;
        [ReadOnly]
        public NativeArray<Bounds> spriteSkinBound;
        public NativeArray<Bounds> bounds;

        public void Execute(int i)
        {
            //for (int i = 0; i < rootTransformId.Length; ++i)
            {
                Bounds unityBounds = spriteSkinBound[i];
                int rootIndex = rootTransformIndex[rootTransformId[i]].transformIndex;
                int rootBoneIndex = boneTransformIndex[rootBoneTransformId[i]].transformIndex;
                if (rootIndex < 0 || rootBoneIndex < 0)
                    return;
                float4x4 rootTransformMatrix = rootTransform[rootIndex];
                float4x4 rootBoneTransformMatrix = boneTransform[rootBoneIndex];
                float4x4 matrix = math.mul(rootTransformMatrix, rootBoneTransformMatrix);
                float4 center = new float4(unityBounds.center, 1);
                float4 extents = new float4(unityBounds.extents, 0);
                float4 p0 = math.mul(matrix, center + new float4(-extents.x, -extents.y, extents.z, extents.w));
                float4 p1 = math.mul(matrix, center + new float4(-extents.x, extents.y, extents.z, extents.w));
                float4 p2 = math.mul(matrix, center + extents);
                float4 p3 = math.mul(matrix, center + new float4(extents.x, -extents.y, extents.z, extents.w));
                float4 min = math.min(p0, math.min(p1, math.min(p2, p3)));
                float4 max = math.max(p0, math.max(p1, math.max(p2, p3)));
                extents = (max - min) * 0.5f;
                center = min + extents;
                bounds[i] = new Bounds()
                {
                    center = new Vector3(center.x, center.y, center.z),
                    extents = new Vector3(extents.x, extents.y, extents.z)
                };
            }
        }
    }
}
