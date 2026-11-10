using System;
using System.Linq;
using System.IO;
using System.Numerics;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.Quality.Core;

internal static class QualityCoreTests
{
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("QUALITYTEST PASS " + message); }
    static UvTriangle Triangle(int id, int a, int b, int c, Vector3[] p, Vector2[] uv) => new UvTriangle {
        Id = id, IndexA = a, IndexB = b, IndexC = c, A = p[a], B = p[b], C = p[c], SourceA = p[a], SourceB = p[b], SourceC = p[c],
        U = uv[a], V = uv[b], W = uv[c], Slot = 0, Submesh = 0, Channel = 7 };
    internal static UvTriangle[] Cylinder(bool reverseUv = false)
    {
        Vector3[] ring = { new Vector3(1,0,0), new Vector3(0,1,0), new Vector3(-1,0,0), new Vector3(0,-1,0), new Vector3(1,0,0) };
        var p = new Vector3[10]; var uv = new Vector2[10];
        for (int i = 0; i < 5; i++) for (int h = 0; h < 2; h++) { p[i * 2 + h] = ring[i] + Vector3.UnitZ * h; uv[i * 2 + h] = new Vector2(i / 4f, reverseUv && i == 4 ? 1 - h : h); }
        return Enumerable.Range(0, 8).Select(i => { int col = i / 2; return (i & 1) == 0 ? Triangle(i, col * 2, col * 2 + 2, col * 2 + 1, p, uv)
            : Triangle(i, col * 2 + 2, col * 2 + 3, col * 2 + 1, p, uv); }).ToArray();
    }
    static SeamTopology Find(UvTriangle[] t, UvWrap u = UvWrap.Repeat) => SeamGeometry.Find(t, 0, 7, Vector2.One, Vector2.Zero, u, UvWrap.Clamp);
    static ColorImage Image(int width, int height, bool srgb, Func<int,int,byte[]> pixel) => new ColorImage(width, height, srgb,
        Enumerable.Range(0, width * height).SelectMany(i => pixel(i % width, i / width)).ToArray());
    public static void Run()
    {
        var triangles = Cylinder(); var topology = Find(triangles);
        Check(topology.Pairs.Count == 1 && topology.Pairs[0].A.Channel == 7 && topology.Pairs[0].A.StartVertex != topology.Pairs[0].B.StartVertex,
            "indexed-connected cylinder identifies one UV-split seam and retains original vertex/channel identity");
        var pair = topology.Pairs[0];
        Check(pair.A.SourceStart == pair.B.SourceStart && pair.A.SourceEnd == pair.B.SourceEnd, "reversed geometric edge directions align corresponding endpoints");
        var p = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.Zero, Vector3.UnitX, -Vector3.UnitY };
        var uv = new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, new Vector2(0,1), new Vector2(1,1), Vector2.One };
        var disconnected = Find(new[] { Triangle(0,0,1,2,p,uv), Triangle(1,4,3,5,p,uv) });
        Check(disconnected.Pairs.Count == 0 && disconnected.Excluded.Keys.Any(k => k.Contains("Disconnected")), "spatially coincident disconnected pieces are uncertain, never invented seam relationships");
        var slots = Cylinder(); slots[7].Slot = 1; slots[7].Submesh = 1;
        Check(Find(slots).Pairs.Count == 0, "material/submesh boundaries are not measured using a single texture");
        var mixed = Cylinder(); mixed[0].Channel = 0; bool rejected = false;
        try { Find(mixed); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "mixed UV-channel topology is rejected rather than combined");
        var mirror = Cylinder(); for (int i = 0; i < mirror.Length; i++) { mirror[i].U.X = -mirror[i].U.X; mirror[i].V.X = -mirror[i].V.X; mirror[i].W.X = -mirror[i].W.X; }
        Check(Find(mirror, UvWrap.Mirror).Pairs.Count == 1, "mirrored UV orientation does not erase valid geometric seam identity");
        var unsupported = Cylinder(); unsupported[7].UnsupportedForDiagnostics = true;
        Check(Find(unsupported).Pairs.Count == 0, "existing UV fragment/tile rejection remains a diagnostic rejection");
        var solid = Image(16,16,true, (_,__) => new byte[] { 100, 140, 80, 255 }); byte[] preserved = solid.CopyPixels();
        var continuous = SeamAnalyzer.Analyze(topology, solid, false, UvWrap.Repeat, UvWrap.Clamp).Single();
        Check(continuous.Samples > 0 && continuous.MaxDifference == 0 && continuous.MaxDeltaE100 == 0, "constant continuous color yields measured zero discontinuity");
        var imprecise = SeamGeometry.Find(Cylinder(),0,7,Vector2.One,new Vector2(1000000),UvWrap.Repeat,UvWrap.Repeat);
        var precision = SeamAnalyzer.Analyze(imprecise,solid,false,UvWrap.Repeat,UvWrap.Repeat).Single();
        Check(precision.Samples == 0 && precision.Unresolved > 0, "UV precision that cannot resolve a half-texel inset remains unmeasured");
        var hard = Image(16,16,true, (x,y) => x < 8 ? new byte[] {255,0,0,255} : new byte[] {0,0,255,255});
        var deliberate = SeamAnalyzer.Analyze(topology, hard, false, UvWrap.Repeat, UvWrap.Clamp).Single();
        Check(deliberate.MaxDifference > .5 && deliberate.MaxDeltaE100 > 10 && deliberate.PotentialContrast > 0,
            "deliberate hard transitions report measured contrast and potential concerns, not a visual-error verdict");
        var reversed = Image(16,16,false, (x,y) => new byte[] { (byte)((x < 8 ? y : 15-y) * 16), 0, 0, 255 });
        var reverseResult = SeamAnalyzer.Analyze(Find(Cylinder(true)), reversed, false, UvWrap.Repeat, UvWrap.Clamp).Single();
        Check(reverseResult.MaxDifference < 1e-6, "reversed UV edge direction samples matching geometric heights");
        var alpha = Image(16,16,true, (x,y) => new byte[] {100,100,100,(byte)(x < 8 ? 255 : 128)});
        Check(SeamAnalyzer.Analyze(topology, alpha, false, UvWrap.Repeat, UvWrap.Clamp).Single().MaxAlpha > .49, "alpha-only differences remain explicit");
        var transparent = Image(16,16,true, (x,y) => new byte[] {(byte)(x < 8 ? 255 : 0),0,255,0});
        var hidden = SeamAnalyzer.Analyze(topology, transparent, false, UvWrap.Repeat, UvWrap.Clamp).Single();
        Check(hidden.VisibleColorSamples == 0 && hidden.MaxDifference == 0, "both-transparent hidden RGB is not called a visible color discontinuity");
        Check(SeamAnalyzer.Analyze(topology, transparent, true, UvWrap.Repeat, UvWrap.Clamp).Single().MaxDifference > .9, "raw data mode measures stored channels independently of alpha");
        var blackWhite = Image(2,1,true, (x,y) => new byte[] {(byte)(x * 255),(byte)(x * 255),(byte)(x * 255),255});
        var colorMips = DiagnosticMips.Build(blackWhite, false); var dataMips = DiagnosticMips.Build(blackWhite, true);
        Check(colorMips.Count == 2 && colorMips[1].Channel(0,0) == 188 && dataMips[1].Channel(0,0) == 128, "color mips average linear light while data mips average raw channels");
        var alphaSource = Image(2,1,true, (x,y) => x == 0 ? new byte[] {255,0,0,255} : new byte[] {0,0,255,0});
        var alphaMip = DiagnosticMips.Build(alphaSource, false)[1];
        Check(alphaMip.Channel(0,0) == 255 && alphaMip.Channel(0,2) == 0 && alphaMip.Channel(0,3) == 128, "premultiplied color filtering excludes hidden transparent color and preserves averaged alpha");
        var tinyAlpha = Image(2,2,true,(x,y) => new byte[] {255,0,0,(byte)(x == 0 && y == 0 ? 1 : 0)});
        var tinyMip = DiagnosticMips.Build(tinyAlpha,false)[1];
        Check(tinyMip.Channel(0,3) == 0 && tinyMip.Channel(0,0) == 0, "alpha quantization to fully transparent also clears diagnostic color RGB");
        var odd = Image(5,1,false, (x,y) => new byte[] {(byte)(x == 4 ? 255 : 0),0,0,255}); var oddMips = DiagnosticMips.Build(odd, true);
        Check(oddMips.Select(i => i.Width).SequenceEqual(new[] {5,2,1}) && oddMips[1].Channel(1,0) == 102 && oddMips[2].Channel(0,0) == 51,
            "NPOT area filtering includes the final source column and deterministically reaches 1x1");
        Check(DiagnosticMips.Build(hard,false).Last().Channel(0,0) > 100, "reduced-resolution mixing exposes contamination from adjacent high-contrast regions");
        Check(DiagnosticMips.Build(blackWhite,false)[1].CopyPixels().SequenceEqual(colorMips[1].CopyPixels()) && solid.CopyPixels().SequenceEqual(preserved), "diagnostic generation is deterministic and source pixels remain immutable");
        bool cancelled = false; try { DiagnosticMips.Build(hard,false,_ => true); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && hard.Channel(0,0) == 255, "cancelled diagnostic construction preserves the source");
        bool limited = false; try { SeamGeometry.Find(new UvTriangle[SeamGeometry.MaxTriangles + 1],0,7,Vector2.One,Vector2.Zero,UvWrap.Repeat,UvWrap.Repeat); } catch (ArgumentException) { limited = true; }
        Check(limited, "oversized source topology is rejected before building edge tables");
        var crowded = new SeamTopology(); crowded.Pairs.AddRange(Enumerable.Repeat(pair, 10000)); bool sampleLimited = false;
        try { SeamAnalyzer.Analyze(crowded, Image(2,512,false,(_,__) => new byte[] {0,0,0,255}),false,UvWrap.Repeat,UvWrap.Clamp); } catch (ArgumentException) { sampleLimited = true; }
        Check(sampleLimited, "oversized seam sample workload is rejected before measuring partial results");
        bool seamCancelled = false; try { SeamGeometry.Find(Cylinder(),0,7,Vector2.One,Vector2.Zero,UvWrap.Repeat,UvWrap.Clamp,_ => true); } catch (OperationCanceledException) { seamCancelled = true; }
        Check(seamCancelled, "topology analysis cancellation is explicit");
        var report = new QualityReport { Source = "synthetic\nsource", Binding = "slot 0 / UV7", Mapping = "ordinary synthetic UV", InputFormat = "RGBA8", Data = false, WrapU = UvWrap.Repeat, WrapV = UvWrap.Clamp, Topology = topology, Levels = DiagnosticMips.Build(solid,false) };
        report.Measurements.Add(0, new System.Collections.Generic.List<SeamMeasurement> { continuous });
        string folder = Path.Combine(Path.GetTempPath(), "ats-quality-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder,"report.txt"); report.SaveNew(path,false); byte[] bytes = File.ReadAllBytes(path);
            Check(File.ReadAllText(path).Contains("synthetic\\nsource") && File.ReadAllText(path).Contains("NOT TESTED") && File.ReadAllText(path).Contains("Oklab"), "exported report retains metrics, escaped provenance and explicit unverified status");
            report.Measurements[0][0] = hidden;
            Check(report.Text(false).Contains("\tN/A\t"), "undefined visible-color metrics are exported as N/A rather than apparent zero continuity");
            bool refused = false; try { report.SaveNew(path,true); } catch (IOException) { refused = true; }
            Check(refused && File.ReadAllBytes(path).SequenceEqual(bytes), "diagnostic report export refuses existing files and preserves prior bytes");
        }
        finally { Directory.Delete(folder,true); }
    }
}
