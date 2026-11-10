using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using AvatarTextureStudio.Painting;
using AvatarTextureStudio.TextureLab;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.Tests
{
    public static class StudioTests
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("ATSTEST PASS " + message);
        }

        static void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { Debug.Log("ATSTEST PASS " + message); return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + message);
        }

        public static void Run()
        {
            int code = 1;
            string scratch = Path.Combine(Path.GetTempPath(), "ats-unity-" + Guid.NewGuid().ToString("N"));
            string folder = null;
            try
            {
                Check(File.Exists("ATS_TEST_PROJECT"), "running in the isolated development project");
                Debug.Log("ATSTEST colorspace=" + QualitySettings.activeColorSpace + " device=" + SystemInfo.graphicsDeviceType);
                Directory.CreateDirectory(scratch);
                string name = "ATS_Shot1Tests_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", name);
                folder = "Assets/" + name;
                MalformedProjects(scratch);
                ExportAndComparisonSession(folder);
                ComparePixels();
                code = 0;
            }
            catch (Exception e) { Debug.LogError("ATSTEST FAIL " + e); }
            finally
            {
                if (folder != null) AssetDatabase.DeleteAsset(folder);
                if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
                PaintResources.Release();
            }
            Debug.Log("ATSTEST DONE exit=" + code);
            EditorApplication.Exit(code);
        }

        static ProjectMeta Meta() => new ProjectMeta { parts = new[] { new PartMeta { width = 16, height = 16 } } };

        static void WriteProject(string path, ProjectMeta meta, Action<BinaryWriter> body)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("MTPAINT2"));
                writer.Write(JsonUtility.ToJson(meta));
                body(writer);
            }
        }

        static void MalformedProjects(string scratch)
        {
            string path = Path.Combine(scratch, "bad.mtpaint");
            var meta = Meta();
            meta.parts[0].width = int.MaxValue;
            WriteProject(path, meta, w => w.Write(1));
            Reject<InvalidDataException>(() => PaintProjectIO.ReadMeta(path), "oversized dimensions fail before pixel allocation");
            WriteProject(path, Meta(), w => { w.Write(1); w.Write(0); });
            Reject<InvalidDataException>(() => PaintProjectIO.Load(path, out _), "empty layer stack is rejected");
            WriteProject(path, Meta(), w => w.Write(2));
            Reject<InvalidDataException>(() => PaintProjectIO.Load(path, out _), "texture count mismatch is rejected");
            WriteProject(path, Meta(), w => {
                w.Write(1); w.Write(1);
                w.Write(JsonUtility.ToJson(new LayerProps { name = "Layer", opacity = 1, visible = true }));
                w.Write(99); w.Write(1); w.Write((byte)0);
            });
            Reject<InvalidDataException>(() => PaintProjectIO.Load(path, out _), "unknown payload encoding is rejected");
            byte[] shortPixels;
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true)) deflate.WriteByte(1);
                shortPixels = output.ToArray();
            }
            WriteProject(path, Meta(), w => {
                w.Write(1); w.Write(1);
                w.Write(JsonUtility.ToJson(new LayerProps { name = "Layer", opacity = 1, visible = true }));
                w.Write(2); w.Write(shortPixels.Length); w.Write(shortPixels);
            });
            Reject<InvalidDataException>(() => PaintProjectIO.Load(path, out _), "truncated deflate pixels are not silently zero-filled");
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("MTPAINT2"));
                writer.Write(new byte[] { 255, 255, 255, 255, 127 });
            }
            Reject<InvalidDataException>(() => PaintProjectIO.ReadMeta(path), "malformed string length is rejected");
        }

        static void ExportAndComparisonSession(string folder)
        {
            string sourcePath = folder + "/source.png";
            var pixels = Solid(new Color(0.2f, 0.4f, 0.6f, 1));
            File.WriteAllBytes(sourcePath, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            Check(!texture.isReadable && TextureComparePanel.UnsupportedReason(texture) == null, "non-readable imported color texture can be compared");
            byte[] original = File.ReadAllBytes(sourcePath);
            string importerBefore = EditorJsonUtility.ToJson(importer);
            var go = new GameObject("Studio test mesh");
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up }, triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard")) { mainTexture = texture };
            renderer.sharedMaterial = mat;
            MeshTexturePainterWindow window = null;
            PaintSession session = null;
            try
            {
                session = PaintSession.Create(renderer, new[] { 0 }, 0, "_MainTex", 16, 16, 0, new BrushSettings(), out string error);
                Check(session != null, "painting session created: " + error);
                session.FillLayer(Color.red);
                Reject<IOException>(() => session.ExportTexture(0, sourcePath, false), "source replacement needs explicit overwrite permission");
                session.ExportTexture(0, folder + "/export.png", false);
                Check(mat.mainTexture == texture && original.SequenceEqual(File.ReadAllBytes(sourcePath)), "default export preserves original texture and shared material");

                window = ScriptableObject.CreateInstance<MeshTexturePainterWindow>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                object tab = typeof(MeshTexturePainterWindow).GetField("colorTab", flags).GetValue(window);
                var other = new GameObject("Other root");
                try
                {
                    other.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var otherRenderer = other.AddComponent<MeshRenderer>();
                    otherRenderer.sharedMaterial = mat;
                    typeof(MeshTexturePainterWindow).GetMethod("UseRoot", flags).Invoke(window, new object[] { tab, go });
                    typeof(MeshTexturePainterWindow).GetMethod("SetRenderer", flags).Invoke(window, new object[] { tab, otherRenderer });
                    Check((GameObject)tab.GetType().GetField("root").GetValue(tab) == other,
                        "dropping a renderer outside the chosen root updates the root instead of reverting the target");
                }
                finally { Object.DestroyImmediate(other); }
                typeof(MeshTexturePainterWindow).GetMethod("AttachSession", flags).Invoke(window, new[] { tab, session });
                session.RefreshPreview();
                int history = session.History.Count;
                typeof(MeshTexturePainterWindow).GetMethod("SelectTab", flags).Invoke(window, new object[] { 2 });
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Check(block.GetTexture("_MainTex") == null && mat.mainTexture == texture, "Compare suspends paint preview without changing the material");
                typeof(MeshTexturePainterWindow).GetMethod("SelectTab", flags).Invoke(window, new object[] { 0 });
                Check(session.Document.Layers.Count == 2 && session.History.Count == history && session.Dirty, "return from Compare retains layers, undo history and dirty state");
                Check(importerBefore == EditorJsonUtility.ToJson(importer), "comparison validation and navigation leave importer settings unchanged");
            }
            finally
            {
                if (window != null) { window.DiscardChanges(); window.Close(); }
                else session?.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(mat);
            }
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(color, 256).ToArray());
            texture.Apply();
            return texture;
        }

        static void ComparePixels()
        {
            var shader = Shader.Find("Hidden/AvatarTextureStudio/Compare");
            Check(shader != null && shader.isSupported, "Compare shader is supported");
            var material = new Material(shader);
            var a = Solid(new Color(0.3f, 0.3f, 0.3f, 0.2f));
            var b = Solid(new Color(0.3f, 0.3f, 0.3f, 0.8f));
            var target = RTUtil.Create("Compare test", 16, 16, RenderTextureFormat.ARGB32);
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                material.SetTexture("_TexA", a); material.SetTexture("_TexB", b);
                material.SetFloat("_Mode", 3); material.SetFloat("_Gain", 1);
                material.SetFloat("_RawValues", 1); material.SetFloat("_LinearProject", 0);
                Graphics.Blit(a, target, material);
                Color difference = RTUtil.ReadPixel(target, 8, 8);
                Check(Mathf.Abs(difference.r - 0.6f) < 0.03f && Mathf.Abs(difference.g - 0.6f) < 0.03f,
                    "difference reveals alpha-only changes as opaque grayscale");
                material.SetTexture("_TexB", a);
                Graphics.Blit(a, target, material);
                Color equal = RTUtil.ReadPixel(target, 8, 8);
                Check(equal.r < 0.01f && equal.g < 0.01f && equal.b < 0.01f, "identical textures have zero RGBA difference");
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RTUtil.Release(ref target);
                Object.DestroyImmediate(material); Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            }
        }
    }
}
