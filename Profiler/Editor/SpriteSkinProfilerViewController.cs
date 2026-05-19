using System;
using System.Collections.Generic;
using Unity.Profiling.Editor;
using UnityEditor.Profiling;
using UnityEditor.U2D.Animation.Profiler.UI;
using UnityEngine;
using UnityEngine.U2D.Animation.Profiler;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.Profiler
{
    /// <summary>
    /// Custom view controller for 2D Animation profiler module using UIToolkit
    /// </summary>
    class SpriteSkinProfilerViewController : ProfilerModuleViewController
    {
        private SpriteSkinProfilerView m_Root;
        public SpriteSkinProfilerViewController(ProfilerWindow profilerWindow)
            : base(profilerWindow)
        {
            profilerWindow.SelectedFrameIndexChanged += OnProfilerFrameChange;
        }


        internal static (IEnumerable<SpriteSkinHierarchyNodeData> frameData, long cpu, long cpuDeform, long gpu, long gpuDeform, long boneProcessed, float deformTime)
            ExtractFrameData(long obj)
        {
            int selectedFrameIndexInt32 = Convert.ToInt32(obj);
            try
            {
                using (RawFrameDataView frameData = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(selectedFrameIndexInt32, 0))
                {
                    Unity.Collections.NativeArray<SpriteSkinProfilerFrameData> data = frameData.GetFrameMetaData<SpriteSkinProfilerFrameData>(Animation2DProfilerMarkers.k_Animation2DProfilerProjectId, 0);
                    Dictionary<EntityId, SpriteSkinHierarchyNodeData> nodeDataDic = new Dictionary<EntityId, SpriteSkinHierarchyNodeData>();
                    int id = 100;
                    for (int i = 0; i < data.Length; i++)
                    {
                        SpriteSkinProfilerFrameData group = data[i];
                        SpriteSkinHierarchyNodeData node = null;
                        EntityId rootBoneEntityId = group.rootBoneGameObjectEntityId != EntityId.None ? group.rootBoneGameObjectEntityId : group.gameObjectEntityId;
                        if (rootBoneEntityId != EntityId.None)
                        {
                            if (!nodeDataDic.TryGetValue(rootBoneEntityId, out node))
                            {
                                if (frameData.GetUnityObjectInfo(rootBoneEntityId, out FrameDataView.UnityObjectInfo unityObjectInfo))
                                {
                                    int boneCount = rootBoneEntityId == group.gameObjectEntityId && group.gameObjectEntityId != group.rootBoneGameObjectEntityId ? group.boneCount : 0;
                                    node = new SpriteSkinHierarchyNodeData(unityObjectInfo.name, rootBoneEntityId, boneCount, id++, group.type, "");
                                    nodeDataDic[rootBoneEntityId] = node;
                                }
                            }
                        }

                        if (group.rootBoneGameObjectEntityId != EntityId.None)
                        {
                            if (frameData.GetUnityObjectInfo(group.gameObjectEntityId, out FrameDataView.UnityObjectInfo unityObjectInfo))
                            {
                                node?.AddChild(new SpriteSkinHierarchyNodeData(unityObjectInfo.name, group.gameObjectEntityId, group.boneCount, id++, 0, ""));
                            }
                        }
                    }

                    int markerId = frameData.GetMarkerId(Animation2DProfilerMarkers.k_SpriteSkinCPUProcessedProfilerCounterName);
                    long cpu = frameData.GetCounterValueAsLong(markerId);
                    markerId = frameData.GetMarkerId(Animation2DProfilerMarkers.k_SpriteSkinCPUVertexProcessedProfilerCounterName);
                    long cpuDeform = frameData.GetCounterValueAsLong(markerId);
                    markerId = frameData.GetMarkerId(Animation2DProfilerMarkers.k_SpriteSkinGPUProcessedProfilerCounterName);
                    long gpu = frameData.GetCounterValueAsLong(markerId);
                    markerId = frameData.GetMarkerId(Animation2DProfilerMarkers.k_SpriteSkinGPUVertexProcessedProfilerCounterName);
                    long gpuDeform = frameData.GetCounterValueAsLong(markerId);
                    markerId = frameData.GetMarkerId(Animation2DProfilerMarkers.k_SpriteSkinBoneTransformedProfilerCounterName);
                    long boneProcessed = frameData.GetCounterValueAsLong(markerId);
                    float deformTime = 0;
                    int d = frameData.GetMarkerId(Animation2DProfilerMarkers.k_DeformationManagerLateUpdateProfilerMarkerName);
                    if (d != FrameDataView.invalidMarkerId)
                    {
                        int sampleCount = frameData.sampleCount;
                        for (int i = 0; i < sampleCount; ++i)
                        {
                            if (d != frameData.GetSampleMarkerId(i))
                                continue;

                            deformTime += frameData.GetSampleTimeMs(i);
                        }
                    }
                    return (nodeDataDic.Values, cpu, cpuDeform, gpu, gpuDeform, boneProcessed, deformTime);
                }
            }
            catch (Exception)
            {
                return (Array.Empty<SpriteSkinHierarchyNodeData>(), 0, 0, 0, 0, 0, 0);
            }

        }

        void OnProfilerFrameChange(long obj)
        {
#if ENABLE_PROFILER && PROFILING_INSTALLED
            if (Event.current != null && Event.current.type == EventType.Layout)
                return;
            if (obj < 0)
            {
                m_Root.SetHierarchyData(Array.Empty<SpriteSkinHierarchyNodeData>());
                m_Root.SetStatistic(0, 0, 0, 0, 0, 0);
                return;
            }
            CreateRootIfNotExist();
            if (UnityEngine.Profiling.Profiler.enabled && !m_Root.IsLiveUpdateEnabled()) // don't update when recording
                return;
            (IEnumerable<SpriteSkinHierarchyNodeData> frameData, long cpu, long cpuDeform, long gpu, long gpuDeform, long boneProcessed, float deformTime) frameData = ExtractFrameData(obj);
            m_Root.SetHierarchyData(frameData.frameData);
            m_Root.SetStatistic(frameData.cpu, frameData.cpuDeform, frameData.gpu, frameData.gpuDeform, frameData.boneProcessed, frameData.deformTime);
#endif
        }

        SpriteSkinProfilerView CreateRootIfNotExist()
        {
            if (m_Root == null)
            {
                m_Root = new SpriteSkinProfilerView();
            }
            return m_Root;
        }

        protected override VisualElement CreateView()
        {
            CreateRootIfNotExist();
            OnProfilerFrameChange(this.ProfilerWindow.selectedFrameIndex);
            return m_Root;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ProfilerWindow != null)
            {
                ProfilerWindow.SelectedFrameIndexChanged -= OnProfilerFrameChange;
            }
            m_Root = null;
            base.Dispose(disposing);
        }
    }
}
