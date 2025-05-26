using System;
using System.IO;
using UnityEngine;

namespace UnityEditor.U2D.Animation.SpriteLibraryEditor
{
    internal class SpriteLibraryAssetPostprocessor : AssetPostprocessor
    {
        public static event Action<string> OnImported;
        public static event Action<string> OnDeleted;
        public static event Action<string, string> OnMovedAssetFromTo;

        const string k_SpriteLibExtension = ".spriteLib";

        static bool IsPathSpriteLibrary(string assetPath) => string.Equals(Path.GetExtension(assetPath), k_SpriteLibExtension);

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (movedAssets.Length == movedFromAssetPaths.Length)
            {
                if (OnMovedAssetFromTo != null)
                {
                    for (int i = 0; i < movedAssets.Length; i++)
                    {
                        string fromPath = movedFromAssetPaths[i];
                        if (IsPathSpriteLibrary(fromPath))
                        {
                            string toPath = IsPathSpriteLibrary(movedAssets[i]) ? movedAssets[i] : null;
                            OnMovedAssetFromTo.Invoke(fromPath, toPath);
                        }
                    }
                }
            }

            if (OnImported != null)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    string assetPath = importedAssets[i];
                    if (IsPathSpriteLibrary(assetPath))
                        OnImported.Invoke(assetPath);
                }
            }

            if (OnDeleted != null)
            {
                for (int i = 0; i < deletedAssets.Length; i++)
                {
                    string assetPath = deletedAssets[i];
                    if (IsPathSpriteLibrary(assetPath))
                        OnDeleted.Invoke(assetPath);
                }
            }
        }
    }
}
