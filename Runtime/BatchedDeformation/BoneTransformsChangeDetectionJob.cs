using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.U2D.Animation
{
    [BurstCompile]
    internal struct BoneTransformsChangeDetectionJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<bool> transformChanged;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeHashMap<int, TransformAccessJob.TransformData> boneTransformIndex;
        [WriteOnly]
        public NativeArray<bool> hasBoneTransformsChanged;

        public void Execute(int skinIndex)
        {
            SpriteSkinData skinData = spriteSkinData[skinIndex];
            bool hasChanged = false;

            for (int boneIndex = 0; boneIndex < skinData.boneTransformId.Length; boneIndex++)
            {
                int boneId = skinData.boneTransformId[boneIndex];

                int transformIndex = boneTransformIndex[boneId].transformIndex;
                if (transformIndex < 0)
                    continue;

                if (transformChanged[transformIndex])
                {
                    hasChanged = true;
                    break;
                }
            }

            hasBoneTransformsChanged[skinIndex] = hasChanged;
        }
    }
}
