using System.IO;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    internal static class ResourceLoader
    {
        const string k_ResourcePath = "Packages/com.unity.2d.animation/Editor/Assets";

        internal static string GetAssetPath(string path)
        {
            return Path.Combine(k_ResourcePath, path);
        }

        internal static T Load<T>(string path) where T : Object
        {
            string assetPath = Path.Combine(k_ResourcePath, path);
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset;
        }
    }
}

