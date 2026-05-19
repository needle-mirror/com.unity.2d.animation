using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class WeightPainterToolWrapper : BaseTool, IWeightMapVisualization
    {
        [SerializeField]
        private WeightPainterMode m_PaintMode;

        [SerializeField]
        private WeightPainterTool m_WeightPainterTool;

        private SkeletonTool m_SkeletonTool;
        private string m_Title;

        public bool displaysWeights => true;

        public override IMeshPreviewBehaviour previewBehaviour
        {
            get { return m_WeightPainterTool.previewBehaviour; }
        }

        public WeightPainterTool weightPainterTool
        {
            get { return m_WeightPainterTool; }
            set { m_WeightPainterTool = value; }
        }

        public SkeletonTool skeletonTool
        {
            get { return m_SkeletonTool; }
            set { m_SkeletonTool = value; }
        }

        public WeightPainterMode paintMode
        {
            get { return m_PaintMode; }
            set { m_PaintMode = value; }
        }

        public string title
        {
            set { m_Title = value; }
        }

        public override bool allowsSpriteSelection
        {
            get
            {
                return skeletonTool != null && skeletonTool.hoveredBone == null;
            }
        }

        public override int defaultControlID
        {
            get { return weightPainterTool.defaultControlID; }
        }

        protected override void OnActivate()
        {
            weightPainterTool.Activate();
            weightPainterTool.panelTitle = m_Title;
        }

        protected override void OnDeactivate()
        {
            weightPainterTool.Deactivate();
        }

        protected override void OnGUI()
        {
            weightPainterTool.paintMode = paintMode;

            weightPainterTool.DoGUI();
        }
    }
}
