using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Common;

namespace UnityEngine.U2D.Animation
{
    /// <summary>
    /// Component that holds a Sprite Library Asset. The component is used by SpriteResolver Component to query for Sprite based on Category and Index.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(U2DAnimationConstants.PackageDisplayName + "/Sprite Library")]
    [IconAttribute(IconUtility.IconPath + "Animation.SpriteLibrary.asset")]
    [MovedFrom("UnityEngine.Experimental.U2D.Animation")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.animation@18.0/manual/SL-component.html")]
    public class SpriteLibrary : MonoBehaviour, IPreviewable
    {
        struct CategoryEntrySprite
        {
            public string category;
            public string entry;
            public Sprite sprite;
        }

        [SerializeField]
        List<SpriteLibCategory> m_Library = new List<SpriteLibCategory>();

        [SerializeField]
        SpriteLibraryAsset m_SpriteLibraryAsset;

        // Cache for combining data in sprite library asset and main library
        List<CategoryEntrySprite> m_CategoryEntries = null;
        Dictionary<int, int> m_CategoryEntryHashToIndex = null;
        Dictionary<int, int> m_LegacyHashToIndex = null;
        Dictionary<string, HashSet<string>> m_CategoryEntryCache = null;
        EntityId m_PreviousSpriteLibraryAsset;
        long m_PreviousModificationHash;

        /// <summary>Get or Set the current SpriteLibraryAsset to use.</summary>
        public SpriteLibraryAsset spriteLibraryAsset
        {
            set
            {
                if (m_SpriteLibraryAsset != value)
                {
                    m_SpriteLibraryAsset = value;
                    CacheOverrides();
                    RefreshSpriteResolvers();
                }
            }
            get { return m_SpriteLibraryAsset; }
        }

        void OnEnable()
        {
            CacheOverrides();
        }

#if UNITY_EDITOR
        // Undo, prefab revert and SerializedObject edits change m_Library without going through CacheOverrides.
        void OnValidate()
        {
            m_CategoryEntryCache = null;
        }
#endif

        /// <summary>
        /// Empty method. Implemented for the IPreviewable interface.
        /// </summary>
        public void OnPreviewUpdate() { }

        /// <summary>
        /// Return the Sprite that is registered for the given Category and Label for the SpriteLibrary.
        /// </summary>
        /// <param name="category">Category name.</param>
        /// <param name="label">Label name.</param>
        /// <returns>Sprite associated to the name and index.</returns>
        public Sprite GetSprite(string category, string label)
        {
            UpdateCacheOverridesIfNeeded();
            int index = FindEntryIndex(GetHashForCategoryAndEntry(category, label), category, label);
            return index >= 0 ? m_CategoryEntries[index].sprite : null;
        }

        int FindEntryIndex(int hash, string category, string entry)
        {
            if (!m_CategoryEntryHashToIndex.TryGetValue(hash, out int index))
                return -1;
            if (IsEntry(index, category, entry))
                return index;

            for (int i = 0; i < m_CategoryEntries.Count; ++i)
            {
                if (IsEntry(i, category, entry))
                    return i;
            }

            return -1;
        }

        bool IsEntry(int index, string category, string entry)
        {
            return m_CategoryEntries[index].category == category && m_CategoryEntries[index].entry == entry;
        }

        // Current keys are looked up before legacy keys, so that a legacy key never shadows a current one.
        bool TryGetEntryIndex(int hash, out int index)
        {
            if (m_CategoryEntryHashToIndex.TryGetValue(hash, out index))
                return true;
            return TryGetLegacyEntryIndex(hash, out index);
        }

        bool TryGetLegacyEntryIndex(int hash, out int index)
        {
            index = -1;
            if (hash == 0)
                return false;

            if (m_LegacyHashToIndex == null)
            {
                m_LegacyHashToIndex = new Dictionary<int, int>(m_CategoryEntries.Count);
                for (int i = 0; i < m_CategoryEntries.Count; ++i)
                    m_LegacyHashToIndex.TryAdd(GetLegacyHashForCategoryAndEntry(m_CategoryEntries[i].category, m_CategoryEntries[i].entry), i);
            }

            return m_LegacyHashToIndex.TryGetValue(hash, out index);
        }

        internal Sprite GetSpriteFromLegacyHash(int hash, out bool validEntry)
        {
            UpdateCacheOverridesIfNeeded();
            validEntry = TryGetLegacyEntryIndex(hash, out int index);
            return validEntry ? m_CategoryEntries[index].sprite : null;
        }

        internal bool GetCategoryAndEntryNameFromLegacyHash(int hash, out string category, out string entry)
        {
            UpdateCacheOverridesIfNeeded();
            if (TryGetLegacyEntryIndex(hash, out int index))
            {
                category = m_CategoryEntries[index].category;
                entry = m_CategoryEntries[index].entry;
                return true;
            }

            category = null;
            entry = null;
            return false;
        }

        void UpdateCacheOverridesIfNeeded()
        {
            EntityId spriteLibraryAsset = m_SpriteLibraryAsset ? m_SpriteLibraryAsset.GetEntityId() : EntityId.None;
            long modificationHash = m_SpriteLibraryAsset ? m_SpriteLibraryAsset.modificationHash : 0;
            if (m_CategoryEntryCache == null ||
                m_PreviousSpriteLibraryAsset != spriteLibraryAsset ||
                m_PreviousModificationHash != modificationHash)
                CacheOverrides();
        }

        internal bool GetCategoryAndEntryNameFromHash(int hash, out string category, out string entry)
        {
            UpdateCacheOverridesIfNeeded();
            if (TryGetEntryIndex(hash, out int index))
            {
                category = m_CategoryEntries[index].category;
                entry = m_CategoryEntries[index].entry;
                return true;
            }

            category = null;
            entry = null;
            return false;
        }

        internal static int GetHashForCategoryAndEntry(string category, string entry)
        {
            return GetHashForCategoryAndEntry(SpriteLibraryUtility.GetStringHash(category), SpriteLibraryUtility.GetStringHash(entry));
        }

        internal static int GetHashForCategoryAndEntry(int categoryHash, int entryHash)
        {
            return SpriteLibraryUtility.CombineHashes(categoryHash, entryHash);
        }

        internal static int GetLegacyHashForCategoryAndEntry(string category, string entry)
        {
            return SpriteLibraryUtility.GetStringHash($"{category}_{entry}");
        }

        internal Sprite GetSpriteFromCategoryAndEntryHash(int hash, out bool validEntry)
        {
            UpdateCacheOverridesIfNeeded();
            if (TryGetEntryIndex(hash, out int index))
            {
                validEntry = true;
                return m_CategoryEntries[index].sprite;
            }

            validEntry = false;
            return null;
        }

        List<SpriteCategoryEntry> GetEntries(string category, bool addIfNotExist)
        {
            int index = m_Library.FindIndex(x => x.name == category);
            if (index < 0)
            {
                if (!addIfNotExist)
                    return null;
                m_Library.Add(new SpriteLibCategory()
                {
                    name = category,
                    categoryList = new List<SpriteCategoryEntry>()
                });
                index = m_Library.Count - 1;
            }

            return m_Library[index].categoryList;
        }

        static SpriteCategoryEntry GetEntry(List<SpriteCategoryEntry> entries, string entry, bool addIfNotExist)
        {
            int index = entries.FindIndex(x => x.name == entry);
            if (index < 0)
            {
                if (!addIfNotExist)
                    return null;
                entries.Add(new SpriteCategoryEntry()
                {
                    name = entry,
                });
                index = entries.Count - 1;
            }

            return entries[index];
        }

        /// <summary>
        /// Add or replace an override when querying for the given Category and Label from a SpriteLibraryAsset.
        /// </summary>
        /// <param name="spriteLib">Sprite Library Asset to query.</param>
        /// <param name="category">Category name from the Sprite Library Asset to add override.</param>
        /// <param name="label">Label name to add override.</param>
        public void AddOverride(SpriteLibraryAsset spriteLib, string category, string label)
        {
            Sprite sprite = spriteLib.GetSprite(category, label);
            List<SpriteCategoryEntry> entries = GetEntries(category, true);
            SpriteCategoryEntry entry = GetEntry(entries, label, true);
            entry.sprite = sprite;
            CacheOverrides();
        }

        /// <summary>
        /// Add or replace an override when querying for the given Category. All the categories in the Category will be added.
        /// </summary>
        /// <param name="spriteLib">Sprite Library Asset to query.</param>
        /// <param name="category">Category name from the Sprite Library Asset to add override.</param>
        public void AddOverride(SpriteLibraryAsset spriteLib, string category)
        {
            int categoryHash = SpriteLibraryUtility.GetStringHash(category);
            SpriteLibCategory cat = spriteLib.categories.FirstOrDefault(x => x.hash == categoryHash);
            if (cat != null)
            {
                List<SpriteCategoryEntry> entries = GetEntries(category, true);
                for (int i = 0; i < cat.categoryList.Count; ++i)
                {
                    SpriteCategoryEntry ent = cat.categoryList[i];
                    GetEntry(entries, ent.name, true).sprite = ent.sprite;
                }

                CacheOverrides();
            }
        }

        /// <summary>
        /// Add or replace an override when querying for the given Category and Label.
        /// </summary>
        /// <param name="sprite">Sprite to override to.</param>
        /// <param name="category">Category name to override.</param>
        /// <param name="label">Label name to override.</param>
        public void AddOverride(Sprite sprite, string category, string label)
        {
            GetEntry(GetEntries(category, true), label, true).sprite = sprite;
            CacheOverrides();
            RefreshSpriteResolvers();
        }

        /// <summary>
        /// Remove all Sprite Library override for a given category.
        /// </summary>
        /// <param name="category">Category overrides to remove.</param>
        public void RemoveOverride(string category)
        {
            int index = m_Library.FindIndex(x => x.name == category);
            if (index >= 0)
            {
                m_Library.RemoveAt(index);
                CacheOverrides();
                RefreshSpriteResolvers();
            }
        }

        /// <summary>
        /// Remove Sprite Library override for a given category and label.
        /// </summary>
        /// <param name="category">Category to remove.</param>
        /// <param name="label">Label to remove.</param>
        public void RemoveOverride(string category, string label)
        {
            List<SpriteCategoryEntry> entries = GetEntries(category, false);
            if (entries != null)
            {
                int index = entries.FindIndex(x => x.name == label);
                if (index >= 0)
                {
                    entries.RemoveAt(index);
                    CacheOverrides();
                    RefreshSpriteResolvers();
                }
            }
        }

        /// <summary>
        /// Method to check if a Category and Label pair has an override.
        /// </summary>
        /// <param name="category">Category name.</param>
        /// <param name="label">Label name.</param>
        /// <returns>True if override exist, false otherwise.</returns>
        public bool HasOverride(string category, string label)
        {
            List<SpriteCategoryEntry> catOverride = GetEntries(category, false);
            if (catOverride != null)
                return GetEntry(catOverride, label, false) != null;
            return false;
        }

        /// <summary>
        /// Request SpriteResolver components that are in the same hierarchy to refresh.
        /// </summary>
        public void RefreshSpriteResolvers()
        {
            SpriteResolver[] spriteResolvers = GetComponentsInChildren<SpriteResolver>();
            foreach (SpriteResolver sr in spriteResolvers)
            {
                sr.ResolveSpriteToSpriteRenderer();
#if UNITY_EDITOR
                sr.spriteLibChanged = true;
#endif
            }
        }

        internal IEnumerable<string> categoryNames
        {
            get
            {
                UpdateCacheOverridesIfNeeded();
                return m_CategoryEntryCache.Keys;
            }
        }

        internal IEnumerable<string> GetEntryNames(string category)
        {
            UpdateCacheOverridesIfNeeded();
            if (m_CategoryEntryCache.ContainsKey(category))
                return m_CategoryEntryCache[category];
            return null;
        }

        internal void CacheOverrides()
        {
            m_PreviousSpriteLibraryAsset = EntityId.None;
            m_PreviousModificationHash = 0;
            int capacity = 0;
            if (m_SpriteLibraryAsset)
            {
                foreach (SpriteLibCategory category in m_SpriteLibraryAsset.categories)
                    capacity += category.categoryList.Count;
            }

            m_CategoryEntries = new List<CategoryEntrySprite>(capacity);
            m_CategoryEntryHashToIndex = new Dictionary<int, int>(capacity);
            m_LegacyHashToIndex = null;
            m_CategoryEntryCache = new Dictionary<string, HashSet<string>>();
            if (m_SpriteLibraryAsset)
            {
                m_PreviousSpriteLibraryAsset = m_SpriteLibraryAsset.GetEntityId();
                m_PreviousModificationHash = m_SpriteLibraryAsset.modificationHash;
                foreach (SpriteLibCategory category in m_SpriteLibraryAsset.categories)
                {
                    string catName = category.name;
                    int categoryHash = SpriteLibraryUtility.GetStringHash(catName);
                    m_CategoryEntryCache.Add(catName, new HashSet<string>());
                    HashSet<string> cacheEntryName = m_CategoryEntryCache[catName];
                    foreach (SpriteCategoryEntry entry in category.categoryList)
                    {
                        AddEntry(GetHashForCategoryAndEntry(categoryHash, SpriteLibraryUtility.GetStringHash(entry.name)), catName, entry.name, entry.sprite);
                        cacheEntryName.Add(entry.name);
                    }
                }
            }

            foreach (SpriteLibCategory category in m_Library)
            {
                string catName = category.name;
                int categoryHash = SpriteLibraryUtility.GetStringHash(catName);
                if (!m_CategoryEntryCache.ContainsKey(catName))
                    m_CategoryEntryCache.Add(catName, new HashSet<string>());
                HashSet<string> cacheEntryName = m_CategoryEntryCache[catName];

                foreach (SpriteCategoryEntry ent in category.categoryList)
                {
                    cacheEntryName.Add(ent.name);
                    int hash = GetHashForCategoryAndEntry(categoryHash, SpriteLibraryUtility.GetStringHash(ent.name));
                    int index = FindEntryIndex(hash, catName, ent.name);
                    if (index >= 0)
                    {
                        CategoryEntrySprite e = m_CategoryEntries[index];
                        e.sprite = ent.sprite;
                        m_CategoryEntries[index] = e;
                    }
                    else
                        AddEntry(hash, catName, ent.name, ent.sprite);
                }
            }
        }

        void AddEntry(int hash, string category, string entry, Sprite sprite)
        {
            m_CategoryEntryHashToIndex.TryAdd(hash, m_CategoryEntries.Count);
            m_CategoryEntries.Add(new CategoryEntrySprite()
            {
                category = category,
                entry = entry,
                sprite = sprite
            });
        }
    }
}
