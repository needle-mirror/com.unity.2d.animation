namespace UnityEngine.Experimental.U2D.Animation
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteResolver : MonoBehaviour
    {
        [SerializeField]
        private int m_Index;
        private int m_PreviousIndex;
        [SerializeField]
        private string m_Category;
        [SerializeField]
        private int m_CategoryHash;
        private int m_PreviousCategoryHash;

#if UNITY_EDITOR
        bool m_SpriteLibChanged;
#endif

        void Awake()
        {
            RefreshSpriteFromSpriteKey();
            m_PreviousIndex = m_Index;
            m_PreviousCategoryHash = m_CategoryHash;
        }

        // TODO : This function need to change to support multiple sprite retrival and using one as the main one.
        public void SetSpriteIndex(int key)
        {
            // Do not set if sprite key is not defined.
            if (key == -1)
                return;

            var sprite = GetSpriteByIndex(key);
            if (sprite != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
            }
        }

        void LateUpdate()
        {
            if (m_Index != m_PreviousIndex || m_CategoryHash != m_PreviousCategoryHash)
            {
                m_PreviousCategoryHash = m_CategoryHash;
                m_PreviousIndex = m_Index;
                RefreshSpriteFromSpriteKey();
            }
        }

        internal string spriteCategory
        {
            set
            {
                m_Category = value;
                m_CategoryHash = SpriteLibraryAsset.GetCategoryHash(m_Category);
            }
        }

        internal int spriteIndex { set { m_Index = value; } }

        public Sprite GetSpriteByIndex(int index)
        {
            var parentLibs = GetComponentsInParent<SpriteLibraryComponent>();
            foreach (var lib in parentLibs)
            {
                var sprite = lib.GetSprite(m_CategoryHash, index, ref m_Category);
                if (sprite != null)
                    return sprite;
            }
            return null;
        }

        public void RefreshSpriteFromSpriteKey()
        {
            SetSpriteIndex(m_Index);
        }

#if UNITY_EDITOR
        internal bool spriteLibChanged
        {
            get {return m_SpriteLibChanged;}
            set { m_SpriteLibChanged = value; }
        }
#endif
    }
}
