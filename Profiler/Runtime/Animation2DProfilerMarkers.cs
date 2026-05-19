using System;
using Unity.Profiling;

namespace UnityEngine.U2D.Animation.Profiler
{
    static class Animation2DProfilerMarkers
    {
        public static readonly Guid k_Animation2DProfilerProjectId = new Guid("7a3f8b2c-4d6e-4f1a-9c8b-5e7d3a2f1b4c");

        public const string k_SpriteSkinCPUProcessedProfilerCounterName = "SpriteSkin Processed (CPU)";
        public const string k_SpriteSkinCPUVertexProcessedProfilerCounterName = "Vertices Processed (CPU)";
        public const string k_SpriteSkinGPUProcessedProfilerCounterName = "SpriteSkin Processed (GPU)";
        public const string k_SpriteSkinGPUVertexProcessedProfilerCounterName = "Vertices Processed (GPU)";
        public const string k_SpriteSkinBoneTransformedProfilerCounterName = "Bones Transformed";
#if PROFILING_INSTALLED

        public static readonly ProfilerCounterValue<int> s_SpriteSkinCPUProcessed =
            new ProfilerCounterValue<int>(ProfilerCategory.U2D, k_SpriteSkinCPUProcessedProfilerCounterName, ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<int> s_SpriteSkinCPUVertexProcessed =
            new ProfilerCounterValue<int>(ProfilerCategory.U2D, k_SpriteSkinCPUVertexProcessedProfilerCounterName, ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<int> s_SpriteSkinGPUProcessed =
            new ProfilerCounterValue<int>(ProfilerCategory.U2D, k_SpriteSkinGPUProcessedProfilerCounterName, ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<int> s_SpriteSkinGPUVertexProcessed =
            new ProfilerCounterValue<int>(ProfilerCategory.U2D, k_SpriteSkinGPUVertexProcessedProfilerCounterName, ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<int> s_SpriteSkinBoneTransformed =
            new ProfilerCounterValue<int>(ProfilerCategory.U2D, k_SpriteSkinBoneTransformedProfilerCounterName, ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
#endif

        public const int k_SpriteSkinProfilerFrameMetaDataTag = 0;

        public const string k_CacheCurrentSpriteProfilerMarkerName = "SpriteSkin.CacheCurrentSprite";
        public const string k_CacheHierarchyProfilerMarkerName = "SpriteSkin.CacheHierarchy";
        public const string k_GetSpriteBonesTransformFromGuidProfilerMarkerName = "SpriteSkin.GetSpriteBoneTransformsFromGuid";
        public const string k_GetSpriteBonesTransformFromPathProfilerMarkerName = "SpriteSkin.GetSpriteBoneTransformsFromPath";
        public const string k_DeformationManagerLateUpdateProfilerMarkerName = "DeformationManager.LateUpdate";

        public static readonly ProfilerMarker cacheCurrentSpriteProfilerMarker = new ProfilerMarker(k_CacheCurrentSpriteProfilerMarkerName);
        public static readonly ProfilerMarker cacheHierarchyProfilerMarker = new ProfilerMarker(k_CacheHierarchyProfilerMarkerName);
        public static readonly ProfilerMarker getSpriteBonesTransformFromGuidProfilerMarker = new ProfilerMarker(k_GetSpriteBonesTransformFromGuidProfilerMarkerName);
        public static readonly ProfilerMarker getSpriteBonesTransformFromPathProfilerMarker = new ProfilerMarker(k_GetSpriteBonesTransformFromPathProfilerMarkerName);
        public static readonly ProfilerMarker deformationManagerLateUpdateProfilerMarker = new ProfilerMarker(ProfilerCategory.U2D, k_DeformationManagerLateUpdateProfilerMarkerName);

    }
}
