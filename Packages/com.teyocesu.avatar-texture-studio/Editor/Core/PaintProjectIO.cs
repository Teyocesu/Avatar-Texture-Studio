using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using AvatarTextureStudio.Infrastructure;

namespace AvatarTextureStudio.Painting
{
    /// <summary>One painted texture of a project.</summary>
    [Serializable]
    internal class PartMeta
    {
        public string textureProperty = "_MainTex";
        public int uvChannel;
        public int width;
        public int height;
        public bool srgb = true;
        public string sourceTexturePath;
        public string exportPath;
        public int wrapU = (int)TextureWrapMode.Repeat;
        public int wrapV = (int)TextureWrapMode.Repeat;
    }

    /// <summary>Everything besides pixels that a saved project remembers.</summary>
    [Serializable]
    internal class ProjectMeta
    {
        public string rendererId;           // GlobalObjectId of the renderer (best effort)
        public string rendererPath;         // hierarchy path, used when the id no longer resolves
        public int[] slots = { 0 };
        public int padding = 16;
        public int activeLayer;
        public int mode;                    // PaintMode: 0 colour layers, 1 mask
        public PartMeta[] parts;

        // version 1 files (a single texture) kept the texture settings here
        public int width;
        public int height;
        public bool srgb = true;
        public int uvChannel;
        public string textureProperty = "_MainTex";
        public string sourceTexturePath;
        public string exportPath;
        public int wrapU = (int)TextureWrapMode.Repeat;
        public int wrapV = (int)TextureWrapMode.Repeat;
    }

    /// <summary>
    /// .mtpaint project files: a small binary container with a JSON header and,
    /// for every painted texture, one image blob per layer (PNG, or deflated raw
    /// RGBA for fast autosaves). Version 1 files (one texture) still open.
    /// </summary>
    internal static class PaintProjectIO
    {
        public const string Extension = "mtpaint";
        static readonly byte[] MagicV1 = Encoding.ASCII.GetBytes("MTPAINT1");
        static readonly byte[] MagicV2 = Encoding.ASCII.GetBytes("MTPAINT2");
        const int EncodingPng = 1;
        const int EncodingDeflate = 2;
        const int MaxJsonBytes = 1024 * 1024;
        const int MaxParts = 32;
        const int MaxLayers = 1024;
        const long MaxProjectPixelBytes = 2L * 1024 * 1024 * 1024;

        public static void Save(string path, IReadOnlyList<PaintDocument> docs, ProjectMeta meta, bool fast)
        {
            if (!string.Equals(Path.GetExtension(path), "." + Extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Painting projects must use the .mtpaint extension.");
            if (docs == null || docs.Count == 0 || meta == null || meta.parts == null || meta.parts.Length != docs.Count)
                throw new InvalidOperationException("The project settings do not match the painted textures.");
            for (int i = 0; i < docs.Count; i++)
            {
                meta.parts[i].width = docs[i].Width;
                meta.parts[i].height = docs[i].Height;
                meta.parts[i].srgb = docs[i].IsSRGB;
            }
            meta.activeLayer = docs[0].ActiveIndex;
            ValidateMeta(meta);
            ValidateDocuments(docs);

            SafeFileIO.Write(path, stream =>
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(MagicV2);
                    WriteBoundedString(writer, JsonUtility.ToJson(meta));
                    writer.Write(docs.Count);
                    foreach (var doc in docs)
                    {
                        writer.Write(doc.Layers.Count);
                        foreach (var layer in doc.Layers) WriteLayer(writer, layer, fast);
                    }
                }
            }, overwrite: true);
        }

        static void WriteLayer(BinaryWriter writer, PaintLayer layer, bool fast)
        {
            WriteBoundedString(writer, JsonUtility.ToJson(layer.Props));
            var tex = RTUtil.ReadToTexture(layer.texture);
            byte[] data;
            int encoding;
            try
            {
                if (fast)
                {
                    encoding = EncodingDeflate;
                    data = Deflate(tex.GetRawTextureData());
                }
                else
                {
                    encoding = EncodingPng;
                    data = tex.EncodeToPNG();
                }
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            writer.Write(encoding);
            writer.Write(data.Length);
            writer.Write(data);
        }

        /// <summary>Reads only the header of a project, e.g. to tell colour and mask projects apart before loading any pixels.</summary>
        public static ProjectMeta ReadMeta(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(MagicV2.Length);
                if (!magic.SequenceEqual(MagicV1) && !magic.SequenceEqual(MagicV2))
                    throw new InvalidDataException("Not an Avatar Texture Studio project file.");
                var meta = ReadProjectMeta(reader, magic.SequenceEqual(MagicV1));
                return meta;
            }
        }

