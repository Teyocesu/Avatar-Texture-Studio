using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AvatarTextureStudio.Creative.Core;

namespace AvatarTextureStudio.Quality.Core
{
    internal sealed class SeamEdge
    {
        public int Triangle, Submesh, Slot, Channel, StartVertex, EndVertex;
        public Vector3 SourceStart, SourceEnd, SourceThird, WorldStart, WorldEnd, Normal;
        public Vector2 Start, End, Third;
        public bool Forward;
        public bool Unsupported;
        public SeamEdge Reversed()
        {
            var e = (SeamEdge)MemberwiseClone();
            e.StartVertex = EndVertex; e.EndVertex = StartVertex; e.SourceStart = SourceEnd; e.SourceEnd = SourceStart;
            e.WorldStart = WorldEnd; e.WorldEnd = WorldStart; e.Start = End; e.End = Start; e.Forward = !Forward; return e;
        }
    }
    internal sealed class SeamPair { public SeamEdge A, B; }
    internal sealed class SeamTopology
    {
        public readonly List<SeamPair> Pairs = new List<SeamPair>();
        public readonly SortedDictionary<string, int> Excluded = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public int Triangles, OpenBoundaries, ContinuousEdges;
        public void Reject(string reason) { Excluded.TryGetValue(reason, out int count); Excluded[reason] = count + 1; }
    }

