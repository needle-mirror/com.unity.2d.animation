using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace UnityEditor.U2D.Animation
{
    /// <summary>
    /// A ScriptedImporter that imports .spriteLib extension file to generate
    /// SpriteLibraryAsset
    /// </summary>
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.animation@latest/index.html?subfolder=/manual/SL-Asset.html")]
    [ScriptedImporter(22100000, "spriteLib", AllowCaching = true)]
    public class SpriteLibrarySourceAssetImporter : ScriptedImporter
    {
        /// <summary>
        /// Implementation of ScriptedImporter.OnImportAsset
        /// </summary>
        /// <param name="ctx">
        /// This argument contains all the contextual information needed to process the import
        /// event and is also used by the custom importer to store the resulting Unity Asset.
        /// </param>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            SpriteLibraryAsset spriteLib = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            spriteLib.name = Path.GetFileNameWithoutExtension(assetPath);
            Object[] sourceAsset = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(assetPath);
            if (sourceAsset?.Length > 0)
            {
                SpriteLibrarySourceAsset sourceLibraryAsset = sourceAsset[0] as SpriteLibrarySourceAsset;
                if (sourceLibraryAsset != null)
                {
                    if (!HasValidMainLibrary(sourceLibraryAsset, assetPath))
                        sourceLibraryAsset.SetPrimaryLibraryGUID(string.Empty);

                    UpdateSpriteLibrarySourceAssetLibraryWithMainAsset(sourceLibraryAsset);

                    foreach (SpriteLibCategoryOverride cat in sourceLibraryAsset.library)
                    {
                        spriteLib.AddCategoryLabel(null, cat.name, null);
                        foreach (SpriteCategoryEntryOverride entry in cat.overrideEntries)
                        {
                            spriteLib.AddCategoryLabel(entry.spriteOverride, cat.name, entry.name);
                        }
                    }

                    spriteLib.modificationHash = sourceLibraryAsset.modificationHash;
                    spriteLib.version = sourceLibraryAsset.version;

                    if (!string.IsNullOrEmpty(sourceLibraryAsset.primaryLibraryGUID))
                        ctx.DependsOnArtifact(AssetDatabase.GUIDToAssetPath(sourceLibraryAsset.primaryLibraryGUID));
                }
            }

            ctx.AddObjectToAsset("SpriteLib", spriteLib);
        }

        internal static void UpdateSpriteLibrarySourceAssetLibraryWithMainAsset(SpriteLibrarySourceAsset sourceLibraryAsset)
        {
            SerializedObject so = new SerializedObject(sourceLibraryAsset);
            SerializedProperty library = so.FindProperty(SpriteLibrarySourceAssetPropertyString.library);
            string mainLibraryAssetAssetPath = AssetDatabase.GUIDToAssetPath(sourceLibraryAsset.primaryLibraryGUID);
            SpriteLibraryAsset mainLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(mainLibraryAssetAssetPath);
            SpriteLibraryUtilitiesEditor.UpdateLibraryWithNewMainLibrary(mainLibraryAsset, library);
            if (so.hasModifiedProperties)
            {
                SerializedProperty modHashProperty = so.FindProperty(SpriteLibrarySourceAssetPropertyString.modificationHash);
                modHashProperty.longValue = mainLibraryAsset != null ? mainLibraryAsset.modificationHash : SpriteLibraryUtility.GenerateHash();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static bool HasValidMainLibrary(SpriteLibrarySourceAsset sourceLibraryAsset, string assetPath)
        {
            if (string.IsNullOrEmpty(sourceLibraryAsset.primaryLibraryGUID))
                return false;

            string primaryLibraryPath = AssetDatabase.GUIDToAssetPath(sourceLibraryAsset.primaryLibraryGUID);
            if (assetPath == primaryLibraryPath)
                return false;

            List<SpriteLibraryAsset> primaryAssetParentChain = GetAssetParentChain(AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(primaryLibraryPath));
            foreach (SpriteLibraryAsset parentLibrary in primaryAssetParentChain)
            {
                string parentPath = AssetDatabase.GetAssetPath(parentLibrary);
                if (parentPath == assetPath)
                    return false;
            }

            return true;
        }

        internal static SpriteLibrarySourceAsset LoadSpriteLibrarySourceAsset(string path)
        {
            Object[] loadedObjects = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(path);
            foreach (Object obj in loadedObjects)
            {
                if (obj is SpriteLibrarySourceAsset)
                    return (SpriteLibrarySourceAsset)obj;
            }

            return null;
        }

        internal static void SaveSpriteLibrarySourceAsset(SpriteLibrarySourceAsset obj, string path)
        {
            if (!HasValidMainLibrary(obj, path))
                obj.SetPrimaryLibraryGUID(string.Empty);

            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { obj }, path, true);
        }

        [MenuItem("internal:Assets/Convert to SpriteLibrarySourceAsset", true)]
        static bool ConvertToSpriteLibrarySourceAssetValidate()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is SpriteLibraryAsset)
                    return true;
            }

            return false;
        }

        [MenuItem("internal:Assets/Convert to SpriteLibrarySourceAsset")]
        static void ConvertToSourceAsset()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is SpriteLibraryAsset)
                {
                    SpriteLibraryAsset asset = (SpriteLibraryAsset)obj;
                    string path = AssetDatabase.GetAssetPath(asset);
                    string currentAssetPath = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string convertFileName = fileName + SpriteLibrarySourceAsset.extension;
                    convertFileName = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(currentAssetPath, convertFileName));
                    SpriteLibrarySourceAsset convertAsset = ScriptableObject.CreateInstance<SpriteLibrarySourceAsset>();
                    convertAsset.SetLibrary(new List<SpriteLibCategoryOverride>(asset.categories.Count));
                    for (int i = 0; i < asset.categories.Count; ++i)
                    {
                        SpriteLibCategory category = asset.categories[i];
                        SpriteLibCategoryOverride newCategory = new SpriteLibCategoryOverride()
                        {
                            overrideEntries = new List<SpriteCategoryEntryOverride>(category.categoryList.Count),
                            name = category.name,
                            entryOverrideCount = 0,
                            fromMain = false
                        };
                        convertAsset.AddCategory(newCategory);
                        for (int j = 0; j < category.categoryList.Count; ++j)
                        {
                            newCategory.overrideEntries.Add(new SpriteCategoryEntryOverride()
                            {
                                name = category.categoryList[j].name,
                                sprite = null,
                                fromMain = false,
                                spriteOverride = category.categoryList[j].sprite
                            });
                        }
                    }

                    SaveSpriteLibrarySourceAsset(convertAsset, convertFileName);
                }
            }

            AssetDatabase.Refresh();
        }

        internal static SpriteLibraryAsset GetAssetParent(SpriteLibraryAsset asset)
        {
            string currentAssetPath = AssetDatabase.GetAssetPath(asset);
            if (AssetImporter.GetAtPath(currentAssetPath) is SpriteLibrarySourceAssetImporter)
            {
                SpriteLibrarySourceAsset sourceAsset = LoadSpriteLibrarySourceAsset(currentAssetPath);
                string primaryLibraryId = sourceAsset != null ? sourceAsset.primaryLibraryGUID : null;
                if (primaryLibraryId != null)
                {
                    string primaryLibraryAssetAssetPath = AssetDatabase.GUIDToAssetPath(primaryLibraryId);
                    return AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(primaryLibraryAssetAssetPath);
                }
            }

            return null;
        }

        internal static List<SpriteLibraryAsset> GetAssetParentChain(SpriteLibraryAsset asset)
        {
            List<SpriteLibraryAsset> chain = new List<SpriteLibraryAsset>();
            if (asset != null)
            {
                SpriteLibraryAsset parent = GetAssetParent(asset);
                while (parent != null && !chain.Contains(parent))
                {
                    chain.Add(parent);
                    parent = GetAssetParent(parent);
                }
            }

            return chain;
        }

        internal static SpriteLibraryAsset GetAssetFromSelection()
        {
            foreach (Object selectedObject in Selection.objects)
            {
                SpriteLibraryAsset selectedAsset = selectedObject as SpriteLibraryAsset;
                if (selectedAsset != null)
                    return selectedAsset;
            }

            return null;
        }
    }
}
