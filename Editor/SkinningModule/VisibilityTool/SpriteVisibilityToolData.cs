using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal class SpriteVisibilityToolData : CacheObject
    {
        [SerializeField]
        bool m_AllVisibility = true;
        bool m_PreviousVisibility = true;

        public bool allVisibility
        {
            get { return m_AllVisibility; }
            set { m_PreviousVisibility = m_AllVisibility = value; }
        }

        public bool previousVisibility
        {
            get { return m_PreviousVisibility; }
            set { m_PreviousVisibility = value; }
        }
    }
}
