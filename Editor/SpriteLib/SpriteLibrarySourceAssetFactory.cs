using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// Represents a Sprite Library's label.
    /// </summary>
    [Serializable]
    public class SpriteLibraryLabel : ISpriteLibraryLabel
    {
        /// <summary>
        /// Label's name.
        /// </summary>
        public string name => m_Name;

        /// <summary>
        /// Sprite associated with the label.
        /// </summary>
        public Sprite sprite => m_Sprite;

        [SerializeField]
        string m_Name;

        [SerializeField]
        Sprite m_Sprite;

        /// <summary>
        /// Constructs a new label.
        /// </summary>
        /// <param name="labelName">Label's name.</param>
        /// <param name="labelSprite">Label's Sprite.</param>
        public SpriteLibraryLabel(string labelName, Sprite labelSprite)
        {
            m_Name = labelName;
            m_Sprite = labelSprite;
        }
    }

    /// <summary>
    /// Represents a Sprite Library's category.
    /// </summary>
    [Serializable]
    public class SpriteLibraryCategory : ISpriteLibraryCategory
    {
        /// <summary>
        /// Category's name.
        /// </summary>
        public string name => m_Name;

        /// <summary>
        /// List of labels in category.
        /// </summary>
        public IEnumerable<ISpriteLibraryLabel> labels => m_Labels;

        [SerializeField]
        List<SpriteLibraryLabel> m_Labels;

        [SerializeField]
        string m_Name;

        /// <summary>
        /// Constructs a new category.
        /// </summary>
        /// <param name="categoryName">Category's name.</param>
        /// <param name="categoryLabels">Collection of labels in a category.</param>
        public SpriteLibraryCategory(string categoryName, IEnumerable<SpriteLibraryLabel> categoryLabels)
        {
            m_Name = categoryName;
            m_Labels = new List<SpriteLibraryLabel>(categoryLabels);
        }
    }

    /// <summary>
    /// Class used for creating new Sprite Library Source Assets.
    /// </summary>
    public static class SpriteLibrarySourceAssetFactory
    {
        /// <summary>
        /// Sprite Library Source Asset's extension.
        /// </summary>
        public const string extension = SpriteLibrarySourceAsset.extension;

        /// <summary>
        /// Creates a new Sprite Library Source Asset at a given path.
        /// </summary>
        /// <param name="path">Save path. Must be within the Assets folder.</param>
        /// <param name="categories">Collection of categories in the library.</param>
        /// <param name="mainLibraryPath">A path to the main library. Null if there is no main library.</param>
        /// <returns>A relative path to the Project with correct extension.</returns>
        /// <exception cref="InvalidOperationException">Throws when the save path is invalid/</exception>
        public static string Create(string path, IEnumerable<ISpriteLibraryCategory> categories, string mainLibraryPath = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("Save path cannot be null or empty.");

            string relativePath = GetRelativePath(path);
            if (string.IsNullOrEmpty(relativePath))
                throw new InvalidOperationException($"{nameof(LoadSpriteLibrarySourceAsset)} can only be saved in the Assets folder.");

            relativePath = Path.ChangeExtension(relativePath, extension);

            SpriteLibraryAsset mainLibrary = null;
            if (!string.IsNullOrEmpty(mainLibraryPath))
            {
                mainLibrary = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(mainLibraryPath);
                if (mainLibrary == null)
                    throw new InvalidOperationException($"No {nameof(SpriteLibraryAsset)} found at path: '{mainLibraryPath}'");
            }

            SpriteLibrarySourceAsset asset = ScriptableObject.CreateInstance<SpriteLibrarySourceAsset>();
            List<SpriteLibCategoryOverride> categoryList = new List<SpriteLibCategoryOverride>();
            if (categories != null)
            {
                foreach (ISpriteLibraryCategory category in categories)
                {
                    SpriteLibCategoryOverride spriteLibCategory = new SpriteLibCategoryOverride
                    {
                        name = category.name,
                        overrideEntries = new List<SpriteCategoryEntryOverride>()
                    };
                    foreach (ISpriteLibraryLabel label in category.labels)
                    {
                        SpriteCategoryEntryOverride spriteCategoryEntryOverride = new SpriteCategoryEntryOverride
                        {
                            name = label.name,
                            spriteOverride = label.sprite
                        };
                        spriteLibCategory.overrideEntries.Add(spriteCategoryEntryOverride);
                    }

                    categoryList.Add(spriteLibCategory);
                }
            }

            if (mainLibrary != null)
            {
                asset.SetPrimaryLibraryGUID(AssetDatabase.GUIDFromAssetPath(mainLibraryPath).ToString());

                List<SpriteLibCategory> newCategories = mainLibrary.categories ?? new List<SpriteLibCategory>();

                List<SpriteLibCategoryOverride> existingCategories = new List<SpriteLibCategoryOverride>(categoryList);
                categoryList.Clear();

                // populate new primary
                foreach (SpriteLibCategory newCategory in newCategories)
                {
                    List<SpriteCategoryEntryOverride> labels = new List<SpriteCategoryEntryOverride>();
                    SpriteLibCategoryOverride existingCategory = null;
                    for (int i = 0; i < existingCategories.Count; i++)
                    {
                        SpriteLibCategoryOverride category = existingCategories[i];
                        if (category.name == newCategory.name)
                        {
                            existingCategory = category;
                            existingCategory.fromMain = true;
                            existingCategories.RemoveAt(i);
                            break;
                        }
                    }

                    List<SpriteCategoryEntry> newEntries = newCategory.categoryList;
                    foreach (SpriteCategoryEntry newEntry in newEntries)
                    {
                        Sprite sprite = newEntry.sprite;

                        labels.Add(new SpriteCategoryEntryOverride
                        {
                            name = newEntry.name,
                            sprite = sprite,
                            spriteOverride = sprite,
                            fromMain = true
                        });
                    }

                    int overrideCount = 0;
                    if (existingCategory != null)
                    {
                        foreach (SpriteCategoryEntryOverride existingLabel in existingCategory.overrideEntries)
                        {
                            bool foundLabel = false;
                            foreach (SpriteCategoryEntryOverride newLabel in labels)
                            {
                                if (existingLabel.name == newLabel.name)
                                {
                                    if (newLabel.spriteOverride != existingLabel.spriteOverride)
                                    {
                                        newLabel.spriteOverride = existingLabel.spriteOverride;
                                        overrideCount++;
                                    }

                                    foundLabel = true;
                                    break;
                                }
                            }

                            if (!foundLabel)
                            {
                                overrideCount++;
                                labels.Add(new SpriteCategoryEntryOverride
                                {
                                    name = existingLabel.name,
                                    sprite = existingLabel.sprite,
                                    spriteOverride = existingLabel.spriteOverride,
                                    fromMain = false
                                });
                            }
                        }
                    }

                    categoryList.Add(new SpriteLibCategoryOverride
                    {
                        name = newCategory.name,
                        overrideEntries = labels,
                        fromMain = true,
                        entryOverrideCount = overrideCount
                    });
                }

                foreach (SpriteLibCategoryOverride existingCategory in existingCategories)
                {
                    bool keepCategory = false;
                    if (existingCategory.fromMain)
                    {
                        for (int i = existingCategory.overrideEntries.Count; i-- > 0;)
                        {
                            SpriteCategoryEntryOverride entry = existingCategory.overrideEntries[i];
                            if (!entry.fromMain || entry.sprite != entry.spriteOverride)
                            {
                                entry.fromMain = false;
                                entry.sprite = entry.spriteOverride;
                                keepCategory = true;
                            }
                            else
                                existingCategory.overrideEntries.RemoveAt(i);
                        }
                    }

                    if (!existingCategory.fromMain || keepCategory)
                    {
                        existingCategory.fromMain = false;
                        existingCategory.entryOverrideCount = 0;
                        categoryList.Add(existingCategory);
                    }
                }
            }

            asset.SetLibrary(categoryList);

            SpriteLibrarySourceAssetImporter.SaveSpriteLibrarySourceAsset(asset, relativePath);
            Object.DestroyImmediate(asset);
            return relativePath;
        }

        /// <summary>
        /// Creates a new Sprite Library Source Asset at a given path.
        /// </summary>
        /// <param name="path">Save path. Must be within the Assets folder.</param>
        /// <param name="spriteLibraryAsset">Sprite Library Asset to be saved.</param>
        /// <param name="mainLibraryPath">A path to the main library. Null if there is no main library.</param>
        /// <returns>A relative path to the Project with correct extension.</returns>
        /// <exception cref="InvalidOperationException">Throws when the save path is invalid/</exception>
        public static string Create(string path, SpriteLibraryAsset spriteLibraryAsset, string mainLibraryPath = null)
        {
            return Create(path, spriteLibraryAsset.categories, mainLibraryPath);
        }

        /// <summary>
        /// Creates a new Sprite Library Source Asset at a given path.
        /// </summary>
        /// <param name="spriteLibraryAsset">Sprite Library Asset to be saved.</param>
        /// <param name="path">Save path. Must be within the Assets folder.</param>
        /// <param name="mainLibraryPath">A path to the main library. Null if there is no main library.</param>
        /// <returns>A relative path to the Project with correct extension.</returns>
        /// <exception cref="InvalidOperationException">Throws when the save path is invalid/</exception>
        public static string SaveAsSourceAsset(this SpriteLibraryAsset spriteLibraryAsset, string path, string mainLibraryPath = null)
        {
            return Create(path, spriteLibraryAsset, mainLibraryPath);
        }

        internal static SpriteLibrarySourceAsset LoadSpriteLibrarySourceAsset(string path)
        {
            Object[] loadedObjects = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(path);
            foreach (Object obj in loadedObjects)
            {
                if (obj is SpriteLibrarySourceAsset asset)
                    return asset;
            }

            return null;
        }

        static string GetRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (!path.StartsWith("Assets/") && !path.StartsWith(Application.dataPath))
                return null;

            int pathStartIndex = path.IndexOf("Assets");
            return pathStartIndex == -1 ? null : path.Substring(pathStartIndex);
        }
    }
}
