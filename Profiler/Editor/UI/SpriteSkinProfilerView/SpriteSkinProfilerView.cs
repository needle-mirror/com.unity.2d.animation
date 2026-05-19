using System;
using System.Collections.Generic;
using UnityEditor.U2D.Animation.Profiler.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.Profiler
{
    class SpriteSkinProfilerView : VisualElement
    {
        const string k_UXML = "Packages/com.unity.2d.animation/Profiler/Editor/UI/SpriteSkinProfilerView/SpriteSkinProfilerView.uxml";
        SpriteSkinHierarchyView m_SpriteSkinHierarchyView;
        SpriteSkinStatisticView m_SpriteSkinStatisticView;

        public SpriteSkinProfilerView()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UXML);
            visualTree.CloneTree(this);
            TwoPaneSplitView splitView = this.Q<TwoPaneSplitView>();
            splitView.fixedPaneInitialDimension = 300;
            splitView.fixedPaneIndex = 0;

            m_SpriteSkinHierarchyView = new SpriteSkinHierarchyView();
            m_SpriteSkinStatisticView = new SpriteSkinStatisticView();
            TwoPaneSplitView twoPaneSplitView = this.Q<TwoPaneSplitView>();
            twoPaneSplitView.Add(m_SpriteSkinStatisticView);
            twoPaneSplitView.Add(m_SpriteSkinHierarchyView);
#if ENABLE_PROFILER && PROFILING_INSTALLED
            this.Q<Label>("noProfiling").style.display = DisplayStyle.None;
            twoPaneSplitView.style.display = DisplayStyle.Flex;
#else
            this.Q<Label>("noProfiling").style.display = DisplayStyle.Flex;
            twoPaneSplitView.style.display = DisplayStyle.None;
#endif
        }

        public void SetHierarchyData(IEnumerable<SpriteSkinHierarchyNodeData> values)
        {
            m_SpriteSkinHierarchyView.SetData(values);
        }

        public void SetStatistic(long cpu, long cpuDeform, long gpu, long gpuDeform, long boneProcessed, float deformTime)
        {
            m_SpriteSkinStatisticView.SetStatistic(cpu, cpuDeform, gpu, gpuDeform, boneProcessed, deformTime);
        }

        public bool IsLiveUpdateEnabled()
        {
            return m_SpriteSkinStatisticView.IsLiveUpdateEnabled();
        }
    }
}
