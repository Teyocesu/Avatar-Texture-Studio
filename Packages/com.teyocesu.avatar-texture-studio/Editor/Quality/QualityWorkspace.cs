using System;
using System.Collections.Generic;
using System.Linq;
using AvatarTextureStudio.Creative;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.Infrastructure;
using AvatarTextureStudio.Painting;
using AvatarTextureStudio.Quality.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.Quality
{
    [Serializable]
    internal sealed class QualityWorkspace : IDisposable
    {
        static readonly string[] Sources = { "Imported texture (GPU mip 0)", "Painting composite", "Masks composite", "Current Colorway result" };
        [SerializeField] int sourceKind, level, selectedPair;
        [SerializeField] bool rawData, alphaOnly;
        [NonSerialized] QualityReport report;
        [NonSerialized] UvLayout capturedLayout;
        [NonSerialized] CreativeDocument capturedCreative;
        [NonSerialized] int capturedSourceKind;
        [NonSerialized] bool supported;
        [NonSerialized] UvWrap wrapU, wrapV;
        [NonSerialized] Texture2D baseTexture, levelTexture;
        [NonSerialized] int textureLevel = -1;
        [NonSerialized] string status;

        bool Stale(CreativeWorkspace creative) => report != null && (sourceKind != capturedSourceKind || (rawData || creative.DiagnosticIsData(sourceKind)) != report.Data
            || !ReferenceEquals(capturedLayout, creative.Layout) || (capturedSourceKind == 3 && !ReferenceEquals(capturedCreative, creative.Document)) || report.Binding != creative.DiagnosticBinding);

        public void Draw(bool mipView, CreativeWorkspace creative, PaintSession colors, PaintSession masks, Action<Texture2D, Texture2D, bool> compare, Action<int> goTo)
        {
            EditorGUILayout.LabelField(mipView ? "MipDoctor · CPU diagnostic levels" : "SeamDoctor · topology-constrained candidates", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Read-only frozen snapshots. Measured contrast is not a confirmed visual defect or export PASS. CPU mips are not exact Unity/VRChat mips or GPU compression. Recapture after painting, undo, reimport or shader changes; source freshness is not reverified automatically.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Return to Edit mode. Editable sessions use their existing recovery; diagnostic snapshots are temporary.", MessageType.Info); return; }
            sourceKind = EditorGUILayout.Popup("Snapshot source", sourceKind, Sources);
            bool forcedData = creative.DiagnosticIsData(sourceKind);
            using (new EditorGUI.DisabledScope(forcedData)) rawData = EditorGUILayout.Toggle("Raw channel-data diagnostics", rawData);
            if (forcedData) EditorGUILayout.LabelField("This source uses data semantics regardless of the color-mode toggle.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture and analyze")) Capture(creative, colors, masks);
                using (new EditorGUI.DisabledScope(report == null)) if (GUILayout.Button("Export report…")) ExportReport(creative);
            }
            bool stale = Stale(creative);
            if (stale) EditorGUILayout.HelpBox("Geometry, selection or Colorway state differs from the frozen report. Recapture before associating its results with the current model. Old measurements remain labelled snapshot data.", MessageType.Warning);
            if (report != null)
            {
                level = Mathf.Clamp(level, 0, report.Levels.Count - 1);
                if (mipView) level = EditorGUILayout.IntSlider("CPU mip level", level, 0, report.Levels.Count - 1);
                else level = 0;
                alphaOnly = EditorGUILayout.Toggle("Alpha inspection", alphaOnly);
                EnsureTextures();
                var image = report.Levels[level];
                EditorGUILayout.LabelField($"CPU L{level}: {image.Width}×{image.Height} · base {report.Levels[0].Width}×{report.Levels[0].Height} · {(report.Data ? "raw RGBA" : image.IsSRGB ? "sRGB color" : "linear color")} · captured {report.CapturedUtc:yyyy-MM-dd HH:mm:ss} UTC", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(report.Source, EditorStyles.wordWrappedMiniLabel);
                if (!supported) EditorGUILayout.HelpBox("Seam color measurements unavailable: " + report.Mapping + ". CPU image/mip inspection remains available; no UV sampling fallback was used.", MessageType.Warning);
                DrawSummary();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Compare base / CPU level")) compare(baseTexture, levelTexture, report.Data);
                    using (new EditorGUI.DisabledScope(!supported || report.Topology == null))
                        if (GUILayout.Button("Measure this CPU level")) MeasureLevel();
                    if (GUILayout.Button("Export snapshot PNG…"))
                    {
                        string path = NewImageExport.Save(baseTexture, !report.Data && report.Levels[0].IsSRGB, (TextureWrapMode)wrapU, (TextureWrapMode)wrapV, "ReviewedTexture");
                        if (path != null) status = "Exported the frozen base snapshot to " + path + ". No assignment or source changes; diagnostics do not certify rendering.";
                    }
                }
                DrawPair();
                var pair = report.Topology != null && selectedPair >= 0 && selectedPair < report.Topology.Pairs.Count ? report.Topology.Pairs[selectedPair] : null;
                creative.SetDiagnosticEdge(stale ? null : pair);
            }
            else creative.SetDiagnosticEdge(null);
            creative.DrawDiagnosticInspection(sourceKind == 2 ? masks : colors, report != null ? levelTexture : null, report != null ? report.Data : rawData || forcedData, alphaOnly);
            if (Stale(creative)) creative.SetDiagnosticEdge(null);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("UV Explorer")) goTo(3);
                if (GUILayout.Button("Painting")) goTo(0);
                if (GUILayout.Button("Colorway / export")) goTo(4);
                if (GUILayout.Button(mipView ? "SeamDoctor" : "MipDoctor")) goTo(mipView ? 6 : 7);
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        void Capture(CreativeWorkspace creative, PaintSession colors, PaintSession masks)
        {
            try
            {
                var image = creative.CaptureDiagnosticImage(sourceKind, colors, masks, out string provenance, out var u, out var v, out bool bindingMatches);
                var next = new QualityReport { Source = provenance, InputFormat = provenance, Binding = creative.DiagnosticBinding,
                    Data = rawData || creative.DiagnosticIsData(sourceKind), WrapU = u, WrapV = v,
                    Mapping = bindingMatches ? creative.CorrespondenceReason ?? "Supported ordinary UV/material mapping; native rendering remains unverified" : "Snapshot binding/wraps differ from the selected source geometry" };
                bool canMeasure = bindingMatches && creative.CorrespondenceReason == null && creative.Layout != null;
                if (creative.Layout != null)
                {
                    var triangles = creative.CaptureDiagnosticTopology(p => EditorUtility.DisplayCancelableProgressBar("SeamDoctor", "Reading source topology…", p));
                    var layout = creative.Layout;
                    next.Topology = SeamGeometry.Find(triangles, creative.Slot, creative.Channel, layout.Scale, layout.Offset, u, v,
                        p => EditorUtility.DisplayCancelableProgressBar("SeamDoctor", "Validating topology-constrained seam candidates…", p));
                }
                next.Levels = DiagnosticMips.Build(image, next.Data, p => EditorUtility.DisplayCancelableProgressBar("MipDoctor", "Generating CPU area-box levels…", p));
                if (canMeasure && next.Topology != null) next.Measurements.Add(0, SeamAnalyzer.Analyze(next.Topology, image, next.Data, u, v,
                    p => EditorUtility.DisplayCancelableProgressBar("SeamDoctor", "Measuring frozen base pixels…", p)));
                ReleaseTextures(); report = next; supported = canMeasure; wrapU = u; wrapV = v; capturedSourceKind = sourceKind;
                capturedLayout = creative.Layout; capturedCreative = creative.Document; level = 0; selectedPair = 0;
                status = "Captured read-only diagnostic snapshot. Deliberate color transitions and uncertain topology need human review; no visual-defect verdict is implied.";
            }
            catch (OperationCanceledException e) { status = e.Message; }
            finally { EditorUtility.ClearProgressBar(); }
        }
        void MeasureLevel()
        {
            try
            {
                var measured = SeamAnalyzer.Analyze(report.Topology, report.Levels[level], report.Data, wrapU, wrapV,
                    p => EditorUtility.DisplayCancelableProgressBar("MipDoctor", "Measuring seam differences at CPU L" + level, p));
                report.Measurements[level] = measured;
            }
            catch (OperationCanceledException e) { status = e.Message; }
            finally { EditorUtility.ClearProgressBar(); }
        }
        void DrawSummary()
        {
            if (report.Topology != null)
            {
                EditorGUILayout.LabelField($"Measured geometry: {report.Topology.Pairs.Count} candidate pairs · {report.Topology.ContinuousEdges} ordinary edges · {report.Topology.OpenBoundaries} open boundaries.", EditorStyles.wordWrappedMiniLabel);
                foreach (var excluded in report.Topology.Excluded) EditorGUILayout.LabelField($"Unmeasured: {excluded.Value} · {excluded.Key}", EditorStyles.wordWrappedMiniLabel);
            }
            if (report.Measurements.TryGetValue(level, out var m))
                EditorGUILayout.LabelField($"CPU L{level}: {m.Sum(x => x.Samples)} samples · {m.Sum(x => x.Unresolved)} unresolved · {m.Sum(x => x.PotentialContrast)} potential contrast · {m.Sum(x => x.ExteriorContrast)} exterior-ring contrasts.", EditorStyles.wordWrappedMiniLabel);
            else EditorGUILayout.LabelField("No seam measurements for this level. Thin/uncertain/unsupported mappings do not imply continuity.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Unverified: actual Unity-generated mips, compressed-platform filtering, shaders and visual rendering. Export remains an explicit new-file operation.", EditorStyles.wordWrappedMiniLabel);
        }
        void DrawPair()
        {
            if (report.Topology == null || report.Topology.Pairs.Count == 0) return;
            selectedPair = EditorGUILayout.IntSlider("Candidate pair", selectedPair, 0, report.Topology.Pairs.Count - 1);
            var pair = report.Topology.Pairs[selectedPair];
            EditorGUILayout.LabelField($"Triangles {pair.A.Triangle} / {pair.B.Triangle} · submesh {pair.A.Submesh} / slot {pair.A.Slot} / UV{pair.A.Channel} · source vertices {pair.A.StartVertex},{pair.A.EndVertex} ↔ {pair.B.StartVertex},{pair.B.EndVertex}. Cyan / magenta edges.", EditorStyles.wordWrappedMiniLabel);
            if (!report.Measurements.TryGetValue(level, out var measured)) return;
            var m = measured[selectedPair];
            string lab = report.Data || m.VisibleColorSamples == 0 ? "N/A" : $"{m.MeanDeltaE100:F3}/{m.MaxDeltaE100:F3}";
            if (m.Samples == 0) EditorGUILayout.LabelField($"No resolvable interior samples; {m.Unresolved} unresolved. Color/alpha/difference metrics are N/A.", EditorStyles.wordWrappedMiniLabel);
            else EditorGUILayout.LabelField($"Samples {m.Samples}, unresolved {m.Unresolved}; visible colors {m.VisibleColorSamples}. Oklab×100 mean/max {lab}; alpha {m.MeanAlpha:F4}/{m.MaxAlpha:F4}; composite/raw {m.MeanDifference:F4}/{m.MaxDifference:F4}; worst t={m.WorstT:F4}.", EditorStyles.wordWrappedMiniLabel);
            if (level != 0 && m.Samples > 0 && report.Measurements.TryGetValue(0, out var baseline) && baseline[selectedPair].Samples > 0) EditorGUILayout.LabelField($"Base max difference {baseline[selectedPair].MaxDifference:F4}; CPU L{level} max {m.MaxDifference:F4}. Increases can suggest reduced-resolution contamination; filtering/author intent remain unverified.", EditorStyles.wordWrappedMiniLabel);
        }
        void EnsureTextures()
        {
            Texture2D Make(ColorImage image, string name) => CreativeWorkspace.TextureFrom(image.CopyPixels(), image.Width, image.Height, !report.Data && image.IsSRGB, name);
            if (baseTexture == null) baseTexture = Make(report.Levels[0], "Frozen diagnostic base / CPU RGBA8");
            if (levelTexture == null || textureLevel != level)
            {
                if (levelTexture != null) Object.DestroyImmediate(levelTexture);
                levelTexture = Make(report.Levels[level], "CPU diagnostic mip L" + level); textureLevel = level;
            }
        }
        void ExportReport(CreativeWorkspace creative)
        {
            string path = EditorUtility.SaveFilePanel("Export new diagnostic report", "", "TextureQuality", "txt");
            if (string.IsNullOrEmpty(path)) return;
            report.SaveNew(path, Stale(creative)); status = "Exported frozen diagnostic report: " + path + ". Existing files were preserved.";
        }
        void ReleaseTextures()
        {
            if (baseTexture != null) Object.DestroyImmediate(baseTexture); if (levelTexture != null) Object.DestroyImmediate(levelTexture);
            baseTexture = levelTexture = null; textureLevel = -1;
        }
        public void Dispose() { ReleaseTextures(); report = null; capturedLayout = null; capturedCreative = null; }
    }
}
