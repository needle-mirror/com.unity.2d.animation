using System;
using Unity.Profiling;
using Unity.Profiling.Editor;
using UnityEngine;
using UnityEngine.U2D.Animation.Profiler;

namespace UnityEditor.U2D.Animation.Profiler
{
    [Serializable]
    [ProfilerModuleMetadata("2D Animation", IconPath = "Packages/com.unity.2d.animation/Editor/Assets/ComponentIcons/Animation.SpriteSkin.png")]
    class SpriteSkinProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(Animation2DProfilerMarkers.k_SpriteSkinCPUProcessedProfilerCounterName, ProfilerCategory.U2D),
            new ProfilerCounterDescriptor(Animation2DProfilerMarkers.k_SpriteSkinCPUVertexProcessedProfilerCounterName, ProfilerCategory.U2D),
            new ProfilerCounterDescriptor(Animation2DProfilerMarkers.k_SpriteSkinGPUProcessedProfilerCounterName, ProfilerCategory.U2D),
            new ProfilerCounterDescriptor(Animation2DProfilerMarkers.k_SpriteSkinGPUVertexProcessedProfilerCounterName, ProfilerCategory.U2D),
            new ProfilerCounterDescriptor(Animation2DProfilerMarkers.k_SpriteSkinBoneTransformedProfilerCounterName, ProfilerCategory.U2D),
        };

        public SpriteSkinProfilerModule()
            : base(k_Counters, ProfilerModuleChartType.Line) { }

        public override ProfilerModuleViewController CreateDetailsViewController()
        {
            return new SpriteSkinProfilerViewController(ProfilerWindow);
        }
    }
}
