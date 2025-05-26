using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.U2D.Animation
{
    internal static class EditorIconUtility
    {
        public const string LightIconPath = "EditorIcons/Light";
        public const string DarkIconPath = "EditorIcons/Dark";

        public static Texture2D LoadIconResource(string name, string lightPath, string darkPath)
        {
            string iconPath = "";

            if (EditorGUIUtility.isProSkin && !string.IsNullOrEmpty(darkPath))
                iconPath = Path.Combine(darkPath, "d_" + name);
            else
                iconPath = Path.Combine(lightPath, name);
            if (EditorGUIUtility.pixelsPerPoint > 1.0f)
            {
                Texture2D icon2x = ResourceLoader.Load<Texture2D>(iconPath + "@2x.png");
                if (icon2x != null)
                    return icon2x;
            }

            return ResourceLoader.Load<Texture2D>(iconPath + ".png");
        }

        public static Texture2D LoadIconResourceWithMipLevels(string name, string lightPath, string darkPath, bool forceUpdate = false)
        {
            string iconPath = "";

            if (EditorGUIUtility.isProSkin && !string.IsNullOrEmpty(darkPath))
                iconPath = Path.Combine(darkPath, "d_" + name);
            else
                iconPath = Path.Combine(lightPath, name);

            iconPath += ".asset";

            Texture2D icon = ResourceLoader.Load<Texture2D>(iconPath);
            if (icon == null || forceUpdate)
            {
                string assetPath = ResourceLoader.GetAssetPath(iconPath);
                icon = GenerateIconWithMipLevels(assetPath, "@", "png");

                // Delay the asset creation to avoid creating an asset in the middle of an import
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.CreateAsset(icon, assetPath);
                };
            }

            return icon;
        }

        internal static Texture2D GenerateIconWithMipLevels(string assetPath, string mipIdentifier, string mipFileExtension)
        {
            FileInfo assetFileInfo = new FileInfo(assetPath);
            string absoluteDirectoryPath = Path.GetDirectoryName(assetFileInfo.FullName);
            string baseName = assetFileInfo.Name;
            baseName = baseName.Substring(0, baseName.LastIndexOf(".", StringComparison.Ordinal));

            string searchPattern = $"{baseName}{mipIdentifier}*.{mipFileExtension}";
            List<string> sourceFilePaths = new List<string>(Directory.GetFiles(absoluteDirectoryPath, searchPattern, SearchOption.AllDirectories));
            searchPattern = $"{baseName}.{mipFileExtension}";
            sourceFilePaths.AddRange(Directory.GetFiles(absoluteDirectoryPath, searchPattern, SearchOption.AllDirectories));

            Uri absoluteDirectoryUri = new Uri($"{absoluteDirectoryPath}/");
            string relativeDirectoryPath = Path.GetDirectoryName(assetPath);
            List<Texture2D> textures = new List<Texture2D>();
            foreach (string path in sourceFilePaths)
            {
                string relativePath = Uri.UnescapeDataString($"{relativeDirectoryPath}/{absoluteDirectoryUri.MakeRelativeUri(new Uri(path))}");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            if (textures.Count == 0)
            {
                return null;
            }

            textures.Sort((a, b) => -a.width.CompareTo(b.width));

            int maxSize = textures[0].width;
            Texture2D textureWithMips = new Texture2D(maxSize, maxSize, GraphicsFormat.R8G8B8A8_SRGB, -1, TextureCreationFlags.MipChain);

            Blit(textures[0], textureWithMips, 0);
            textureWithMips.Apply(updateMipmaps: true);

            int resolution = maxSize / 2;
            int mipLevel = 1;
            for (int i = 1; i < textures.Count; i++)
            {
                if (textures[i].width != resolution)
                {
                    continue;
                }

                Blit(textures[i], textureWithMips, mipLevel);
                textureWithMips.Apply(updateMipmaps: true);
                resolution /= 2;
                mipLevel += 1;
            }

            textureWithMips.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            textureWithMips.name = $"{baseName}.{mipFileExtension}";

            return textureWithMips;
        }

        private static void Blit(Texture2D sourceTexture, Texture2D destTexture, int mipLevel)
        {
            // destTexture might be unreadable and different formats from sourceTexture,
            // so we need to use a RenderTexture to convert
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            RenderTexture renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.useMipMap = false;
            Graphics.Blit(sourceTexture, renderTexture);

            Texture2D texture = new Texture2D(width, height, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);

            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = null;
            renderTexture.Release();

            Graphics.CopyTexture(texture, 0, 0, destTexture, 0, mipLevel);
        }
    }
}
