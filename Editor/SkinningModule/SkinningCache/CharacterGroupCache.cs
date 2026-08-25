using System;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    [Serializable]
    internal class CharacterGroupCache : SkinningObject, ICharacterOrder
    {
        [SerializeField]
        public int parentGroup;
        [SerializeField]
        bool m_IsVisible = true;
        [SerializeField]
        int m_Order = -1;

        public bool isVisible
        {
            get => m_IsVisible;
            set
            {
                m_IsVisible = value;
                skinningCache.GroupVisibilityChanged(this);
            }
        }

        public virtual int order
        {
            get => m_Order;
            set => m_Order = value;
        }
    }
}