    internal static class SeamGeometry
    {
        public const int MaxTriangles = 250000, MaxPairs = 10000;
        readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            readonly Vector3 a, b;
            public EdgeKey(Vector3 x, Vector3 y) { bool forward = Compare(x, y) <= 0; a = forward ? x : y; b = forward ? y : x; }
            public bool Equals(EdgeKey other) => a == other.a && b == other.b;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked(a.GetHashCode() * 397 ^ b.GetHashCode());
        }
        public static SeamTopology Find(UvTriangle[] triangles, int slot, int channel, Vector2 scale, Vector2 offset, UvWrap wrapU, UvWrap wrapV, Func<float, bool> cancel = null)
        {
            if (triangles.Length > MaxTriangles) throw new ArgumentException("Seam topology is limited to 250,000 source triangles. Select a smaller mesh.");
            var result = new SeamTopology { Triangles = triangles.Length };
            var groups = new Dictionary<EdgeKey, List<(SeamEdge edge, int triangle)>>();
            var indexed = new Dictionary<(int, int), List<(int triangle, int start, int end)>>();
            var vertices = new Dictionary<int, (Vector3 position, Vector2 uv)>();
            int[] parent = Enumerable.Range(0, triangles.Length).ToArray();
            int Root(int n) { while (parent[n] != n) { parent[n] = parent[parent[n]]; n = parent[n]; } return n; }
            for (int i = 0; i < triangles.Length; i++)
            {
                if ((i & 1023) == 0) CheckCancel(cancel, (float)i / Math.Max(1, triangles.Length) * .5f);
                var t = triangles[i];
                int[] ids = { t.IndexA, t.IndexB, t.IndexC };
                Vector3[] rest = { t.SourceA, t.SourceB, t.SourceC }, world = { t.A, t.B, t.C };
                Vector2[] uv = { t.U, t.V, t.W };
                if (t.Channel != channel) throw new ArgumentException("Source topology mixes UV channels.");
                for (int k = 0; k < 3; k++)
                {
                    if (ids[k] < 0 || !Finite(rest[k]) || !Finite(world[k]) || !Finite(uv[k])) throw new ArgumentException("Source topology requires finite positions/UVs and original vertex indices.");
                    if (vertices.TryGetValue(ids[k], out var previous) && (previous.position != rest[k] || previous.uv != uv[k]))
                        throw new ArgumentException("A source vertex has inconsistent position/UV data. Correspondence is unavailable.");
                    vertices[ids[k]] = (rest[k], uv[k]); uv[k] = Vector2.Multiply(uv[k], scale) + offset;
                    if (!Finite(uv[k])) throw new ArgumentException("Material UV transform overflows source coordinates.");
                }
                var normal = Vector3.Cross(rest[1] - rest[0], rest[2] - rest[0]);
                if (!Finite(normal)) throw new ArgumentException("Source geometry overflows the diagnostic precision range.");
                if (ids.Distinct().Count() != 3 || normal.LengthSquared() < 1e-20f || Vector3.Cross(world[1] - world[0], world[2] - world[0]).LengthSquared() < 1e-20f)
                { result.Reject("Degenerate source/posed triangle"); continue; }
                for (int k = 0; k < 3; k++)
                {
                    int n = (k + 1) % 3, c = (k + 2) % 3;
                    var edge = new SeamEdge { Triangle = t.Id, Submesh = t.Submesh, Slot = t.Slot, Channel = t.Channel, StartVertex = ids[k], EndVertex = ids[n],
                        SourceStart = rest[k], SourceEnd = rest[n], SourceThird = rest[c], WorldStart = world[k], WorldEnd = world[n], Normal = Vector3.Normalize(normal),
                        Start = uv[k], End = uv[n], Third = uv[c], Forward = Compare(rest[k], rest[n]) <= 0, Unsupported = t.UnsupportedForDiagnostics };
                    var key = new EdgeKey(rest[k], rest[n]);
                    if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new List<(SeamEdge, int)>());
                    group.Add((edge, i));
                    var indexKey = (Math.Min(ids[k], ids[n]), Math.Max(ids[k], ids[n]));
                    if (!indexed.TryGetValue(indexKey, out var indexGroup)) indexed.Add(indexKey, indexGroup = new List<(int, int, int)>());
                    indexGroup.Add((i, ids[k], ids[n]));
                }
            }
            // Only indexed manifold edge adjacency establishes a connected component. Position welding does not.
            foreach (var group in indexed.Values)
                if (group.Count == 2 && group[0].start == group[1].end && group[0].end == group[1].start)
                    parent[Root(group[1].triangle)] = Root(group[0].triangle);
            int visited = 0;
            foreach (var group in groups.Values)
            {
                if ((visited++ & 1023) == 0) CheckCancel(cancel, .5f + .5f * visited / Math.Max(1, groups.Count));
                if (!group.Any(e => e.edge.Slot == slot)) continue;
                if (group.Count == 1) { result.OpenBoundaries++; continue; }
                if (group.Count != 2) { result.Reject("Nonunique/nonmanifold coincident edge"); continue; }
                var a = group[0].edge; var b = group[1].edge;
                if (a.Forward == b.Forward || a.SourceThird == b.SourceThird || Vector3.Dot(a.Normal, b.Normal) < -.99999f)
                { result.Reject("Duplicate, overlapping or incompatible edge orientation"); continue; }
                b = b.Reversed();
                if (Root(group[0].triangle) != Root(group[1].triangle)) { result.Reject("Disconnected topology: coincidence alone is uncertain"); continue; }
                if (a.Slot != slot || b.Slot != slot || a.Submesh != b.Submesh) { result.Reject("Material/submesh boundary or ambiguous slot association"); continue; }
                float tolerance = 1e-5f * Math.Max(1, Vector3.Distance(a.WorldStart, a.WorldEnd));
                if (Vector3.DistanceSquared(a.WorldStart, b.WorldStart) > tolerance * tolerance || Vector3.DistanceSquared(a.WorldEnd, b.WorldEnd) > tolerance * tolerance)
                { result.Reject("Posed edges no longer coincide"); continue; }
                if (Collapsed(a) || Collapsed(b)) { result.Reject("Collapsed UV triangle"); continue; }
                if (a.Unsupported || b.Unsupported || Unsupported(a, wrapU, wrapV) || Unsupported(b, wrapU, wrapV)) { result.Reject("UV fragment/tile span, coordinate limit or clamped-tail ambiguity"); continue; }
                if (Vector2.DistanceSquared(a.Start, b.Start) < 1e-12f && Vector2.DistanceSquared(a.End, b.End) < 1e-12f)
                { result.ContinuousEdges++; continue; }
                if (result.Pairs.Count == MaxPairs) throw new ArgumentException("More than 10,000 seam candidates. Use a smaller mesh; no partial result was substituted.");
                result.Pairs.Add(new SeamPair { A = a, B = b });
            }
            result.Pairs.Sort((a, b) => a.A.Triangle != b.A.Triangle ? a.A.Triangle.CompareTo(b.A.Triangle) : a.B.Triangle.CompareTo(b.B.Triangle));
            return result;
        }
        static bool Collapsed(SeamEdge e) => Math.Abs(Cross(e.End - e.Start, e.Third - e.Start)) < 1e-12f;
        static bool Unsupported(SeamEdge e, UvWrap u, UvWrap v)
        {
            var min = Vector2.Min(e.Start, Vector2.Min(e.End, e.Third)); var max = Vector2.Max(e.Start, Vector2.Max(e.End, e.Third));
            bool Axis(float a, float b, UvWrap mode) => Math.Abs(a) > 1048576 || Math.Abs(b) > 1048576
                || (mode == UvWrap.Clamp ? a < 0 || b > 1 : mode == UvWrap.MirrorOnce ? a < -1 || b > 1 : Math.Ceiling(b) - Math.Floor(a) > 16);
            return Axis(min.X, max.X, u) || Axis(min.Y, max.Y, v);
        }
        static int Compare(Vector3 a, Vector3 b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z);
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector2 v) => Finite(v.X) && Finite(v.Y);
        static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        internal static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        internal static void CheckCancel(Func<float, bool> cancel, float progress)
        { if (cancel != null && cancel(Math.Max(0, Math.Min(1, progress)))) throw new OperationCanceledException("Diagnostics cancelled; the previous snapshot and editable work were retained."); }
    }
}
