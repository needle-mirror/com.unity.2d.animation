using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class BoneVisibilityToolData : CacheObject
    {
        [SerializeField]
        bool m_AllVisibility = true;
        bool m_PreviousVisibility = true;

        public bool allVisibility
        {
            get { return m_AllVisibility; }
            set { m_AllVisibility = value; }
        }

        public bool previousVisiblity
        {
            get { return m_PreviousVisibility; }
            set { m_PreviousVisibility = value; }
        }
    }
}
