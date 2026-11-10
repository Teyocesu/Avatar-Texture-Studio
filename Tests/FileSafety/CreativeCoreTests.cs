using System;
using System.Numerics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AvatarTextureStudio.Creative.Core;

internal static class CreativeCoreTests
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("CREATIVETEST PASS " + message);
    }

    static UvTriangle Triangle(int id, int slot = 0, int channel = 0) => new UvTriangle {
        Id = id, Slot = slot, Submesh = slot, Channel = channel, A = Vector3.Zero, B = Vector3.UnitX, C = Vector3.UnitY,
        U = Vector2.Zero, V = Vector2.UnitX, W = Vector2.UnitY };

    public static void Run()
    {
        var triangle = Triangle(4, 2, 7);
        var layout = new UvLayout(new[] { triangle }, new Vector2(2, 1), new Vector2(0.1f, 0), UvWrap.Repeat, UvWrap.Repeat);
        var forward = layout.FindNearestRay(new Vector3(0.2f, 0.3f, -2), Vector3.UnitZ, 2);
        Check(forward.Count == 1 && Vector2.Distance(forward[0].MeshUv, new Vector2(0.2f, 0.3f)) < 1e-5f
            && Vector2.Distance(forward[0].TextureUv, new Vector2(0.5f, 0.3f)) < 1e-5f,
            "ray-to-source UV and material scale/offset preserve barycentric correspondence");
        Check(layout.Triangles[forward[0].Triangle].Channel == 7 && layout.FindNearestRay(new Vector3(.2f,.3f,-2), Vector3.UnitZ, 0).Count == 0,
            "material-slot filtering and selected UV channel identity remain distinct");
        var reverse = layout.FindUv(new Vector2(.5f,.3f), true, 2);
        Check(reverse.Exists(h => Vector3.Distance(h.World, new Vector3(.2f,.3f,0)) < 1e-5f), "wrapped reverse lookup returns the forward surface");
        var duplicate = Triangle(5, 2, 7);
        var overlaps = new UvLayout(new[] { triangle, duplicate }, Vector2.One, Vector2.Zero, UvWrap.Repeat, UvWrap.Repeat);
        Check(overlaps.FindUv(new Vector2(.2f,.3f), true).Count == 2 && overlaps.FindNearestRay(new Vector3(.2f,.3f,-1), Vector3.UnitZ).Count == 2,
            "overlapping UVs and equal-depth surfaces retain every match");
        duplicate.A.Z = duplicate.B.Z = duplicate.C.Z = 2;
        var depth = new UvLayout(new[] { duplicate, triangle }, Vector2.One, Vector2.Zero, UvWrap.Repeat, UvWrap.Repeat);
        Check(depth.FindNearestRay(new Vector3(.2f,.3f,-1), Vector3.UnitZ).Count == 1
            && depth.FindNearestRay(new Vector3(.2f,.3f,-1), Vector3.UnitZ)[0].Triangle == 1, "nearest visible surface wins over farther geometry");
        var collapsed = Triangle(9); collapsed.V = collapsed.W = collapsed.U;
        var degenerate = new UvLayout(new[] { collapsed }, Vector2.One, Vector2.Zero, UvWrap.Repeat, UvWrap.Repeat);
        Check(degenerate.Collapsed == 1 && degenerate.FindUv(Vector2.Zero, true).Count == 0
            && degenerate.FindNearestRay(new Vector3(.2f,.3f,-1), Vector3.UnitZ).Count == 1,
            "collapsed UV triangles remain identifiable in 3D but are not false reverse matches");
        var moved = Triangle(10); moved.U.X -= 1; moved.V.X -= 1; moved.W.X -= 1;
        var mirrored = new UvLayout(new[] { moved }, Vector2.One, Vector2.Zero, UvWrap.Mirror, UvWrap.Repeat);
        Check(mirrored.FindUv(new Vector2(.8f,.2f), true).Exists(h => Math.Abs(h.MeshUv.X + .8f) < 1e-5f), "negative mirrored tile maps back to its actual source UV");
        var clamp = new UvLayout(new[] { moved }, Vector2.One, Vector2.Zero, UvWrap.Clamp, UvWrap.Repeat);
        Check(clamp.AmbiguousBorder(new Vector2(0,.2f)) && clamp.FindUv(new Vector2(.5f,.2f), true).Count == 0,
            "clamped border collapse is disclosed and does not fabricate an interior match");
        Check(Math.Abs(UvLayout.Wrap(-1.2f, UvWrap.Repeat) - .8f) < 1e-5f
            && Math.Abs(UvLayout.Wrap(-.2f, UvWrap.MirrorOnce) - .2f) < 1e-5f
            && UvLayout.Wrap(2f, UvWrap.MirrorOnce) == 1, "wrap semantics cover negative repeat and MirrorOnce clamping");
        var large = new UvLayout(new[] { triangle }, new Vector2(32,1), Vector2.Zero, UvWrap.Repeat, UvWrap.Repeat);
        Check(large.Unsupported.Count == 1 && large.Raw.Count == 1 && large.Sampled.Count == 0, "oversized tile spans remain raw-inspectable and never silently truncate correspondence");
        ColorAndPersistence();
    }

    static void ColorAndPersistence()
    {
        byte[] original = Enumerable.Range(0, 16).SelectMany(i => new byte[] { (byte)(40 + i * 9), (byte)(110 + i * 3), 80, (byte)(i * 17) }).ToArray();
        var doc = new CreativeDocument { Baseline = new ColorImage(4, 4, true, original), ActiveRegion = 0 };
        var triangle = new UvLayout(new[] { Triangle(0) }, Vector2.One, Vector2.Zero, UvWrap.Repeat, UvWrap.Repeat);
        var mask = new RegionMask(4, 4).Triangles(triangle.Sampled, true);
        Check(mask.Contains(0) && !mask.Contains(15), "triangle rasterization selects pixel centers without covering the opposite corner");
        var refined = mask.Circle(.5f, .5f, .6f, false);
        Check(mask.Contains(0) && !refined.Contains(0) && refined.Contains(1), "subtract brush refines an immutable mask without changing adjacent pixels");
        var stroke = new RegionMask(4, 4).Stroke(new Vector2(.5f, .5f), new Vector2(3.5f, 3.5f), .5f, true);
        Check(stroke.Contains(0) && stroke.Contains(5) && stroke.Contains(10) && stroke.Contains(15) && !stroke.Contains(3), "mask brush interpolates sparse pointer samples without gaps or unrelated corner coverage");
        doc.Regions.Add(new ColorRegion { Name = "Cheek / user named", FacePreset = true, Mask = mask, Hue = 75, Chroma = 1.2f, Strength = .8f });
        byte[] output = ColorwayProcessor.Render(doc);
        Check(Enumerable.Range(0, 16).All(p => output[p * 4 + 3] == original[p * 4 + 3]), "recoloring preserves every alpha byte including transparent pixels");
        Check(Enumerable.Range(0, 16).Where(p => !mask.Contains(p)).All(p => Enumerable.Range(0, 4).All(c => output[p * 4 + c] == original[p * 4 + c]))
            && !output.SequenceEqual(original), "recoloring changes selected RGB while unselected RGBA stays byte-identical");
        double Lightness(byte[] bytes, int pixel, bool srgb)
        {
            ColorwayProcessor.ToLab(ColorwayProcessor.Decode(bytes[pixel * 4] / 255d, srgb), ColorwayProcessor.Decode(bytes[pixel * 4 + 1] / 255d, srgb),
                ColorwayProcessor.Decode(bytes[pixel * 4 + 2] / 255d, srgb), out double l, out _, out _); return l;
        }
        Check(Enumerable.Range(0, 16).All(p => Math.Abs(Lightness(output, p, true) - Lightness(original, p, true)) < .005), "Oklab lightness remains within RGBA8 quantization tolerance after gamut reduction");
        ColorwayProcessor.ToLab(1, 0, 0, out double redL, out double redA, out double redB);
        Check(Math.Abs(redL - .62795536) < 1e-7 && Math.Abs(redA - .22486306) < 1e-7 && Math.Abs(redB - .12584630) < 1e-7,
            "Oklab conversion matches the published linear-red reference");
        var identity = doc.Clone(); identity.Regions[0].Strength = 0;
        Check(ColorwayProcessor.Render(identity).SequenceEqual(original), "zero-strength adjustments are exact no-ops");
        identity.Regions[0].Strength = 1; identity.Regions[0].Hue = 0; identity.Regions[0].Chroma = 1;
        Check(ColorwayProcessor.Render(identity).SequenceEqual(original), "identity hue/chroma is an exact no-op");
        var linear = doc.Clone(); linear.Baseline = new ColorImage(4, 4, false, original);
        byte[] linearResult = ColorwayProcessor.Render(linear);
        Check(!linearResult.SequenceEqual(output) && Enumerable.Range(0, 16).All(p => Math.Abs(Lightness(linearResult, p, false) - Lightness(original, p, false)) < .005),
            "linear sources bypass sRGB transfer and preserve their own lightness");
        bool cancelled = false; try { ColorwayProcessor.Render(doc, _ => true); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && doc.Baseline.CopyPixels().SequenceEqual(original), "cancelled preview leaves the immutable source intact");
        var history = new CreativeHistory(doc);
        history.Edit(d => { d.Regions[0].Mask = refined; d.Regions[0].Hue = -20; });
        history.Undo(); Check(history.Current.Regions[0].Mask.Contains(0) && history.Current.Regions[0].Hue == 75, "undo restores mask and editable adjustment together");
        history.Redo(); Check(!history.Current.Regions[0].Mask.Contains(0) && history.Current.Regions[0].Hue == -20, "redo restores mask refinement and adjustment");
        string dir = Path.Combine(Path.GetTempPath(), "ats-creative-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "project.atsstudio");
            doc.Binding.Renderer = "Avatar/Body"; doc.Binding.Channel = 7; doc.Binding.Slot = 2; doc.Binding.WrapU = UvWrap.Mirror; doc.Zoom = 3;
            CreativeProjectIO.Save(path, doc, false);
            var loaded = CreativeProjectIO.Load(path);
            Check(loaded.Binding.Channel == 7 && loaded.Binding.Slot == 2 && loaded.Binding.Renderer == "Avatar/Body" && loaded.Binding.WrapU == UvWrap.Mirror && loaded.Zoom == 3
                && loaded.Regions[0].FacePreset && loaded.Regions[0].Name == doc.Regions[0].Name && loaded.Regions[0].Mask.CopyBits().SequenceEqual(mask.CopyBits())
                && ColorwayProcessor.Render(loaded).SequenceEqual(output), "sidecar round trip preserves pixels, editable masks, adjustments, presets, binding and navigation");
            byte[] valid = File.ReadAllBytes(path);
            void Reject(byte[] bytes, string reason)
            {
                File.WriteAllBytes(path, bytes); bool rejected = false;
                try { CreativeProjectIO.Load(path); } catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException) { rejected = true; }
                Check(rejected, reason);
            }
            var magic = (byte[])valid.Clone(); magic[0] = 0; Reject(magic, "invalid creative magic is rejected");
            var version = (byte[])valid.Clone(); version[8] = 99; Reject(version, "future creative versions fail explicitly");
            Reject(valid.Take(valid.Length / 2).ToArray(), "truncated compressed creative data is rejected");
            Reject(valid.Take(valid.Length - 4).ToArray(), "missing gzip footer bytes are rejected even when the editable payload is complete");
            byte[] decoded;
            using (var input = new MemoryStream(valid, 20, valid.Length - 20))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var body = new MemoryStream()) { gzip.CopyTo(body); decoded = body.ToArray(); }
            byte[] Encode(byte[] body)
            {
                using (var output = new MemoryStream())
                {
                    output.Write(valid, 0, 20);
                    using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(body, 0, body.Length);
                    byte[] bytes = output.ToArray(); BitConverter.GetBytes((long)bytes.Length - 20).CopyTo(bytes, 12); return bytes;
                }
            }
            var dimension = (byte[])decoded.Clone(); BitConverter.GetBytes(int.MaxValue).CopyTo(dimension, 0);
            Reject(Encode(dimension), "hostile creative dimensions are rejected before allocating pixels");
            var textLength = (byte[])decoded.Clone(); BitConverter.GetBytes(16385).CopyTo(textLength, 9);
            Reject(Encode(textLength), "oversized creative text declarations are rejected before allocating strings");
            Reject(Encode(decoded.Concat(new byte[] { 1 }).ToArray()), "unrecognized decoded trailing data is rejected");
            var invalid = doc.Clone(); invalid.Regions[0].Hue = float.NaN;
            File.WriteAllBytes(path, valid); bool refused = false;
            try { CreativeProjectIO.Save(path, invalid, true); } catch (ArgumentException) { refused = true; }
            Check(refused && File.ReadAllBytes(path).SequenceEqual(valid), "invalid adjustment cannot replace a valid creative project");
        }
        finally { Directory.Delete(dir, true); }
    }
}
