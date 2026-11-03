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
        public NativeArray<byte> transformState;
        [ReadOnly]
        public NativeArray<SpriteSkinData> spriteSkinData;
        [ReadOnly]
        public NativeHashMap<EntityId, TransformAccessJob.TransformData> boneTransformIndex;
        [WriteOnly]
        public NativeArray<bool> hasBoneTransformsChanged;

        public void Execute(int skinIndex)
        {
            SpriteSkinData skinData = spriteSkinData[skinIndex];
            bool hasChanged = false;

            for (int boneIndex = 0; boneIndex < skinData.boneTransformId.Length; boneIndex++)
            {
                EntityId boneId = skinData.boneTransformId[boneIndex];

                if (!boneTransformIndex.TryGetValue(boneId, out TransformAccessJob.TransformData transformData))
                {
                    hasChanged = true;
                    break;
                }

                int transformIndex = transformData.transformIndex;
                if (transformIndex < 0)
                    continue;

                // Moved, or destroyed since the last sweep and so deformed with identity like a removed entry.
                if (transformState[transformIndex] != TransformAccessJob.k_TransformUnchanged)
                {
                    hasChanged = true;
                    break;
                }
            }

            hasBoneTransformsChanged[skinIndex] = hasChanged;
        }
    }
}
