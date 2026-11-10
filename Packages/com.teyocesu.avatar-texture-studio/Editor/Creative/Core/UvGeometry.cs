using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AvatarTextureStudio.Creative.Core
{
    internal enum UvWrap { Repeat, Clamp, Mirror, MirrorOnce }

    internal struct UvTriangle
    {
        public int Id, Submesh, Slot, Channel;
        public Vector3 A, B, C;
        public Vector2 U, V, W;
        public Vector3 Point(Vector3 weights) => A * weights.X + B * weights.Y + C * weights.Z;
        public Vector2 MeshUv(Vector3 weights) => U * weights.X + V * weights.Y + W * weights.Z;
    }

    internal struct UvVertex
    {
        public Vector2 Uv;
        public Vector3 Weights;
        public UvVertex(Vector2 uv, Vector3 weights) { Uv = uv; Weights = weights; }
    }

    internal sealed class UvFragment
    {
        public int Triangle, TileX, TileY;
        public UvVertex A, B, C;
    }

    internal struct SurfaceHit
    {
        public int Triangle, TileX, TileY;
        public Vector3 Weights, World;
        public Vector2 MeshUv, TextureUv;
        public float Distance;
    }

    /// <summary>Read-only source triangles, preserving all reverse matches and wrap aliases.</summary>
    internal sealed class UvLayout
    {
        public readonly UvTriangle[] Triangles;
        public readonly Vector2 Scale, Offset;
        public readonly UvWrap WrapU, WrapV;
        public readonly List<UvFragment> Raw = new List<UvFragment>();
        public readonly List<UvFragment> Sampled = new List<UvFragment>();
        public readonly HashSet<int> Unsupported = new HashSet<int>();
        public readonly HashSet<int> Rasterizable = new HashSet<int>();
        public int Collapsed { get; private set; }
        public int BorderCollapsed { get; private set; }
        const float Epsilon = 1e-6f;

        public UvLayout(IEnumerable<UvTriangle> triangles, Vector2 scale, Vector2 offset, UvWrap wrapU, UvWrap wrapV)
        {
            if (!Finite(scale) || !Finite(offset) || !Enum.IsDefined(typeof(UvWrap), wrapU) || !Enum.IsDefined(typeof(UvWrap), wrapV))
                throw new ArgumentException("Invalid UV transform or wrapping.");
            Triangles = triangles.ToArray(); Scale = scale; Offset = offset; WrapU = wrapU; WrapV = wrapV;
            for (int i = 0; i < Triangles.Length; i++)
            {
                var t = Triangles[i];
                if (!Finite(t.U) || !Finite(t.V) || !Finite(t.W) || !Finite(t.A) || !Finite(t.B) || !Finite(t.C))
                    throw new ArgumentException("Mesh positions and UVs must be finite.");
                var a = new UvVertex(Transform(t.U), Vector3.UnitX);
                var b = new UvVertex(Transform(t.V), Vector3.UnitY);
                var c = new UvVertex(Transform(t.W), Vector3.UnitZ);
                if (!Finite(a.Uv) || !Finite(b.Uv) || !Finite(c.Uv)) throw new ArgumentException("Material scale/offset overflows the source UV coordinates.");
                Raw.Add(new UvFragment { Triangle = i, A = a, B = b, C = c });
                if (Math.Abs(Cross(b.Uv - a.Uv, c.Uv - a.Uv)) < 1e-12f) { Collapsed++; continue; }
                if (Sampled.Count >= 1000000) { Unsupported.Add(i); continue; }
                Vector2 min = Vector2.Min(a.Uv, Vector2.Min(b.Uv, c.Uv));
                Vector2 max = Vector2.Max(a.Uv, Vector2.Max(b.Uv, c.Uv));
                if (HasCollapsedBorder(min.X, max.X, wrapU) || HasCollapsedBorder(min.Y, max.Y, wrapV)) BorderCollapsed++;
                if (!Tiles(min.X, max.X, wrapU, out int x0, out int x1)
                    || !Tiles(min.Y, max.Y, wrapV, out int y0, out int y1)) { Unsupported.Add(i); continue; }
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var polygon = new List<UvVertex> { a, b, c };
                    polygon = Clip(polygon, 0, x, true); polygon = Clip(polygon, 0, x + 1, false);
                    polygon = Clip(polygon, 1, y, true); polygon = Clip(polygon, 1, y + 1, false);
                    for (int n = 1; n + 1 < polygon.Count; n++)
                    {
                        UvVertex Map(UvVertex v) => new UvVertex(new Vector2(MapTile(v.Uv.X, x, wrapU), MapTile(v.Uv.Y, y, wrapV)), v.Weights);
                        var fragment = new UvFragment { Triangle = i, TileX = x, TileY = y, A = Map(polygon[0]), B = Map(polygon[n]), C = Map(polygon[n + 1]) };
                        if (Math.Abs(Cross(fragment.B.Uv - fragment.A.Uv, fragment.C.Uv - fragment.A.Uv)) > 1e-12f)
                        { Sampled.Add(fragment); Rasterizable.Add(i); }
                    }
                }
            }
        }

        public Vector2 Transform(Vector2 uv) => Vector2.Multiply(uv, Scale) + Offset;
        public Vector2 Wrapped(Vector2 uv) => new Vector2(Wrap(uv.X, WrapU), Wrap(uv.Y, WrapV));

        public bool AmbiguousBorder(Vector2 uv) => BorderCollapsed > 0 &&
            ((WrapU == UvWrap.Clamp || WrapU == UvWrap.MirrorOnce) && (uv.X <= Epsilon || uv.X >= 1 - Epsilon)
            || (WrapV == UvWrap.Clamp || WrapV == UvWrap.MirrorOnce) && (uv.Y <= Epsilon || uv.Y >= 1 - Epsilon));

        public List<SurfaceHit> FindUv(Vector2 uv, bool sampled, int slot = -1)
        {
            var hits = new List<SurfaceHit>();
            foreach (var f in sampled ? Sampled : Raw)
            {
                var triangle = Triangles[f.Triangle];
                if (slot >= 0 && triangle.Slot != slot) continue;
                if (!Barycentric(uv, f.A.Uv, f.B.Uv, f.C.Uv, out var bary)) continue;
                Vector3 weights = f.A.Weights * bary.X + f.B.Weights * bary.Y + f.C.Weights * bary.Z;
                if (hits.Any(h => h.Triangle == f.Triangle && Vector3.DistanceSquared(h.Weights, weights) < 1e-10f)) continue;
                hits.Add(Hit(f.Triangle, weights, 0, f.TileX, f.TileY));
            }
            return hits;
        }

        public List<SurfaceHit> FindNearestRay(Vector3 origin, Vector3 direction, int slot = -1)
        {
            if (!Finite(origin) || !Finite(direction) || direction.LengthSquared() < 1e-12f) throw new ArgumentException("Invalid ray.");
            direction = Vector3.Normalize(direction);
            var hits = new List<SurfaceHit>();
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < Triangles.Length; i++)
            {
                var t = Triangles[i];
                if (slot >= 0 && t.Slot != slot) continue;
                Vector3 edge1 = t.B - t.A, edge2 = t.C - t.A;
                Vector3 p = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Math.Abs(determinant) < 1e-10f) continue;
                Vector3 s = origin - t.A;
                float u = Vector3.Dot(s, p) / determinant;
                Vector3 q = Vector3.Cross(s, edge1);
                float v = Vector3.Dot(direction, q) / determinant;
                float distance = Vector3.Dot(edge2, q) / determinant;
                if (u < -Epsilon || v < -Epsilon || u + v > 1 + Epsilon || distance < 0) continue;
                float tolerance = 1e-5f * Math.Max(1, Math.Min(nearest, distance));
                if (distance < nearest - tolerance) { nearest = distance; hits.Clear(); }
                if (Math.Abs(distance - nearest) <= tolerance) hits.Add(Hit(i, new Vector3(1 - u - v, u, v), distance, 0, 0));
            }
            return hits;
        }

        SurfaceHit Hit(int triangle, Vector3 weights, float distance, int x, int y)
        {
            var t = Triangles[triangle];
            Vector2 uv = t.MeshUv(weights);
            return new SurfaceHit { Triangle = triangle, Weights = weights, MeshUv = uv, TextureUv = Transform(uv),
                World = t.Point(weights), Distance = distance, TileX = x, TileY = y };
        }

        public static bool Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights)
        {
            float area = Cross(b - a, c - a);
            weights = default;
            if (Math.Abs(area) < 1e-12f) return false;
            float u = Cross(p - a, c - a) / area;
            float v = Cross(b - a, p - a) / area;
            weights = new Vector3(1 - u - v, u, v);
            return weights.X >= -Epsilon && u >= -Epsilon && v >= -Epsilon;
        }

        public static float Wrap(float value, UvWrap mode)
        {
            switch (mode)
            {
                case UvWrap.Clamp: return Math.Max(0, Math.Min(1, value));
                case UvWrap.MirrorOnce: return Math.Min(1, Math.Abs(value));
                case UvWrap.Mirror:
                    float p = value - 2 * (float)Math.Floor(value / 2);
                    return p <= 1 ? p : 2 - p;
                default: return value - (float)Math.Floor(value);
            }
        }

        static bool Tiles(float min, float max, UvWrap mode, out int first, out int last)
        {
            first = last = 0;
            if (mode == UvWrap.Clamp) return true;
            if (Math.Abs(min) > 1048576 || Math.Abs(max) > 1048576) return false;
            first = (int)Math.Floor(min + Epsilon); last = Math.Max(first, (int)Math.Floor(max - Epsilon));
            if (mode == UvWrap.MirrorOnce) { first = Math.Max(-1, first); last = Math.Min(0, last); }
            return last - first + 1 <= 16;
        }

        static bool HasCollapsedBorder(float min, float max, UvWrap mode) => mode == UvWrap.Clamp && (min < 0 || max > 1)
            || mode == UvWrap.MirrorOnce && (min < -1 || max > 1);
        static float MapTile(float value, int tile, UvWrap mode) => mode == UvWrap.Mirror ? ((tile & 1) == 0 ? value - tile : tile + 1 - value)
            : mode == UvWrap.MirrorOnce ? Math.Abs(value) : value - tile;
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Finite(Vector2 v) => Finite(v.X) && Finite(v.Y);
        static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);

        static List<UvVertex> Clip(List<UvVertex> input, int axis, float bound, bool minimum)
        {
            var output = new List<UvVertex>();
            if (input.Count == 0) return output;
            float Distance(UvVertex v) => (axis == 0 ? v.Uv.X : v.Uv.Y) - bound;
            var previous = input[input.Count - 1]; float pd = Distance(previous);
            bool pi = minimum ? pd >= 0 : pd <= 0;
            foreach (var current in input)
            {
                float cd = Distance(current); bool ci = minimum ? cd >= 0 : cd <= 0;
                if (pi != ci)
                {
                    float t = pd / (pd - cd);
                    output.Add(new UvVertex(Vector2.Lerp(previous.Uv, current.Uv, t), Vector3.Lerp(previous.Weights, current.Weights, t)));
                }
                if (ci) output.Add(current);
                previous = current; pd = cd; pi = ci;
            }
            return output;
        }
    }
}
