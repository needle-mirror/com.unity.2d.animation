using System;

namespace UnityEngine.U2D.Animation.Profiler
{
    [Serializable]
    struct SpriteSkinProfilerFrameData
    {
        public enum SpriteSkinType
        {
            Unknown = 0,
            CPU = 1,
            GPU = 2
        }

        public EntityId gameObjectEntityId;
        public EntityId rootBoneGameObjectEntityId;
        public int boneCount;
        public int type;

        public static string GetSpriteSkinTypeName(int type)
        {
            switch (type)
            {
                case (int)SpriteSkinType.CPU:
                    return "CPU";
                case (int)SpriteSkinType.GPU:
                    return "GPU";
            }

            return "";
        }

    }
}
