using System;
using System.Collections.Generic;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.Painting;
using UnityEngine;
using UnityEngine.Rendering;
using N2 = System.Numerics.Vector2;
using N3 = System.Numerics.Vector3;

namespace AvatarTextureStudio.Creative
{
    /// <summary>Source UVs and posed vertices only. Does not bake padding, change a mesh or allocate paint targets.</summary>
    internal sealed class CreativeMeshAdapter : IDisposable
    {
        readonly PaintTarget pose;
        readonly N2 scale, offset;
        int poseHash;
        public UvLayout Layout { get; private set; }
        public CreativeMeshAdapter(Renderer renderer, int slot, int channel, string property, N2 scale, N2 offset, TextureWrapMode u, TextureWrapMode v)
        {
            pose = new PaintTarget(renderer, new[] { slot }, channel, property, u, v); this.scale = scale; this.offset = offset;
            Refresh(true);
        }
        public bool Refresh(bool force = false)
        {
            int hash = pose.ComputePoseHash();
            if (!force && Layout != null && hash == poseHash) return false;
            var source = PaintTarget.GetSourceMesh(pose.Renderer);
            if (source == null) throw new InvalidOperationException("Select a MeshRenderer or SkinnedMeshRenderer with a source mesh.");
            int submesh = PaintTarget.SubmeshForSlot(source, pose.Slots[0]);
            if (source.subMeshCount == 0 || source.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("This material slot does not reference triangle geometry.");
            var uvs = new List<Vector2>(); source.GetUVs(pose.UVChannel, uvs);
            if (uvs.Count != source.vertexCount) throw new InvalidOperationException($"The source mesh has no complete UV{pose.UVChannel} channel.");
            Mesh baked = null;
            try
            {
                var matrix = pose.Renderer.localToWorldMatrix;
                if (pose.Renderer is SkinnedMeshRenderer skin)
                {
                    baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    skin.BakeMesh(baked, true); matrix = PaintTarget.ResolveBakeMatrix(skin, source, baked);
                }
                var positions = new List<Vector3>(); (baked != null ? baked : source).GetVertices(positions);
                var indices = source.GetTriangles(submesh);
                if (positions.Count != source.vertexCount) throw new InvalidOperationException("Baked/source vertex counts disagree; correspondence is unavailable.");
                if (indices.Length / 3 > 250000) throw new InvalidOperationException("UV inspection is limited to 250,000 triangles per material slot; select a smaller source mesh.");
                var triangles = new UvTriangle[indices.Length / 3];
                for (int i = 0; i < triangles.Length; i++)
                {
                    int a = indices[i * 3], b = indices[i * 3 + 1], c = indices[i * 3 + 2];
                    triangles[i] = new UvTriangle { Id = i, Submesh = submesh, Slot = pose.Slots[0], Channel = pose.UVChannel,
                        A = ToCore(matrix.MultiplyPoint3x4(positions[a])), B = ToCore(matrix.MultiplyPoint3x4(positions[b])), C = ToCore(matrix.MultiplyPoint3x4(positions[c])),
                        U = ToCore(uvs[a]), V = ToCore(uvs[b]), W = ToCore(uvs[c]) };
                }
                Layout = new UvLayout(triangles, scale, offset, (UvWrap)pose.WrapU, (UvWrap)pose.WrapV);
                poseHash = hash; return true;
            }
            finally { if (baked != null) UnityEngine.Object.DestroyImmediate(baked); }
        }
        public static N2 ToCore(Vector2 v) => new N2(v.x, v.y);
        public static N3 ToCore(Vector3 v) => new N3(v.x, v.y, v.z);
        public static Vector2 ToUnity(N2 v) => new Vector2(v.X, v.Y);
        public static Vector3 ToUnity(N3 v) => new Vector3(v.X, v.Y, v.Z);
        public void Dispose() => pose.Dispose();
    }
}
