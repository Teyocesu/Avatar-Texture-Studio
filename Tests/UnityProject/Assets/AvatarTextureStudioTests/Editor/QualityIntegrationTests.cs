using System;
using System.IO;
using System.Linq;
using AvatarTextureStudio.Creative;
using AvatarTextureStudio.Painting;
using AvatarTextureStudio.Quality.Core;
using AvatarTextureStudio.TextureLab;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using N2 = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.Tests
{
    internal static class QualityIntegrationTests
    {
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception(message); Debug.Log("ATSQUALITY PASS " + message); }
        public static void Run(string folder, string scratch)
        {
            var go = new GameObject("Synthetic quality target"); var mesh = new Mesh();
            var material = new Material(Shader.Find("Unlit/Texture")); var workspace = new CreativeWorkspace(); PaintSession session = null;
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1,1,0) }; mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                mesh.subMeshCount = 2; mesh.SetTriangles(new[] {0,1,2},0); mesh.SetTriangles(new[] {1,3,2},1);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] {material,material};
                using (var adapter = new CreativeMeshAdapter(renderer,0,0,"_MainTex",N2.One,N2.Zero,TextureWrapMode.Repeat,TextureWrapMode.Repeat))
                {
                    var topology = adapter.CaptureTopology();
                    Check(topology.Length == 2 && topology[0].IndexA == 0 && topology[1].Submesh == 1 && topology[1].Slot == 1 && topology[1].IndexB == 3,
                        "whole-source diagnostics preserve native vertex, submesh and material identities");
                    var candidates = SeamGeometry.Find(topology,0,0,N2.One,N2.Zero,AvatarTextureStudio.Creative.Core.UvWrap.Repeat,AvatarTextureStudio.Creative.Core.UvWrap.Repeat);
                    Check(candidates.Pairs.Count == 0 && candidates.Excluded.Keys.Any(k => k.Contains("Material")), "native cross-material boundaries do not substitute one texture for two");
                }
                string path = folder + "/quality-source.png";
                var texture = new Texture2D(4,4,TextureFormat.RGBA32,false,true);
                try { texture.SetPixels32(Enumerable.Range(0,16).Select(i => new Color32((byte)(i*11),100,75,(byte)(i*17))).ToArray()); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = false; importer.alphaIsTransparency = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path); material.SetTexture("_MainTex",source);
                byte[] pixels = File.ReadAllBytes(path), metadata = File.ReadAllBytes(path + ".meta"); var originalUv = mesh.uv;
                workspace.SetTarget(renderer,0,0,"_MainTex");
                var snapshot = workspace.CaptureDiagnosticImage(0,null,null,out string provenance,out var u,out var v,out _);
                var mips = DiagnosticMips.Build(snapshot,false);
                Check(mips.Count == 3 && mips.Last().Width == 1 && provenance.Contains("GPU mip-0") && workspace.Document == null,
                    "native readback feeds explicitly CPU diagnostic levels without creating/editing a creative document");
                session = PaintSession.Create(renderer,new[] {0},0,"_MainTex",16,16,0,BrushSettings.Load(),out string error);
                Check(session != null,"quality session fixture starts on the actual GPU: " + error);
                int layers = session.Document.Layers.Count; bool dirty = session.Dirty; var history = session.History;
                workspace.CaptureDiagnosticImage(1,session,null,out _,out _,out _,out _);
                Check(session.Document.Layers.Count == layers && session.Dirty == dirty && ReferenceEquals(session.History,history), "diagnosing a painting composite preserves its layers, dirty state and history");
                Check(!source.isReadable && File.ReadAllBytes(path).SequenceEqual(pixels) && File.ReadAllBytes(path + ".meta").SequenceEqual(metadata) && mesh.uv.SequenceEqual(originalUv)
                    && renderer.sharedMaterials[0] == material && material.GetTexture("_MainTex") == source, "diagnostics preserve source files/importers, UVs and shared material assignment");
                var report = new QualityReport { Source = provenance, Binding = "synthetic UV0", InputFormat = source.format.ToString(), Mapping = "native fixture; rendering unverified", Data = false, Levels = mips };
                string reportPath = Path.Combine(scratch,"quality-native.txt"); report.SaveNew(reportPath,false);
                Check(File.ReadAllText(reportPath).Contains("NOT TESTED"), "exported native diagnostic report does not claim acceptance");
                var caller = CreativeWorkspace.TextureFrom(snapshot.CopyPixels(),snapshot.Width,snapshot.Height,snapshot.IsSRGB,"Caller snapshot");
                var panel = new TextureComparePanel();
                try
                {
                    panel.SetPreviews(caller,caller,false,"Synthetic snapshot lifetime regression");
                    var owned = (Texture2D)typeof(TextureComparePanel).GetField("textureA",BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                    Object.DestroyImmediate(caller); caller = null;
                    Check(owned != null && owned.GetPixelData<byte>(0).ToArray().SequenceEqual(snapshot.CopyPixels()), "TextureLab owns independent snapshots after the caller releases or refreshes its previews");
                    panel.Dispose(); Check(owned == null,"TextureLab releases its own snapshot copies on disposal");
                }
                finally { if (caller != null) Object.DestroyImmediate(caller); panel.Dispose(); }
            }
            finally { session?.Dispose(); workspace.Dispose(); Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }
    }
}
