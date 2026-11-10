using System;
using System.IO;
using System.Linq;
using AvatarTextureStudio.Creative;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.TextureLab;
using UnityEditor;
using UnityEngine;
using N2 = System.Numerics.Vector2;
using N3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.Tests
{
    /// <summary>Real Unity adapter/readback/recovery checks. Runs only inside the isolated Studio harness.</summary>
    internal static class CreativeIntegrationTests
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            Debug.Log("ATSCREATIVE PASS " + message);
        }
        public static void Run(string folder, string scratch)
        {
            var go = new GameObject("Synthetic creative target");
            var mesh = new Mesh();
            var mat = new Material(Shader.Find("Unlit/Texture"));
            var workspace = new CreativeWorkspace(); var restored = new CreativeWorkspace();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one - Vector3.forward };
                mesh.subMeshCount = 2; mesh.SetTriangles(new[] { 0, 1, 2 }, 0); mesh.SetTriangles(new[] { 1, 3, 2 }, 1);
                var uv0 = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.uv = uv0;
                mesh.SetUVs(7, uv0.Select(v => new Vector2(1 - v.x, v.y)).ToList());
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { mat, mat };
                using (var adapter = new CreativeMeshAdapter(renderer, 1, 7, "_MainTex", N2.One, N2.Zero, TextureWrapMode.Repeat, TextureWrapMode.Repeat))
                {
                    var hits = adapter.Layout.FindNearestRay(new N3(.8f, .8f, -1), N3.UnitZ, 1);
                    Check(adapter.Layout.Triangles.Length == 1 && adapter.Layout.Triangles[0].Submesh == 1 && adapter.Layout.Triangles[0].Channel == 7
                        && hits.Count == 1 && N2.Distance(hits[0].MeshUv, new N2(.2f, .8f)) < 1e-5f, "Unity source UV7 and selected material submesh agree with the CPU correspondence kernel");
                    go.transform.position = Vector3.forward * 2;
                    Check(adapter.Refresh() && Math.Abs(adapter.Layout.Triangles[0].A.Z - 2) < 1e-5, "pose hashing refreshes world vertices after a renderer transform");
                    Check(mesh.uv.SequenceEqual(uv0), "UV inspection leaves the source mesh untouched");
                }
                string sourcePath = folder + "/creative-source.png";
                byte[] rgba = Enumerable.Range(0, 16).SelectMany(i => new byte[] { (byte)(40 + i * 7), 110, 75, (byte)(i * 17) }).ToArray();
                var pixels = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                try { pixels.LoadRawTextureData(rgba); pixels.Apply(); File.WriteAllBytes(sourcePath, pixels.EncodeToPNG()); }
                finally { Object.DestroyImmediate(pixels); }
                AssetDatabase.ImportAsset(sourcePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
                importer.sRGBTexture = true; importer.isReadable = false; importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath); mat.SetTexture("_MainTex", source);
                byte[] sourceBytes = File.ReadAllBytes(sourcePath), sourceMeta = File.ReadAllBytes(sourcePath + ".meta");
                workspace.SetTarget(renderer, 0, 0, "_MainTex"); workspace.Capture(null);
                Check(workspace.Document.Baseline.IsSRGB && workspace.Document.Baseline.CopyPixels().Zip(rgba, (a, b) => Math.Abs(a - b)).All(d => d <= 1),
                    "creative capture preserves sRGB stored channels, alpha and orientation through real GPU readback");
                Check(!source.isReadable && File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes) && File.ReadAllBytes(sourcePath + ".meta").SequenceEqual(sourceMeta),
                    "creative capture does not change source image bytes or importer metadata");
                workspace.SaveTo(Path.Combine(scratch, "creative.atsstudio")); workspace.Autosave();
                restored.TryRestore();
                Check(!restored.Dirty && restored.Document.Baseline.CopyPixels().SequenceEqual(workspace.Document.Baseline.CopyPixels()),
                    "Library creative recovery uses real Unity SessionState and retains the clean saved state");
                string malformed = Path.Combine(scratch, "invalid.atsstudio"); File.WriteAllBytes(malformed, new byte[] { 1, 2, 3 });
                var retained = restored.Document; bool rejected = false;
                try { restored.Open(malformed); } catch (IOException) { rejected = true; } catch (InvalidDataException) { rejected = true; }
                Check(rejected && ReferenceEquals(retained, restored.Document) && !restored.Dirty, "failed creative open retains the existing document and dirty state");
                var transient = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
                try
                {
                    Check(TextureComparePanel.UnsupportedReason(transient) != null && TextureComparePanel.UnsupportedReason(transient, true) == null,
                        "TextureLab permits owned snapshots while its default asset-only validation stays intact");
                }
                finally { Object.DestroyImmediate(transient); }
            }
            finally
            {
                workspace.DiscardRecovery(); restored.DiscardRecovery(); workspace.Dispose(); restored.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); Object.DestroyImmediate(mat);
                string recovery = Path.Combine("Library", "AvatarTextureStudio", "CreativeAutosave.atsstudio");
                if (File.Exists(recovery)) File.Delete(recovery);
            }
        }
    }
}