        /// <summary>Loads every painted texture of a project (in the order of meta.parts).</summary>
        public static List<PaintDocument> Load(string path, out ProjectMeta meta)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(MagicV2.Length);
                bool v1 = magic.SequenceEqual(MagicV1);
                if (!v1 && !magic.SequenceEqual(MagicV2))
                    throw new InvalidDataException("Not an Avatar Texture Studio project file.");

                meta = ReadProjectMeta(reader, v1);

                int docCount = v1 ? 1 : reader.ReadInt32();
                if (docCount != meta.parts.Length)
                    throw new InvalidDataException("The project file is damaged (texture count mismatch).");

                var docs = new List<PaintDocument>();
                long pixelBytes = 0;
                try
                {
                    for (int d = 0; d < docCount; d++)
                    {
                        var pm = meta.parts[d];
                        int count = reader.ReadInt32();
                        ValidateLayerCount(count, meta.mode);
                        if (meta.activeLayer < 0 || meta.activeLayer >= count)
                            throw new InvalidDataException("The project's active layer is invalid.");
                        pixelBytes += (long)pm.width * pm.height * 4 * count;
                        ValidatePixelBudget(pixelBytes);
                        var doc = new PaintDocument(pm.width, pm.height) { IsSRGB = pm.srgb, IsMask = meta.mode == (int)PaintMode.Mask };
                        docs.Add(doc);
                        for (int i = 0; i < count; i++) doc.Layers.Add(ReadLayer(reader, doc, i));
                        doc.ActiveIndex = meta.activeLayer;
                    }
                    if (docs.Any(x => x.Layers.Count != docs[0].Layers.Count))
                        throw new InvalidDataException("The project file is damaged (layer count mismatch).");
                }
                catch
                {
                    foreach (var doc in docs) doc.Dispose();
                    throw;
                }
                return docs;
            }
        }

        static PaintLayer ReadLayer(BinaryReader reader, PaintDocument doc, int index)
        {
            var props = JsonUtility.FromJson<LayerProps>(ReadBoundedString(reader));
            if (string.IsNullOrEmpty(props.name) || float.IsNaN(props.opacity) || props.opacity < 0 || props.opacity > 1
                || !Enum.IsDefined(typeof(LayerBlendMode), props.blendMode))
                throw new InvalidDataException($"Layer {index} has invalid properties.");
            int encoding = reader.ReadInt32();
            int length = reader.ReadInt32();
            int rawLength = checked(doc.Width * doc.Height * 4);
            if ((encoding != EncodingPng && encoding != EncodingDeflate) || length <= 0
                || length > (long)rawLength * 2 + MaxJsonBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException($"Layer {index} has an invalid or truncated payload.");
            var data = reader.ReadBytes(length);
            if (encoding == EncodingPng && !PngMatches(data, doc.Width, doc.Height))
                throw new InvalidDataException($"Layer {index} PNG dimensions do not match its project.");

            var tex = new Texture2D(doc.Width, doc.Height, TextureFormat.RGBA32, false, true);
            try
            {
                if (encoding == EncodingDeflate)
                {
                    tex.LoadRawTextureData(Inflate(data, rawLength));
                    tex.Apply(false);
                }
                else if (!tex.LoadImage(data, false))
                {
                    throw new InvalidDataException($"Layer {index} could not be decoded.");
                }
                return new PaintLayer(props.name, doc.RenderImported(tex, false)) { Props = props };
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        static byte[] Deflate(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                    deflate.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        static byte[] Inflate(byte[] data, int expectedLength)
        {
            var result = new byte[expectedLength];
            using (var input = new MemoryStream(data))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                int offset = 0;
                while (offset < expectedLength)
                {
                    int read = deflate.Read(result, offset, expectedLength - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                if (offset != expectedLength || deflate.ReadByte() != -1)
                    throw new InvalidDataException("The project contains truncated or oversized pixel data.");
            }
            return result;
        }

        // ------------------------------------------------------------------ export

        /// <summary>Writes the flattened, seam padded texture as PNG.</summary>
        public static void ExportPng(PaintDocument doc, RenderTexture padMap, Vector4 wrapMode, string path, bool overwrite = false)
        {
            var flat = RTUtil.Create("MTP Export", doc.Width, doc.Height, RenderTextureFormat.ARGB32);
            try
            {
                if (padMap != null) doc.Present(padMap, wrapMode, flat);
                else Graphics.CopyTexture(doc.GetComposite(), flat);
                var tex = RTUtil.ReadToTexture(flat);
                try
                {
                    byte[] png = tex.EncodeToPNG();
                    SafeFileIO.Write(path, stream => stream.Write(png, 0, png.Length), overwrite);
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                RTUtil.Release(ref flat);
            }
        }

        static ProjectMeta ReadProjectMeta(BinaryReader reader, bool v1)
        {
            var meta = JsonUtility.FromJson<ProjectMeta>(ReadBoundedString(reader));
            if (meta == null) throw new InvalidDataException("The project has no settings.");
            if (v1 || meta.parts == null || meta.parts.Length == 0)
                meta.parts = new[] { new PartMeta {
                    textureProperty = meta.textureProperty, uvChannel = meta.uvChannel,
                    width = meta.width, height = meta.height, srgb = meta.srgb,
                    sourceTexturePath = meta.sourceTexturePath, exportPath = meta.exportPath,
                    wrapU = meta.wrapU, wrapV = meta.wrapV } };
            ValidateMeta(meta);
            return meta;
        }

        static void ValidateMeta(ProjectMeta meta)
        {
            if (meta.parts == null || meta.parts.Length < 1 || meta.parts.Length > MaxParts
                || (meta.mode != 0 && meta.mode != 1) || (meta.mode == 1 && meta.parts.Length != 1)
                || meta.padding < 0 || meta.padding > 256 || meta.slots == null || meta.slots.Length == 0
                || meta.slots.Length > 1024 || meta.slots.Any(s => s < 0 || s >= 1024))
                throw new InvalidDataException("The project has invalid texture, mode, padding or material-slot settings.");
            foreach (var part in meta.parts)
                if (part == null || part.width < 1 || part.width > 8192 || part.height < 1 || part.height > 8192
                    || part.uvChannel < 0 || part.uvChannel > 7 || string.IsNullOrWhiteSpace(part.textureProperty)
                    || !Enum.IsDefined(typeof(TextureWrapMode), part.wrapU) || !Enum.IsDefined(typeof(TextureWrapMode), part.wrapV))
                    throw new InvalidDataException("The project has invalid texture dimensions, UV channel or wrap mode.");
        }

        static void ValidateLayerCount(int count, int mode)
        {
            if (count < 1 || count > MaxLayers || (mode == 1 && count != 1))
                throw new InvalidDataException("The project has an invalid layer count.");
        }

        internal static void ValidateDocuments(IReadOnlyList<PaintDocument> docs, int addedLayers = 0)
        {
            if (docs.Count < 1 || docs.Count > MaxParts)
                throw new InvalidDataException("A project supports between 1 and 32 painted textures.");
            long pixelBytes = 0;
            foreach (var doc in docs)
            {
                int count = doc.Layers.Count + addedLayers;
                ValidateLayerCount(count, doc.IsMask ? 1 : 0);
                if (doc.Layers.Count != docs[0].Layers.Count)
                    throw new InvalidDataException("Painted textures must share their layer structure.");
                pixelBytes += (long)doc.Width * doc.Height * 4 * count;
            }
            ValidatePixelBudget(pixelBytes);
        }

        static void ValidatePixelBudget(long bytes)
        {
            if (bytes > MaxProjectPixelBytes)
                throw new InvalidDataException("The project exceeds the supported 2 GiB of decoded layer pixels. Split it into smaller projects.");
        }

        static string ReadBoundedString(BinaryReader reader)
        {
            uint length = 0;
            for (int shift = 0; shift <= 28; shift += 7)
            {
                byte b = reader.ReadByte();
                if (shift == 28 && (b & 0xF0) != 0) break;
                length |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) != 0) continue;
                if (length > MaxJsonBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("The project contains oversized or truncated settings.");
                return new UTF8Encoding(false, true).GetString(reader.ReadBytes((int)length));
            }
            throw new InvalidDataException("The project contains an invalid string length.");
        }

        static void WriteBoundedString(BinaryWriter writer, string value)
        {
            if (Encoding.UTF8.GetByteCount(value) > MaxJsonBytes)
                throw new InvalidDataException("Project settings or a layer name exceeds the supported 1 MiB field size.");
            writer.Write(value);
        }

        static bool PngMatches(byte[] data, int width, int height)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (data.Length < 24 || !data.Take(8).SequenceEqual(signature)
                || Encoding.ASCII.GetString(data, 12, 4) != "IHDR") return false;
            uint ReadBig(int p) => (uint)data[p] << 24 | (uint)data[p + 1] << 16 | (uint)data[p + 2] << 8 | data[p + 3];
            return ReadBig(16) == width && ReadBig(20) == height;
        }

        /// <summary>Gives a newly exported texture the import settings of the texture it replaces.</summary>
        public static void CopyImporterSettings(string fromAssetPath, string toAssetPath, bool srgbFallback, bool alphaIsTransparencyFallback = true)
        {
            var dst = AssetImporter.GetAtPath(toAssetPath) as TextureImporter;
            if (dst == null) return;
            var src = string.IsNullOrEmpty(fromAssetPath) ? null : AssetImporter.GetAtPath(fromAssetPath) as TextureImporter;
            if (src != null)
            {
                var settings = new TextureImporterSettings();
                src.ReadTextureSettings(settings);
                dst.SetTextureSettings(settings);
                dst.maxTextureSize = src.maxTextureSize;
                dst.textureCompression = src.textureCompression;
                dst.crunchedCompression = src.crunchedCompression;
                dst.compressionQuality = src.compressionQuality;
                dst.streamingMipmaps = src.streamingMipmaps;
                foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                {
                    var ps = src.GetPlatformTextureSettings(platform);
                    if (ps != null && ps.overridden) dst.SetPlatformTextureSettings(ps);
                }
            }
            else
            {
                dst.sRGBTexture = srgbFallback;
                dst.alphaIsTransparency = alphaIsTransparencyFallback;
                dst.streamingMipmaps = true;
                dst.maxTextureSize = 8192;
            }
            dst.SaveAndReimport();
        }

        // ------------------------------------------------------------------ source images

        public static bool IsSRGB(Texture texture)
        {
            if (texture == null) return true;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer != null) return importer.sRGBTexture;
            return GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
        }

        public static Vector2Int SourceSize(Texture texture)
        {
            if (texture == null) return new Vector2Int(2048, 2048);
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer != null)
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w > 0 && h > 0) return new Vector2Int(w, h);
            }
            return new Vector2Int(texture.width, texture.height);
        }

        /// <summary>
        /// Loads the original file of a texture asset at full quality (PNG, JPG,
        /// TGA) as raw values, bypassing import compression and size limits.
        /// Returns null for other formats.
        /// </summary>
        public static Texture2D LoadSourcePixels(Texture texture)
        {
            if (texture == null) return null;
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsSubAsset(texture)) return null;
            return LoadImageFile(Path.GetFullPath(assetPath));
        }

        public static Texture2D LoadImageFile(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            try
            {
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    if (tex.LoadImage(File.ReadAllBytes(fullPath), false)) return tex;
                    Object.DestroyImmediate(tex);
                    return null;
                }
                if (ext == ".tga") return TgaLoader.Load(File.ReadAllBytes(fullPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Avatar Texture Studio: could not read '{fullPath}': {e.Message}");
            }
            return null;
        }
    }

    /// <summary>Minimal TGA reader: true colour, 24/32 bit, raw or RLE.</summary>
    internal static class TgaLoader
    {
        public static Texture2D Load(byte[] data)
        {
            if (data.Length < 18) return null;
            int idLength = data[0];
            int colorMapType = data[1];
            int imageType = data[2];
            int width = data[12] | (data[13] << 8);
            int height = data[14] | (data[15] << 8);
            int bpp = data[16];
            bool topOrigin = (data[17] & 0x20) != 0;
            if (colorMapType != 0 || (imageType != 2 && imageType != 10) || (bpp != 24 && bpp != 32) || width == 0 || height == 0)
                return null;

            int bytesPerPixel = bpp / 8;
            int offset = 18 + idLength;
            var pixels = new Color32[width * height];
            int count = width * height;
            int p = 0;

            Color32 ReadPixel(ref int o)
            {
                byte b = data[o], g = data[o + 1], r = data[o + 2];
                byte a = bytesPerPixel == 4 ? data[o + 3] : (byte)255;
                o += bytesPerPixel;
                return new Color32(r, g, b, a);
            }

            if (imageType == 2)
            {
                for (; p < count && offset + bytesPerPixel <= data.Length; p++) pixels[p] = ReadPixel(ref offset);
            }
            else
            {
                while (p < count && offset < data.Length)
                {
                    int header = data[offset++];
                    int run = (header & 0x7F) + 1;
                    if ((header & 0x80) != 0)
                    {
                        var c = ReadPixel(ref offset);
                        for (int i = 0; i < run && p < count; i++) pixels[p++] = c;
                    }
                    else
                    {
                        for (int i = 0; i < run && p < count && offset + bytesPerPixel <= data.Length; i++) pixels[p++] = ReadPixel(ref offset);
                    }
                }
            }

            if (topOrigin)
            {
                var row = new Color32[width];
                for (int y = 0; y < height / 2; y++)
                {
                    int a = y * width, b = (height - 1 - y) * width;
                    Array.Copy(pixels, a, row, 0, width);
                    Array.Copy(pixels, b, pixels, a, width);
                    Array.Copy(row, 0, pixels, b, width);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }
    }
}
