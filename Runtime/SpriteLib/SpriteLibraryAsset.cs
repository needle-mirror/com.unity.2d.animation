using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.Experimental.U2D.Animation
{
    [Serializable]
    internal class Categorylabel
    {
        [SerializeField]
        string m_Name;
        [SerializeField]
        [HideInInspector]
        int m_Hash;
        [SerializeField]
        Sprite m_Sprite;

        public string name
        {
            get { return m_Name; }
            set
            {
                m_Name = value;
                m_Hash = SpriteLibraryAsset.GetStringHash(m_Name);
            }
        }
        public int hash { get { return m_Hash; } }
        public Sprite sprite {get { return m_Sprite; } set { m_Sprite = value; }}
        public void UpdateHash()
        {
            m_Hash = SpriteLibraryAsset.GetStringHash(m_Name);
        }
    }

    [Serializable]
    internal class SpriteLibCategory
    {
        [SerializeField]
        string m_Name;
        [SerializeField]
        int m_Hash;
        [SerializeField]
        List<Categorylabel> m_CategoryList;

        public string name
        {
            get { return m_Name; }
            set
            {
                m_Name = value;
                m_Hash = SpriteLibraryAsset.GetStringHash(m_Name);
            }
        }

        public int hash { get { return m_Hash; } }

        public List<Categorylabel> categoryList
        {
            get { return m_CategoryList; }
            set { m_CategoryList = value; }
        }

        public void UpdateHash()
        {
            m_Hash = SpriteLibraryAsset.GetStringHash(m_Name);
            foreach (var s in m_CategoryList)
                s.UpdateHash();
        }
    }

    /// <summary>
    /// A custom Asset that stores Sprites grouping
    /// </summary>
    /// <Description>
    /// Sprites are grouped under a given category as labels. Each category and label needs to have
    /// a name specified so that it can be queried.
    /// </Description>
    [CreateAssetMenu(order = 350)]
    public class SpriteLibraryAsset : ScriptableObject
    {
        [SerializeField]
        private List<SpriteLibCategory> m_Labels = new List<SpriteLibCategory>();

        internal List<SpriteLibCategory> labels { get { return m_Labels; } set { m_Labels = value; } }

        internal Sprite GetSprite(int categoryHash, int labelHash)
        {
            var category = m_Labels.FirstOrDefault(x => x.hash == categoryHash);
            if (category != null)
            {
                var spritelabel = category.categoryList.FirstOrDefault(x => x.hash == labelHash);
                if (spritelabel != null)
                {
                    return spritelabel.sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the Sprite registered in the Asset given the Category and Label value
        /// </summary>
        /// <param name="category">Category string value</param>
        /// <param name="label">Label string value</param>
        /// <returns></returns>
        public Sprite GetSprite(string category, string label)
        {
            var categoryHash = SpriteLibraryAsset.GetStringHash(category);
            var labelHash = SpriteLibraryAsset.GetStringHash(label);
            return GetSprite(categoryHash, labelHash);
        }

        /// <summary>
        /// Return all the Category names of the Sprite Library Asset that is associated.
        /// </summary>
        /// <returns>A Enumerable string value representing the name</returns>
        public IEnumerable<string> GetCategoryNames()
        {
            return m_Labels.Select(x => x.name);
        }

        /// <summary>
        /// Returns the labels' name for the given name
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>A Enumerable string representing labels' name</returns>
        public IEnumerable<string> GetCategorylabelNames(string category)
        {
            var label = m_Labels.FirstOrDefault(x => x.name == category);
            return label == null ? new string[0] : label.categoryList.Select(x => x.name);
        }

        internal IEnumerable<string> GetCategorylabelNames(int category)
        {
            var label = m_Labels.FirstOrDefault(x => x.hash == category);
            return label == null ? new string[0] : label.categoryList.Select(x => x.name);
        }

        internal string GetCategoryNameFromHash(int hash)
        {
            var label = m_Labels.FirstOrDefault(x => x.hash == hash);
            return label == null ? "" : label.name;
        }

        /// <summary>
        /// Add or replace and existing Sprite into the given Category and Label
        /// </summary>
        /// <param name="sprite">Sprite to add</param>
        /// <param name="category">Category to add the Sprite to</param>
        /// <param name="label">Label of the Category to add the Sprite to</param>
        public void AddCategoryLabel(Sprite sprite, string category, string label)
        {
            category = category.Trim();
            label = label.Trim();
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(label))
            {
                Debug.LogError("Cannot add label with empty or null Category or label string");
            }
            var catHash = SpriteLibraryAsset.GetStringHash(category);
            Categorylabel categorylabel = null;
            SpriteLibCategory libCategory = null;
            libCategory = m_Labels.FirstOrDefault(x => x.hash == catHash);
            
            if (libCategory != null)
            {
                var labelHash = SpriteLibraryAsset.GetStringHash(label);
                categorylabel = libCategory.categoryList.FirstOrDefault(y => y.hash == labelHash);
                if (categorylabel != null)
                    categorylabel.sprite = sprite;
                else
                {
                    categorylabel = new Categorylabel()
                    {
                        name = label,
                        sprite = sprite
                    };
                    libCategory.categoryList.Add(categorylabel);
                }
            }
            else
            {
                var slc = new SpriteLibCategory()
                {
                    categoryList = new List<Categorylabel>()
                    {
                        new Categorylabel()
                        {
                            name = label,
                            sprite = sprite
                        }
                    },
                    name = category
                };
                m_Labels.Add(slc);
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Remove a Label from a given Category
        /// </summary>
        /// <param name="category">Category to remove from</param>
        /// <param name="label">Label to remove</param>
        /// <param name="deleteCategory">Indicate to remove the Category if it is empty</param>
        public void RemoveCategoryLabel(string category, string label, bool deleteCategory)
        {
            var catHash = SpriteLibraryAsset.GetStringHash(category);
            SpriteLibCategory libCategory = null;
            libCategory = m_Labels.FirstOrDefault(x => x.hash == catHash);

            if (libCategory != null)
            {
                var labelHash = SpriteLibraryAsset.GetStringHash(label);
                libCategory.categoryList.RemoveAll(x => x.hash == labelHash);
                if (deleteCategory && libCategory.categoryList.Count == 0)
                    m_Labels.RemoveAll(x => x.hash == libCategory.hash);
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        internal string GetLabelNameFromHash(int categoryHas, int labelHash)
        {
            var labels = m_Labels.FirstOrDefault(x => x.hash == categoryHas);
            if (labels != null)
            {
                var label = labels.categoryList.FirstOrDefault(x => x.hash == labelHash);
                return label == null ? "" : label.name;
            }
            return "";
        }

        internal void UpdateHashes()
        {
            foreach (var e in m_Labels)
                e.UpdateHash();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        internal static int GetStringHash(string value)
        {
            return Animator.StringToHash(value);
        }
    }
}
