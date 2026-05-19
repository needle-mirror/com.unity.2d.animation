using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Animation.Profiler.UI
{
    [UxmlElement]
    partial class SpriteSkinStatisticView : VisualElement
    {
        const string k_UXML = "Packages/com.unity.2d.animation/Profiler/Editor/UI/SpriteSkinStatisticView/SpriteSkinStatisticView.uxml";

        public Label SpriteSkinCPULabel { get; private set; }
        public Label SpriteSkinCPUVerticesLabel { get; private set; }
        public Label SpriteSkinGPULabel { get; private set; }
        public Label SpriteSkinGPUVerticesLabel { get; private set; }
        public Label SpriteSkinBoneLabel { get; private set; }
        public Label DeformationTimeLabel { get; private set; }
        Toggle m_LiveUpdate;

        public SpriteSkinStatisticView()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UXML);
            visualTree.CloneTree(this);

            // Query and cache label references
            SpriteSkinCPULabel = this.Q<Label>("SpriteSkinCPULabel");
            SpriteSkinCPUVerticesLabel = this.Q<Label>("SpriteSkinCPUVerticesLabel");
            SpriteSkinGPULabel = this.Q<Label>("SpriteSkinGPULabel");
            SpriteSkinGPUVerticesLabel = this.Q<Label>("SpriteSkinGPUVerticesLabel");
            SpriteSkinBoneLabel = this.Q<Label>("SpriteSkinBoneLabel");
            DeformationTimeLabel = this.Q<Label>("DeformationTimeLabel");
            m_LiveUpdate = this.Q<Toggle>("EnableStatisticsToggle");
        }


        public void SetStatistic(long cpu, long cpuDeform, long gpu, long gpuDeform, long boneProcessed, float deformTime)
        {
            SpriteSkinCPULabel.text = cpu.ToString();
            SpriteSkinCPUVerticesLabel.text = cpuDeform.ToString();
            SpriteSkinGPULabel.text = gpu.ToString();
            SpriteSkinGPUVerticesLabel.text = gpuDeform.ToString();
            SpriteSkinBoneLabel.text = boneProcessed.ToString();
            DeformationTimeLabel.text = $"{deformTime:F2}ms";
        }

        public bool IsLiveUpdateEnabled()
        {
            return m_LiveUpdate.value;
        }
    }
}
