using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AvatarTextureStudio.Infrastructure
{
    internal static class NewImageExport
    {
        public static string Save(Texture2D image, bool srgb, TextureWrapMode u, TextureWrapMode v, string defaultName)
        {
            string path = EditorUtility.SaveFilePanel("Export new texture PNG", "Assets", defaultName, "png");
            if (string.IsNullOrEmpty(path)) return null;
            string assetPath = SafeFileIO.ExportAssetPath(path, Path.GetFullPath("."));
            if (File.Exists(path) || File.Exists(path + ".meta")) throw new IOException("Choose a new asset filename. Existing PNG/meta files are preserved.");
            byte[] png = image.EncodeToPNG(); SafeFileIO.Write(path, s => s.Write(png, 0, png.Length), false);
            AssetDatabase.ImportAsset(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new IOException("PNG was written, but its new importer is unavailable: " + assetPath);
            importer.sRGBTexture = srgb; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Math.Max(32, Math.Max(image.width, image.height)));
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapModeU = u; importer.wrapModeV = v;
            importer.SaveAndReimport(); return assetPath;
        }
    }
}
