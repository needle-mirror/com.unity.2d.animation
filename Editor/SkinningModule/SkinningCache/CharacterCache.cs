using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    // The part of a Sprite the data provider did not load, such as a Sprite whose PSD layer is hidden or unchecked.
    // The Skinning Editor does not show it, but keeps its bones so that Apply writes it as it writes the loaded parts.
    // PSD Importer looks this type up by name to know that the Skinning Editor can take such parts.
    [Serializable]
    internal class UnloadedCharacterPart
    {
        public CharacterPart part;
        public List<BoneCache> bones = new List<BoneCache>();
    }

    internal class CharacterCache : SkinningObject, IEnumerable<CharacterPartCache>
    {
        [SerializeField]
        SkeletonCache m_Skeleton;
        [SerializeField]
        List<CharacterPartCache> m_Parts = new List<CharacterPartCache>();
        [SerializeField]
        Vector2Int m_Dimension;
        [SerializeField]
        List<CharacterGroupCache> m_Groups = new List<CharacterGroupCache>();
        [SerializeField]
        Vector2 m_Pivot;
        [SerializeField]
        List<UnloadedCharacterPart> m_UnloadedParts = new List<UnloadedCharacterPart>();

        public SkeletonCache skeleton
        {
            get => m_Skeleton;
            set => m_Skeleton = value;
        }

        public virtual CharacterPartCache[] parts
        {
            get => m_Parts.ToArray();
            set => m_Parts = new List<CharacterPartCache>(value);
        }

        public virtual CharacterGroupCache[] groups
        {
            get => m_Groups.ToArray();
            set => m_Groups = new List<CharacterGroupCache>(value);
        }

        public UnloadedCharacterPart[] unloadedParts
        {
            get => m_UnloadedParts.ToArray();
            set => m_UnloadedParts = new List<UnloadedCharacterPart>(value);
        }

        public Vector2Int dimension
        {
            get => m_Dimension;
            set => m_Dimension = value;
        }

        public Vector2 pivot
        {
            get => m_Pivot;
            set => m_Pivot = value;
        }

        public IEnumerator<CharacterPartCache> GetEnumerator()
        {
            return ((IEnumerable<CharacterPartCache>)m_Parts).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<CharacterPartCache>)m_Parts).GetEnumerator();
        }
    }
}
