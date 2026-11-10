using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AvatarTextureStudio.Creative.Core
{
    internal sealed class ColorImage
    {
        public readonly int Width, Height;
        public readonly bool IsSRGB;
        readonly byte[] pixels;
        public ColorImage(int width, int height, bool srgb, byte[] rgba)
        {
            if (width < 1 || height < 1 || width > 8192 || height > 8192 || rgba.Length != (long)width * height * 4)
                throw new ArgumentException("Color images require RGBA8 pixels, dimensions 1–8192.");
            Width = width; Height = height; IsSRGB = srgb; pixels = (byte[])rgba.Clone();
        }
        public byte[] CopyPixels() => (byte[])pixels.Clone();
        public byte Channel(int pixel, int channel) => pixels[checked(pixel * 4 + channel)];
    }

    /// <summary>Immutable, bottom-left-origin binary pixels. Refinement never changes the baseline.</summary>
    internal sealed class RegionMask
    {
        public readonly int Width, Height;
        readonly byte[] bits;
        public int ByteCount => bits.Length;
        public RegionMask(int width, int height) : this(width, height, new byte[checked((width * height + 7) / 8)]) { }
        public RegionMask(int width, int height, byte[] data)
        {
            if (width < 1 || height < 1 || width > 8192 || height > 8192 || data.Length != ((long)width * height + 7) / 8)
                throw new ArgumentException("Invalid region mask dimensions or bit count.");
            Width = width; Height = height; bits = (byte[])data.Clone();
            int remainder = width * height % 8;
            if (remainder != 0 && (bits[bits.Length - 1] >> remainder) != 0) throw new ArgumentException("Nonzero mask padding bits.");
        }
        public bool Contains(int pixel) => (bits[pixel >> 3] & (1 << (pixel & 7))) != 0;
        public byte[] CopyBits() => (byte[])bits.Clone();
        static void Set(byte[] data, int pixel, bool add)
        {
            if (add) data[pixel >> 3] |= (byte)(1 << (pixel & 7));
            else data[pixel >> 3] &= (byte)~(1 << (pixel & 7));
        }
        public RegionMask Circle(float x, float y, float radius, bool add) => Stroke(new Vector2(x, y), new Vector2(x, y), radius, add);
        public RegionMask Stroke(Vector2 from, Vector2 to, float radius, bool add)
        {
            bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
            if (!Finite(from.X) || !Finite(from.Y) || !Finite(to.X) || !Finite(to.Y) || !Finite(radius) || radius < .5f || radius > 8192)
                throw new ArgumentException("Invalid mask brush.");
            byte[] next = CopyBits();
            Vector2 min = Vector2.Min(from, to) - new Vector2(radius), max = Vector2.Max(from, to) + new Vector2(radius), segment = to - from;
            float length = segment.LengthSquared();
            for (int py = Math.Max(0, (int)Math.Floor(min.Y)); py < Math.Min(Height, Math.Ceiling(max.Y)); py++)
            for (int px = Math.Max(0, (int)Math.Floor(min.X)); px < Math.Min(Width, Math.Ceiling(max.X)); px++)
            {
                var point = new Vector2(px + .5f, py + .5f);
                float t = length > 0 ? Math.Max(0, Math.Min(1, Vector2.Dot(point - from, segment) / length)) : 0;
                if (Vector2.DistanceSquared(point, from + t * segment) <= radius * radius) Set(next, py * Width + px, add);
            }
            return new RegionMask(Width, Height, next);
        }
        public RegionMask Triangles(IEnumerable<UvFragment> fragments, bool add)
        {
            byte[] next = CopyBits();
            foreach (var f in fragments)
            {
                Vector2 a = Vector2.Multiply(f.A.Uv, new Vector2(Width, Height));
                Vector2 b = Vector2.Multiply(f.B.Uv, new Vector2(Width, Height));
                Vector2 c = Vector2.Multiply(f.C.Uv, new Vector2(Width, Height));
                Vector2 min = Vector2.Min(a, Vector2.Min(b, c)), max = Vector2.Max(a, Vector2.Max(b, c));
                for (int y = Math.Max(0, (int)Math.Floor(min.Y)); y < Math.Min(Height, Math.Ceiling(max.Y)); y++)
                for (int x = Math.Max(0, (int)Math.Floor(min.X)); x < Math.Min(Width, Math.Ceiling(max.X)); x++)
                    if (UvLayout.Barycentric(new Vector2(x + .5f, y + .5f), a, b, c, out _)) Set(next, y * Width + x, add);
            }
            return new RegionMask(Width, Height, next);
        }
    }

    internal sealed class ColorRegion
    {
        public string Name = "Region";
        public RegionMask Mask;
        public bool Enabled = true, FacePreset;
        public float Hue, Chroma = 1, Strength = 1;
        public ColorRegion Clone() => (ColorRegion)MemberwiseClone();
    }

    internal sealed class CreativeBinding
    {
        public string Renderer = "", Material = "", Property = "_MainTex", Source = "";
        public int Slot, Channel;
        public Vector2 Scale = Vector2.One, Offset;
        public UvWrap WrapU, WrapV;
        public CreativeBinding Clone() => (CreativeBinding)MemberwiseClone();
    }

    internal sealed class CreativeDocument
    {
        public ColorImage Baseline;
        public CreativeBinding Binding = new CreativeBinding();
        public List<ColorRegion> Regions = new List<ColorRegion>();
        public int ActiveRegion = -1;
        public float Zoom = 1;
        public Vector2 Pan;
        public CreativeDocument Clone() => new CreativeDocument { Baseline = Baseline, Binding = Binding.Clone(),
            Regions = Regions.Select(r => r.Clone()).ToList(), ActiveRegion = ActiveRegion, Zoom = Zoom, Pan = Pan };
        public void Validate()
        {
            if (Baseline == null || Regions.Count > 32 || ActiveRegion < -1 || ActiveRegion >= Regions.Count
                || !Finite(Zoom) || Zoom < .25 || Zoom > 64 || !Finite(Pan.X) || !Finite(Pan.Y) || Math.Abs(Pan.X) > 1048576 || Math.Abs(Pan.Y) > 1048576)
                throw new ArgumentException("Invalid creative document or navigation.");
            if (Binding == null || Binding.Slot < 0 || Binding.Slot > 1023 || Binding.Channel < 0 || Binding.Channel > 7
                || !Enum.IsDefined(typeof(UvWrap), Binding.WrapU) || !Enum.IsDefined(typeof(UvWrap), Binding.WrapV)
                || !Finite(Binding.Scale.X) || !Finite(Binding.Scale.Y) || !Finite(Binding.Offset.X) || !Finite(Binding.Offset.Y))
                throw new ArgumentException("Invalid creative texture binding.");
            Text(Binding.Renderer); Text(Binding.Material); Text(Binding.Property); Text(Binding.Source);
            long bytes = (long)Baseline.Width * Baseline.Height * 4;
            foreach (var r in Regions)
            {
                Text(r.Name);
                if (r.Mask == null || r.Mask.Width != Baseline.Width || r.Mask.Height != Baseline.Height
                    || !Finite(r.Hue) || r.Hue < -180 || r.Hue > 180 || !Finite(r.Chroma) || r.Chroma < 0 || r.Chroma > 3
                    || !Finite(r.Strength) || r.Strength < 0 || r.Strength > 1) throw new ArgumentException("Invalid region mask or adjustment.");
                bytes += r.Mask.ByteCount;
            }
            if (bytes > 511L * 1024 * 1024) throw new ArgumentException("Creative pixels and masks exceed the 512 MiB decoded document limit (1 MiB is reserved for metadata). Remove a region or use a smaller baseline.");
        }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static void Text(string s) { if (s == null || s.Length > 4096) throw new ArgumentException("Creative text fields are limited to 4096 characters."); }
    }

    /// <summary>At most 24 steps. Retain up to 128 MiB, or one baseline's bytes (up to 256 MiB) so a large rebase stays undoable.</summary>
    internal sealed class CreativeHistory
    {
        readonly List<CreativeDocument> undo = new List<CreativeDocument>(), redo = new List<CreativeDocument>();
        public CreativeDocument Current { get; private set; }
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public CreativeHistory(CreativeDocument document) { document.Validate(); Current = document; }
        public void Edit(Action<CreativeDocument> action, bool recordHistory = true)
        {
            var next = Current.Clone(); action(next); next.Validate();
            if (recordHistory) undo.Add(Current);
            Current = next; redo.Clear(); Trim();
        }
        public void Undo() { if (CanUndo) { redo.Add(Current); Current = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); } }
        public void Redo() { if (CanRedo) { undo.Add(Current); Current = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1); } }
        void Trim()
        {
            long limit = Math.Max(128L * 1024 * 1024, (long)Current.Baseline.Width * Current.Baseline.Height * 4);
            while (undo.Count > 24 || (undo.Count > 0 && RetainedBytes() > limit)) undo.RemoveAt(0);
        }
        long RetainedBytes()
        {
            var images = new HashSet<ColorImage> { Current.Baseline };
            var masks = new HashSet<RegionMask>(Current.Regions.Select(r => r.Mask));
            long size = 0;
            foreach (var d in undo)
            {
                if (images.Add(d.Baseline)) size += (long)d.Baseline.Width * d.Baseline.Height * 4;
                foreach (var r in d.Regions) if (masks.Add(r.Mask)) size += r.Mask.ByteCount;
            }
            return size;
        }
    }
}
