using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine.Experimental.U2D.Animation
{
    public class SpriteLibraryComponent : MonoBehaviour
    {
        [SerializeField]
        private SpriteLibraryAsset m_SpriteLib;

        Dictionary<string, Dictionary<int, Sprite>> m_Overrides = new Dictionary<string, Dictionary<int, Sprite>>();

        public SpriteLibraryAsset spriteLib { set { m_SpriteLib = value; } }

        public Sprite GetSprite(string category, int index)
        {
            if (m_Overrides.ContainsKey(category) && m_Overrides[category].ContainsKey(index))
                return m_Overrides[category][index];
            return m_SpriteLib == null ? null : m_SpriteLib.GetSprite(category, index);
        }

        public Sprite GetSprite(int categoryHash, int index, ref string outCategoryname)
        {
            var category = GetCategoryNameFromHash(categoryHash);
            if (m_Overrides.ContainsKey(category) && m_Overrides[category].ContainsKey(index))
                return m_Overrides[category][index];
            return m_SpriteLib == null ? null : m_SpriteLib.GetSprite(categoryHash, index, ref outCategoryname);
        }

        public string GetCategoryNameFromHash(int categoryHash)
        {
            return m_SpriteLib == null ? "" : m_SpriteLib.GetCategoryNameFromHash(categoryHash);
        }

        private Dictionary<int, Sprite> GetCategoryOverrides(string category)
        {
            Dictionary<int, Sprite> entry;
            m_Overrides.TryGetValue(category, out entry);
            if (entry == null)
            {
                entry = new Dictionary<int, Sprite>();
                m_Overrides.Add(category, entry);
            }

            return entry;
        }

        private static void AddSpriteToOverrides(Dictionary<int, Sprite> overrides, int index, Sprite sprite)
        {
            if (overrides.ContainsKey(index))
                overrides[index] = sprite;
            else
                overrides.Add(index, sprite);
        }

        public void AddOverrides(SpriteLibraryAsset spriteLib, string category, int index)
        {
            var sprite = spriteLib.GetSprite(category, index);

            var entry = GetCategoryOverrides(category);
            AddSpriteToOverrides(entry, index, sprite);
        }

        public void AddOverrides(SpriteLibraryAsset spriteLib, string category)
        {
            var cat = spriteLib.entries.FirstOrDefault(x => x.category == category);
            if (cat != null)
            {
                var entry = GetCategoryOverrides(category);
                for (int i = 0; i < cat.spriteList.Count; ++i)
                {
                    AddSpriteToOverrides(entry, i, cat.spriteList[i]);
                }
            }
        }

        public void AddOverrides(Sprite sprite, string category, int index)
        {
            var entry = GetCategoryOverrides(category);
            AddSpriteToOverrides(entry, index, sprite);
        }

        public void RemoveOverrides(string category)
        {
            m_Overrides.Remove(category);
        }

        public void RemoveOverrides(string category, int index)
        {
            var entry = GetCategoryOverrides(category);
            entry.Remove(index);
        }

        public List<LibEntry> entries
        {
            get { return m_SpriteLib != null ? m_SpriteLib.entries : new List<LibEntry>(); }
        }
    }
}
