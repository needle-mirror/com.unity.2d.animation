using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal interface ISkinningCachePersistentState
    {
        String lastSpriteId
        {
            get;
            set;
        }

        Tools lastUsedTool
        {
            get;
            set;
        }

        List<int> lastBoneSelectionIds
        {
            get;
        }

        Texture2D lastTexture
        {
            get;
            set;
        }

        SerializableDictionary<int, BonePose> lastPreviewPose
        {
            get;
        }

        SkinningMode lastMode
        {
            get;
            set;
        }

        bool lastVisibilityToolActive
        {
            get;
            set;
        }

        IndexedSelection lastVertexSelection
        {
            get;
        }

        float lastBrushSize
        {
            get;
            set;
        }

        float lastBrushHardness
        {
            get;
            set;
        }

        float lastBrushStep
        {
            get;
            set;
        }
    }

    [Serializable]
    internal class SkinningCachePersistentState
        : ScriptableSingleton<SkinningCachePersistentState>
        , ISkinningCachePersistentState
    {
        [SerializeField] private Tools m_LastUsedTool = Tools.EditPose;

        [SerializeField] private SkinningMode m_LastMode = SkinningMode.Character;

        [SerializeField] private string m_LastSpriteId = String.Empty;

        [SerializeField] private List<int> m_LastBoneSelectionIds = new List<int>();

        [SerializeField] private Texture2D m_LastTexture;

        [SerializeField]
        private SerializableDictionary<int, BonePose> m_SkeletonPreviewPose =
            new SerializableDictionary<int, BonePose>();

        [SerializeField] private IndexedSelection m_VertexSelection;

        [SerializeField] private bool m_VisibilityToolActive;

        [SerializeField] private float m_LastBrushSize = 25f;
        [SerializeField] private float m_LastBrushHardness = 1f;
        [SerializeField] private float m_LastBrushStep = 20f;

        public SkinningCachePersistentState()
        {
            m_VertexSelection = new IndexedSelection();
        }

        public void SetDefault()
        {
            m_LastUsedTool = Tools.EditPose;
            m_LastMode = SkinningMode.Character;
            m_LastSpriteId = String.Empty;
            m_LastBoneSelectionIds.Clear();
            m_LastTexture = null;
            m_VertexSelection.Clear();
            m_SkeletonPreviewPose.Clear();
            m_VisibilityToolActive = false;
        }

        public string lastSpriteId
        {
            get { return m_LastSpriteId; }
            set { m_LastSpriteId = value; }
        }

        public Tools lastUsedTool
        {
            get { return m_LastUsedTool; }
            set { m_LastUsedTool = value; }
        }

        public List<int> lastBoneSelectionIds
        {
            get { return m_LastBoneSelectionIds; }
        }

        public Texture2D lastTexture
        {
            get { return m_LastTexture; }
            set
            {
                if (value != m_LastTexture)
                {
                    m_LastMode = SkinningMode.Character;
                    m_LastSpriteId = String.Empty;
                    m_LastBoneSelectionIds.Clear();
                    m_VertexSelection.Clear();
                    m_SkeletonPreviewPose.Clear();
                }

                m_LastTexture = value;
            }
        }

        public SerializableDictionary<int, BonePose> lastPreviewPose
        {
            get { return m_SkeletonPreviewPose; }
        }

        public SkinningMode lastMode
        {
            get { return m_LastMode; }
            set { m_LastMode = value; }
        }

        public bool lastVisibilityToolActive
        {
            get { return m_VisibilityToolActive; }
            set { m_VisibilityToolActive = value; }
        }

        public IndexedSelection lastVertexSelection
        {
            get { return m_VertexSelection; }
        }

        public float lastBrushSize
        {
            get { return m_LastBrushSize; }
            set { m_LastBrushSize = value; }
        }

        public float lastBrushHardness
        {
            get { return m_LastBrushHardness; }
            set { m_LastBrushHardness = value; }
        }

        public float lastBrushStep
        {
            get { return m_LastBrushStep; } 
            set { m_LastBrushStep = value; }
        }
    }
}