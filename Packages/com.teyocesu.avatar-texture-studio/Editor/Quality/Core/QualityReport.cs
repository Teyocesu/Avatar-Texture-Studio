using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.Infrastructure;

namespace AvatarTextureStudio.Quality.Core
{
    internal sealed class QualityReport
    {
        public string Source = "", Binding = "", Mapping = "", InputFormat = "";
        public bool Data;
        public UvWrap WrapU, WrapV;
        public DateTime CapturedUtc = DateTime.UtcNow;
        public SeamTopology Topology;
        public List<ColorImage> Levels;
        public readonly SortedDictionary<int, List<SeamMeasurement>> Measurements = new SortedDictionary<int, List<SeamMeasurement>>();
        public string Text(bool stale)
        {
            string Number(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
            string Vector(Vector2 v) => Number(v.X) + "," + Number(v.Y);
            string OneLine(string s)
            {
                if (s == null || s.Length > 4096) throw new InvalidDataException("Diagnostic metadata exceeds 4096 characters.");
                return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            }
            var b = new StringBuilder();
            b.AppendLine("Avatar Texture Studio quality diagnostic report v1");
            b.AppendLine("Snapshot UTC: " + CapturedUtc.ToString("O", CultureInfo.InvariantCulture));
            b.AppendLine("Geometry/selection stale relative to current workspace: " + stale);
            b.AppendLine("Source freshness is not reverified automatically. This is frozen capture data; recapture after edits, undo, reimport or shader changes.");
            b.AppendLine("Source: " + OneLine(Source)); b.AppendLine("Binding: " + OneLine(Binding)); b.AppendLine("Input: " + OneLine(InputFormat));
            b.AppendLine("Mapping: " + OneLine(Mapping)); b.AppendLine("Mode: " + (Data ? "stored RGBA data, no transfer/premultiplication" : "linear-light color, premultiplied alpha"));
            b.AppendLine("Source transfer flag: " + (Levels[0].IsSRGB ? "sRGB" : "linear") + (Data ? " (ignored for raw data filtering)" : ""));
            b.AppendLine("Diagnostic wrap U/V: " + WrapU + "/" + WrapV);
            b.AppendLine("CPU diagnostic area-box mips / RGBA8 quantization per level. Not exact Unity/VRChat mipmaps or GPU compression.");
            b.AppendLine("Native compilation, shaders, rendering, Windows/Metal and visual defects: NOT TESTED. No quality PASS is implied.");
            b.AppendLine("Seams require exact source positions, unique opposite edges, indexed connectivity and same material/submesh; disconnected coincidences are unmeasured.");
            b.AppendLine("Metrics: Euclidean Oklab x100 for two visible colors; alpha absolute; max linear RGB composited on black/white. Data uses max raw RGBA.");
            b.AppendLine("Diagnostic bilinear sampling uses linear premultiplied color or raw channels; actual shader filtering/compression is not modeled. N/A means no eligible measurement.");
            b.AppendLine("Interior offset 0.5 texel; exterior probe 1 texel. At most 256 samples/edge and 1,000,000/level. Concern threshold 0.05 normalized composite/data difference.");
            b.AppendLine("Contrast may be intentional. Exterior contrast indicates potential bleeding/padding concerns, never confirmed insufficient padding.");
            if (Topology != null)
            {
                b.AppendLine($"Topology: triangles={Topology.Triangles}; candidate_pairs={Topology.Pairs.Count}; continuous_edges={Topology.ContinuousEdges}; open_boundaries={Topology.OpenBoundaries}");
                foreach (var excluded in Topology.Excluded) b.AppendLine("Unmeasured: " + excluded.Key + " = " + excluded.Value);
            }
            else b.AppendLine("Seam topology unavailable; no seam measurement was substituted.");
            for (int level = 0; level < Levels.Count; level++) b.AppendLine($"CPU level {level}: {Levels[level].Width}x{Levels[level].Height}");
            b.AppendLine("level\ttriA\ttriB\tsubmesh/slot/UV\tvertices A/B\tUV edge A/B\tsamples\tunresolved\tvisible_colors\tmean/max Oklab100\tmean/max alpha\tmean/max difference\tworst_t\tpotential_contrast\texterior_contrast");
            foreach (var level in Measurements)
            foreach (var m in level.Value)
            {
                var a = m.Pair.A; var c = m.Pair.B;
                string lab = Data || m.VisibleColorSamples == 0 ? "N/A" : Number(m.MeanDeltaE100) + "/" + Number(m.MaxDeltaE100);
                string alpha = m.Samples == 0 ? "N/A" : Number(m.MeanAlpha) + "/" + Number(m.MaxAlpha);
                string difference = m.Samples == 0 ? "N/A" : Number(m.MeanDifference) + "/" + Number(m.MaxDifference);
                b.AppendLine($"{level.Key}\t{a.Triangle}\t{c.Triangle}\t{a.Submesh}/{a.Slot}/{a.Channel}\t{a.StartVertex},{a.EndVertex}/{c.StartVertex},{c.EndVertex}\t{Vector(a.Start)}:{Vector(a.End)}/{Vector(c.Start)}:{Vector(c.End)}\t{m.Samples}\t{m.Unresolved}\t{m.VisibleColorSamples}\t{lab}\t{alpha}\t{difference}\t{Number(m.WorstT)}\t{m.PotentialContrast}\t{m.ExteriorContrast}");
                if (b.Length > 4 * 1024 * 1024) throw new InvalidDataException("Diagnostic report exceeds its 8 MiB text budget; analyze/export fewer levels.");
            }
            return b.ToString();
        }
        public void SaveNew(string path, bool stale)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(Text(stale));
            if (bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Diagnostic report exceeds 8 MiB.");
            SafeFileIO.Write(path, s => s.Write(bytes, 0, bytes.Length), false);
        }
    }
}
