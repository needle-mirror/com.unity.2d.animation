using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace UnityEngine.Experimental.U2D.Animation
{
    [Serializable]
    public class LibEntry
    {
        [SerializeField]
        public string category;
        [SerializeField]
        public int categoryHash;
        [SerializeField]
        public List<Sprite> spriteList;

        public void GenerateHash()
        {
            categoryHash = SpriteLibraryAsset.GetCategoryHash(category);
        }
    }

    [CreateAssetMenu(order = 350)]
    public class SpriteLibraryAsset : ScriptableObject
    {
        [SerializeField]
        private List<LibEntry> m_Entries = new List<LibEntry>();

        internal List<LibEntry> entries { get { return m_Entries; } set { m_Entries = value; } }

        // Use this to get only the main sprite (for common use case)
        public Sprite GetSprite(string category, int index)
        {
            try
            {
                var entry = m_Entries.FirstOrDefault(x => x.category == category);
                if (entry != null && index >= 0 && index < entry.spriteList.Count)
                    return entry.spriteList[index];
            }
            catch{}

            return null;
        }

        public Sprite GetSprite(int categoryHash, int index, ref string outCategoryname)
        {
            try
            {
                var entry = m_Entries.FirstOrDefault(x => x.categoryHash == categoryHash);
                if (entry != null && index >= 0 && index < entry.spriteList.Count)
                {
                    if (outCategoryname != null)
                        outCategoryname = entry.category;
                    return entry.spriteList[index];
                }
            }
            catch {}

            return null;
        }

        public string GetCategoryNameFromHash(int hash)
        {
            var entry = m_Entries.FirstOrDefault(x => x.categoryHash == hash);
            return entry == null ? "" : entry.category;
        }

        public void UpdateHashes()
        {
            foreach (var p in m_Entries)
                p.GenerateHash();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public static int GetCategoryHash(string name)
        {
            return Animator.StringToHash(name);
        }
    }
}
