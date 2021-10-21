using System.Collections.Generic;

namespace UnityEngine.U2D.Animation
{
    internal class SpriteLibrarySourceAsset : ScriptableObject
    {
        [SerializeField]
        List<SpriteLibCategoryOverride> m_Library = new List<SpriteLibCategoryOverride>();
        [SerializeField]
        string m_PrimaryLibraryGUID;
        [SerializeField]
        long m_ModificationHash;
        [SerializeField]
        int m_Version = 1;

        public IReadOnlyList<SpriteLibCategoryOverride> library => m_Library;

        public string primaryLibraryID
        {
            get => m_PrimaryLibraryGUID;
            set
            {
                m_PrimaryLibraryGUID = value;
                UpdateModificationHash();
            }
        }

        public long modificationHash => m_ModificationHash;
        
        public int version => m_Version;

        public void Copy(SpriteLibrarySourceAsset source)
        {
            m_Library.Clear();
            m_Library.AddRange(source.m_Library);
            m_PrimaryLibraryGUID = source.m_PrimaryLibraryGUID;
            UpdateModificationHash();
        }

        public void SetLibrary(IList<SpriteLibCategoryOverride> newLibrary)
        {
            if (!m_Library.Equals(newLibrary))
            {
                m_Library = (List<SpriteLibCategoryOverride>)newLibrary;
                UpdateModificationHash();
            }
        }

        public void AddCategory(SpriteLibCategoryOverride newCategory)
        {
            if (!m_Library.Contains(newCategory))
            {
                m_Library.Add(newCategory);
                UpdateModificationHash();
            }
        }

        public void RemoveCategory(SpriteLibCategoryOverride categoryToRemove)
        {
            if (m_Library.Contains(categoryToRemove))
            {
                m_Library.Remove(categoryToRemove);
                UpdateModificationHash();
            }
        }
        
        public void RemoveCategory(int indexToRemove)
        {
            if (indexToRemove >= 0 && indexToRemove < m_Library.Count)
            {
                m_Library.RemoveAt(indexToRemove);
                UpdateModificationHash();
            }
        }

        void UpdateModificationHash()
        {
            var hash = System.DateTime.Now.Ticks;
            m_ModificationHash = hash;
        }        
    }
}