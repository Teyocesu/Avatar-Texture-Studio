using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AvatarTextureStudio.Creative.Core;
using AvatarTextureStudio.Infrastructure;
using AvatarTextureStudio.Painting;
using AvatarTextureStudio.TextureLab;
using UnityEditor;
using UnityEngine;
using N2 = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.Creative
{
    /// <summary>Shared UV / Colorway / Face state inside the existing window. Owns only transient previews.</summary>
    [Serializable]
    internal sealed class CreativeWorkspace : IDisposable
    {
        const string RestoreKey = "AvatarTextureStudio.Creative.Recovery";
        const string ProjectKey = RestoreKey + ".Project";
        const string DirtyKey = RestoreKey + ".Dirty";
        [SerializeField] Renderer renderer;
        [SerializeField] int slot, channel, sourcePart;
        [SerializeField] string property = "_MainTex";
        [SerializeField] float zoom = 1, brushRadius = 12;
        [SerializeField] Vector2 pan;
        [SerializeField] bool rawLayout, edges = true, scenePick = true, showRegion = true, modelPreview;
        [SerializeField] int tool;
        [NonSerialized] CreativeHistory history;
        [NonSerialized] CreativeMeshAdapter mesh;
        [NonSerialized] PaintTarget previewTarget;
        [NonSerialized] Texture2D baselineTexture, resultTexture, regionTexture, modelTexture, compareAfter;
        [NonSerialized] Material canvasMaterial;
        [NonSerialized] List<SurfaceHit> candidates = new List<SurfaceHit>();
        [NonSerialized] int chosen = -1, dragControl;
        [NonSerialized] bool dirty, resultStale = true, active, painting;
        [NonSerialized] string projectPath, mappingWarning, status, meshKey;
        [NonSerialized] Action changed, repaint;
        [NonSerialized] Color sampled;
        [NonSerialized] bool hasSample;
        [NonSerialized] bool maskStroke;
        [NonSerialized] N2 lastMaskPoint;
        public CreativeDocument Document => history?.Current;
        public bool Dirty => dirty;
        public bool Painting => painting;
        public Renderer Renderer => renderer;
        public int Slot => slot;
        public int Channel => channel;
        public string Property => property;
        Material SelectedMaterial => renderer != null && slot >= 0 && slot < renderer.sharedMaterials.Length ? renderer.sharedMaterials[slot] : null;
        Texture2D Source => SelectedMaterial != null && SelectedMaterial.HasProperty(property) ? SelectedMaterial.GetTexture(property) as Texture2D : null;
        ColorRegion Region => Document != null && Document.ActiveRegion >= 0 && Document.ActiveRegion < Document.Regions.Count ? Document.Regions[Document.ActiveRegion] : null;

        public void Initialize(Action onChanged, Action onRepaint) { changed = onChanged; repaint = onRepaint; if (candidates == null) candidates = new List<SurfaceHit>(); }
        public void Enter()
        {
            active = true;
            SceneView.duringSceneGui -= OnSceneGUI; SceneView.duringSceneGui += OnSceneGUI;
        }
        public void Leave()
        {
            active = false; painting = false; EndInteraction(); RestoreModel();
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        public void StopPainting() { painting = false; }
        public bool Matches(PaintPart part) => part != null && part.Target.Renderer == renderer && part.Target.Slots.Contains(slot)
            && part.Target.TextureProperty == property && part.Target.UVChannel == channel;
        public bool Related(PaintSession session) => Related(session, Document);
        static bool Related(PaintSession session, CreativeDocument document) => document != null && session != null && session.Parts.Any(p =>
            p.Target.Renderer != null && Hierarchy(p.Target.Renderer.transform) == document.Binding.Renderer && p.Target.Slots.Contains(document.Binding.Slot)
            && p.Target.TextureProperty == document.Binding.Property && p.Target.UVChannel == document.Binding.Channel
            && p.Document.Width == document.Baseline.Width && p.Document.Height == document.Baseline.Height && p.Document.IsSRGB == document.Baseline.IsSRGB);

        public void Draw(int workspace, PaintSession session, Action<Color> sampleColor, Action<Texture2D, Texture2D> compare, Func<bool> startPainting)
        {
            bool face = workspace == 5;
            if (painting && (session == null || !session.Parts.Any(Matches))) { painting = false; session?.Deactivate(); }
            EditorGUILayout.LabelField(face ? "Face Studio · manual regions" : workspace == 4 ? "Colorway Studio" : "UV Explorer", EditorStyles.boldLabel);
            DrawProjectToolbar();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Return to Edit mode to resume creative work. Recovery data is kept in Library/AvatarTextureStudio.", MessageType.Info); return;
            }
            DrawTarget(session);
            if (renderer != null) EnsureMesh();
            if (!string.IsNullOrEmpty(mappingWarning)) EditorGUILayout.HelpBox(mappingWarning, MessageType.Warning);
            if (Document != null && !BindingMatches()) EditorGUILayout.HelpBox("The current target differs from the saved creative binding. Image edits remain available; model preview and triangle-to-region operations require the captured binding. Re-select the saved target or explicitly capture a new baseline.", MessageType.Warning);
            if (face)
            {
                EditorGUILayout.HelpBox("Choose your own mesh and regions. Face presets share the Colorway masks. Region masks constrain recoloring; the existing layer brushes retain their normal painting behavior.", MessageType.Info);
                bool next = EditorGUILayout.Toggle("Paint with existing tools", painting);
                if (next != painting)
                {
                    RestoreModel();
                    if (!next) { painting = false; session?.Deactivate(); }
                    else painting = startPainting();
                }
            }
            else if (painting) { painting = false; session?.Deactivate(); }
            if (painting) EditorGUILayout.HelpBox("Scene view is owned by the existing color painting session. Turn painting off to pick UV surfaces or preview Colorway on the model. Capture the composite explicitly to update the creative baseline.", MessageType.Info);
            var part = session?.Parts.FirstOrDefault(Matches);
            using (new EditorGUILayout.HorizontalScope())
            {
                string error = ColorSourceError();
                using (new EditorGUI.DisabledScope(error != null))
                    if (GUILayout.Button(part != null ? "Capture painting composite" : "Capture color baseline")) Capture(part);
                if (Document != null && GUILayout.Button("Compare before / after"))
                {
                    EnsureBaseline();
                    if (face && painting && part != null)
                    {
                        session.EndStroke(); Destroy(ref compareAfter);
                        compareAfter = TextureFrom(ReadPixels(part.Document.GetComposite()), part.Document.Width, part.Document.Height, part.Document.IsSRGB, "Painting comparison");
                        compare(baselineTexture, compareAfter);
                    }
                    else if (UpdatePreview()) compare(baselineTexture, resultTexture);
                }
            }
            if (ColorSourceError() is string sourceError) EditorGUILayout.HelpBox(sourceError, MessageType.Info);
            if (Document != null) DrawRegions(face);
            tool = workspace == 3 ? (GUILayout.Toolbar(tool == 3 ? 1 : 0, new[] { "Pick triangle", "Eyedropper" }) == 1 ? 3 : 0)
                : GUILayout.Toolbar(tool, new[] { "Pick triangle", "Add pixels", "Subtract pixels", "Eyedropper" });
            if (tool == 1 || tool == 2) brushRadius = EditorGUILayout.Slider("Mask radius (pixels)", brushRadius, .5f, 256);
            DrawNavigation();
            DrawCanvas(sampleColor);
            DrawCandidates();
            if (Document != null && workspace != 3) DrawAdjustments();
            if (hasSample)
            {
                EditorGUILayout.ColorField("Stored color sample", sampled);
                EditorGUILayout.LabelField("Samples use the displayed baseline/result pixels, excluding the mask tint. RGB follows the captured transfer; linear samples are converted for the shared color brush. Alpha is retained.", EditorStyles.wordWrappedMiniLabel);
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        void DrawProjectToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Open Studio…", EditorStyles.toolbarButton))
                {
                    string path = EditorUtility.OpenFilePanel("Open editable creative project", "", "atsstudio");
                    if (!string.IsNullOrEmpty(path) && ConfirmReplace()) Open(path);
                }
                using (new EditorGUI.DisabledScope(Document == null))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton)) Save(false);
                    if (GUILayout.Button("Save As…", EditorStyles.toolbarButton)) Save(true);
                }
                GUILayout.FlexibleSpace(); GUILayout.Label(dirty ? "Creative · unsaved" : "Creative", EditorStyles.miniLabel);
            }
            if (Document != null) EditorGUILayout.LabelField($"{Document.Baseline.Width}×{Document.Baseline.Height} RGBA8 · {(Document.Baseline.IsSRGB ? "sRGB" : "linear color")} · {Document.Binding.Renderer} / slot {Document.Binding.Slot} / {Document.Binding.Property} / UV{Document.Binding.Channel}", EditorStyles.wordWrappedMiniLabel);
        }

        void DrawTarget(PaintSession session)
        {
            if (session != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sourcePart = EditorGUILayout.Popup("Painting texture", Mathf.Clamp(sourcePart, 0, session.Parts.Count - 1), session.Parts.Select(p => p.Label).ToArray());
                    using (new EditorGUI.DisabledScope(painting)) if (GUILayout.Button("Use target", GUILayout.Width(85)))
                    {
                        var p = session.Parts[sourcePart]; SetTarget(p.Target.Renderer, p.Target.Slots[0], p.Target.UVChannel, p.Target.TextureProperty);
                    }
                }
            }
            using (new EditorGUI.DisabledScope(painting))
            {
                EditorGUI.BeginChangeCheck();
                var nextRenderer = (Renderer)EditorGUILayout.ObjectField("Renderer", renderer, typeof(Renderer), true);
                if (EditorGUI.EndChangeCheck()) SetTarget(nextRenderer, 0, 0, "_MainTex");
                var materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
                if (materials.Length == 0) { EditorGUILayout.HelpBox("Choose a MeshRenderer or SkinnedMeshRenderer with a material. Inspection never modifies source meshes or UVs.", MessageType.Info); return; }
                EditorGUI.BeginChangeCheck();
                int nextSlot = EditorGUILayout.Popup("Material slot", Mathf.Clamp(slot, 0, materials.Length - 1), materials.Select((m, i) => $"{i}: {(m != null ? m.name : "missing")}").ToArray());
                var mat = materials[nextSlot]; var properties = MeshTexturePainterWindow.TextureProperties(mat);
                string nextProperty = property;
                if (properties.Count > 0) nextProperty = properties[EditorGUILayout.Popup("Texture property", Math.Max(0, properties.IndexOf(property)), properties.ToArray())];
                var sourceMesh = PaintTarget.GetSourceMesh(renderer); var uv = new List<Vector2>(); var names = new string[8];
                for (int i = 0; i < 8; i++) { if (sourceMesh != null) sourceMesh.GetUVs(i, uv); names[i] = $"UV{i}" + (uv.Count == 0 ? " (empty)" : ""); }
                int nextChannel = EditorGUILayout.Popup("Source UV channel", channel, names);
                if (EditorGUI.EndChangeCheck()) SetTarget(renderer, nextSlot, nextChannel, nextProperty);
            }
            if (SelectedMaterial != null) EditorGUILayout.LabelField($"{SelectedMaterial.name} · {SelectedMaterial.shader.name} · {property} · UV{channel}", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Refresh source geometry")) { mesh?.Dispose(); mesh = null; candidates.Clear(); chosen = -1; }
        }

        public void SetTarget(Renderer target, int materialSlot, int uv, string textureProperty)
        {
            RestoreModel(); mesh?.Dispose(); mesh = null; meshKey = null; candidates.Clear(); chosen = -1;
            renderer = target; slot = materialSlot; channel = uv; property = textureProperty; hasSample = false;
            repaint?.Invoke(); SceneView.RepaintAll();
        }
        void EnsureMesh()
        {
            var mat = SelectedMaterial; var source = Source;
            if (mat == null || !mat.HasProperty(property)) { RestoreModel(); mesh?.Dispose(); mesh = null; mappingWarning = "Choose a material with the selected texture property."; return; }
            var scale = mat.GetTextureScale(property); var offset = mat.GetTextureOffset(property);
            var u = source != null ? source.wrapModeU : TextureWrapMode.Repeat; var v = source != null ? source.wrapModeV : TextureWrapMode.Repeat;
            string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            string key = $"{renderer.GetInstanceID()}/{slot}/{channel}/{property}/{Number(scale.x)}/{Number(scale.y)}/{Number(offset.x)}/{Number(offset.y)}/{u}/{v}";
            if (mesh == null || meshKey != key)
            {
                RestoreModel(); mesh?.Dispose(); mesh = null;
                mesh = new CreativeMeshAdapter(renderer, slot, channel, property, CreativeMeshAdapter.ToCore(scale), CreativeMeshAdapter.ToCore(offset), u, v); meshKey = key;
                candidates.Clear(); chosen = -1;
            }
            else if (mesh.Refresh()) { candidates.Clear(); chosen = -1; SceneView.RepaintAll(); }
            mappingWarning = MappingError(mat);
            if (mesh.Layout.Collapsed > 0) EditorGUILayout.HelpBox($"{mesh.Layout.Collapsed} collapsed UV triangles: Scene identification is possible, reverse area selection is unavailable.", MessageType.Warning);
            if (mesh.Layout.Unsupported.Count > 0) EditorGUILayout.HelpBox($"{mesh.Layout.Unsupported.Count} triangles exceed the tile/fragment budget. Use raw layout to inspect them; wrapped region selection is unavailable for those triangles.", MessageType.Warning);
            if (mesh.Layout.BorderCollapsed > 0) EditorGUILayout.HelpBox("Clamp/MirrorOnce collapses some surfaces onto a texture border. Border reverse lookup cannot identify a unique surface; collapsed tails are excluded from triangle masks.", MessageType.Warning);
        }
        string MappingError(Material mat)
        {
            int mode = MeshTexturePainterWindow.ShaderUVMode(mat, property);
            if (mode > 3) return "This shader uses a non-UV mapping mode. Geometry inspection only; surface/texture correspondence and triangle masks are unsupported.";
            if (mode >= 0 && mode != channel) return $"Shader UV{mode} differs from inspected UV{channel}. Geometry inspection only; choose the shader's channel for correspondence.";
            if (MeshTexturePainterWindow.DecalTransformWarning(mat, property) != null) return "Transformed shader decal: raw geometry inspection only. Its extra mapping transforms are unsupported.";
            string name = mat.shader.name;
            bool ordinary = property == "_MainTex" && channel == 0 && (name == "Standard" || name == "Standard (Specular setup)" || name == "Unlit/Texture" || name == "Unlit/Transparent" || name == "Unlit/Transparent Cutout");
            return ordinary ? null : "Custom shader correspondence is unverified. Geometry UVs and material scale/offset are shown, but shader-specific transforms may differ. Triangle-to-region operations are disabled; direct pixel masking remains available for supported color sources.";
        }
        internal string ColorSourceError()
        {
            var source = Source; var mat = SelectedMaterial;
            if (mat == null || !MeshTexturePainterWindow.ColorTextureProperties(mat).Contains(property)) return "Select a supported color property; normal maps and shader-data masks cannot be recolored or sampled as color.";
            string error = TextureComparePanel.UnsupportedReason(source);
            if (error != null) return error;
            if (source.width > 8192 || source.height > 8192) return "Color capture is limited to 8192 pixels per axis.";
            return null;
        }

        CreativeBinding CurrentBinding() => new CreativeBinding { Renderer = renderer != null ? Hierarchy(renderer.transform) : "", Material = SelectedMaterial != null ? SelectedMaterial.name : "",
            Property = property, Source = Source != null ? AssetDatabase.GetAssetPath(Source) : "", Slot = slot, Channel = channel,
            Scale = SelectedMaterial != null ? CreativeMeshAdapter.ToCore(SelectedMaterial.GetTextureScale(property)) : N2.One,
            Offset = SelectedMaterial != null ? CreativeMeshAdapter.ToCore(SelectedMaterial.GetTextureOffset(property)) : N2.Zero,
            WrapU = Source != null ? (UvWrap)Source.wrapModeU : UvWrap.Repeat, WrapV = Source != null ? (UvWrap)Source.wrapModeV : UvWrap.Repeat };
        bool BindingMatches()
        {
            if (Document == null || renderer == null || SelectedMaterial == null || !SelectedMaterial.HasProperty(property)) return false;
            var a = Document.Binding; var b = CurrentBinding();
            return a.Renderer == b.Renderer && a.Material == b.Material && a.Property == b.Property && a.Source == b.Source && a.Slot == b.Slot && a.Channel == b.Channel
                && a.Scale == b.Scale && a.Offset == b.Offset && a.WrapU == b.WrapU && a.WrapV == b.WrapV;
        }
        static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
        internal void Capture(PaintPart part)
        {
            string reason = ColorSourceError(); if (reason != null) throw new InvalidOperationException(reason);
            if (part != null && (!Matches(part) || part.Document.IsMask)) throw new InvalidOperationException("Capture requires the matching color-paint document; shader-data masks are not color baselines.");
            string gpu = PaintResources.UnsupportedReason(); if (gpu != null) throw new InvalidOperationException(gpu);
            byte[] pixels; int width, height; bool srgb;
            if (part != null)
            {
                width = part.Document.Width; height = part.Document.Height; srgb = part.Document.IsSRGB;
                pixels = ReadPixels(part.Document.GetComposite());
            }
            else
            {
                var source = Source; width = source.width; height = source.height; srgb = PaintProjectIO.IsSRGB(source);
                using (var document = new PaintDocument(width, height) { IsSRGB = srgb })
                {
                    var rt = document.RenderImported(source, srgb);
                    try { pixels = ReadPixels(rt); } finally { RTUtil.Release(ref rt); }
                }
            }
            var image = new ColorImage(width, height, srgb, pixels); var binding = CurrentBinding();
            if (Document != null && Document.Baseline.Width == width && Document.Baseline.Height == height)
            {
                if (!EditorUtility.DisplayDialog("Refresh creative baseline", "Replace the baseline with this snapshot, keeping region masks and editable adjustments? This can be undone during this session.", "Refresh", "Cancel")) return;
                history.Edit(d => { d.Baseline = image; d.Binding = binding; });
            }
            else
            {
                if (Document != null && !ConfirmReplace()) return;
                history = new CreativeHistory(new CreativeDocument { Baseline = image, Binding = binding }); projectPath = null;
            }
            Invalidate(true); status = "Captured immutable color snapshot. Original assets and painting layers are unchanged.";
        }
        static byte[] ReadPixels(RenderTexture rt)
        {
            var texture = RTUtil.ReadToTexture(rt);
            try { return texture.GetRawTextureData<byte>().ToArray(); } finally { Object.DestroyImmediate(texture); }
        }
        static Texture2D TextureFrom(byte[] pixels, int width, int height, bool srgb, string name, bool mipmaps = false)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps, !srgb) { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            try { texture.SetPixelData(pixels, 0); texture.Apply(mipmaps, false); return texture; }
            catch { Object.DestroyImmediate(texture); throw; }
        }
        void EnsureBaseline()
        {
            if (baselineTexture == null && Document != null) baselineTexture = TextureFrom(Document.Baseline.CopyPixels(), Document.Baseline.Width, Document.Baseline.Height, Document.Baseline.IsSRGB, "Creative baseline");
        }
        void Invalidate(bool baselineChanged = false)
        {
            dirty = true; resultStale = true; RestoreModel(); Destroy(ref regionTexture); Destroy(ref modelTexture);
            if (baselineChanged) Destroy(ref baselineTexture);
            changed?.Invoke(); repaint?.Invoke(); SceneView.RepaintAll();
        }
        void Edit(Action<CreativeDocument> action) { history.Edit(action); Invalidate(); }

        void DrawRegions(bool face)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!history.CanUndo)) if (GUILayout.Button("Undo creative")) { history.Undo(); Invalidate(true); }
                using (new EditorGUI.DisabledScope(!history.CanRedo)) if (GUILayout.Button("Redo creative")) { history.Redo(); Invalidate(true); }
                using (new EditorGUI.DisabledScope(Document.Regions.Count >= 32)) if (GUILayout.Button(face ? "New face preset" : "New region"))
                    Edit(d => { d.Regions.Add(new ColorRegion { Name = face ? "Face region" : "Region", FacePreset = face, Mask = new RegionMask(d.Baseline.Width, d.Baseline.Height) }); d.ActiveRegion = d.Regions.Count - 1; });
            }
            var names = new[] { "No active region" }.Concat(Document.Regions.Select((r, i) => $"{i + 1}: {r.Name}" + (r.FacePreset ? " · face" : ""))).ToArray();
            int selection = EditorGUILayout.Popup("Region / preset", Document.ActiveRegion + 1, names) - 1;
            if (selection != Document.ActiveRegion) { Document.ActiveRegion = selection; Invalidate(); }
            if (Region == null) return;
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Name", Region.Name); bool preset = EditorGUILayout.Toggle("Face preset", Region.FacePreset);
            if (EditorGUI.EndChangeCheck()) Edit(d => { d.Regions[d.ActiveRegion].Name = name; d.Regions[d.ActiveRegion].FacePreset = preset; });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear mask")) Edit(d => d.Regions[d.ActiveRegion].Mask = new RegionMask(d.Baseline.Width, d.Baseline.Height));
                if (GUILayout.Button("Delete region")) Edit(d => { d.Regions.RemoveAt(d.ActiveRegion); d.ActiveRegion = Math.Min(d.ActiveRegion, d.Regions.Count - 1); });
            }
            showRegion = EditorGUILayout.Toggle("Show active mask", showRegion);
            EditorGUILayout.LabelField("Masks select whole pixel centers. Mirrored/shared UV pixels recolor every surface using those pixels; surface choice does not split shared texels.", EditorStyles.wordWrappedMiniLabel);
        }
        void DrawAdjustments()
        {
            if (Region != null)
            {
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle("Adjustment enabled", Region.Enabled);
                float hue = EditorGUILayout.Slider("Hue rotation", Region.Hue, -180, 180);
                float chroma = EditorGUILayout.Slider("Chroma ×", Region.Chroma, 0, 3);
                float strength = EditorGUILayout.Slider("Strength", Region.Strength, 0, 1);
                if (EditorGUI.EndChangeCheck()) Edit(d => { var r = d.Regions[d.ActiveRegion]; r.Enabled = enabled; r.Hue = hue; r.Chroma = chroma; r.Strength = strength; });
            }
            EditorGUILayout.LabelField("Oklab: fixed perceptual lightness, radial chroma gamut reduction. Alpha and unselected RGBA8 pixels are exact. Regions apply in list order. Preview updates explicitly.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(resultStale ? "Update preview · changed" : "Update preview")) UpdatePreview();
                if (GUILayout.Button("Export new PNG…")) Export();
            }
            bool next = EditorGUILayout.Toggle("Preview on model", modelPreview);
            if (next != modelPreview) { modelPreview = next; RestoreModel(); }
            if (modelPreview && !painting && BindingMatches() && mappingWarning == null)
            {
                if (resultStale) EditorGUILayout.HelpBox("Update the preview before showing current adjustments on the model.", MessageType.Info);
                else ApplyModel(showRegion && Region != null ? GetModelOverlay() : resultTexture);
            }
            else if (modelPreview && !painting)
            {
                if (previewTarget != null) RestoreModel();
                EditorGUILayout.HelpBox("Model preview requires the captured binding and a supported shader correspondence.", MessageType.Warning);
            }
        }
        bool UpdatePreview()
        {
            if (Document == null) return false;
            if (!resultStale && resultTexture != null) return true;
            try
            {
                byte[] output = ColorwayProcessor.Render(Document, p => EditorUtility.DisplayCancelableProgressBar("Colorway", "Rendering editable adjustments…", p));
                var next = TextureFrom(output, Document.Baseline.Width, Document.Baseline.Height, Document.Baseline.IsSRGB, "Colorway result", true);
                ConfigureModelSampler(next);
                RestoreModel(); Destroy(ref resultTexture); Destroy(ref modelTexture); resultTexture = next; resultStale = false; EnsureBaseline();
                return true;
            }
            catch (OperationCanceledException e) { status = e.Message; return false; }
            finally { EditorUtility.ClearProgressBar(); }
        }
        Texture2D GetRegionOverlay()
        {
            if (Region == null) return null;
            if (regionTexture == null)
            {
                byte[] pixels = new byte[Document.Baseline.Width * Document.Baseline.Height * 4];
                for (int i = 0; i < pixels.Length / 4; i++) if (Region.Mask.Contains(i)) { pixels[i * 4 + 1] = 230; pixels[i * 4 + 2] = 255; pixels[i * 4 + 3] = 90; }
                regionTexture = TextureFrom(pixels, Document.Baseline.Width, Document.Baseline.Height, true, "Region overlay");
            }
            return regionTexture;
        }
        Texture2D GetModelOverlay()
        {
            if (modelTexture == null)
            {
                byte[] pixels = resultTexture.GetPixelData<byte>(0).ToArray();
                for (int i = 0; i < pixels.Length / 4; i++) if (Region.Mask.Contains(i))
                { pixels[i * 4] = (byte)(pixels[i * 4] / 2); pixels[i * 4 + 1] = (byte)((pixels[i * 4 + 1] + 230) / 2); pixels[i * 4 + 2] = (byte)((pixels[i * 4 + 2] + 255) / 2); }
                modelTexture = TextureFrom(pixels, Document.Baseline.Width, Document.Baseline.Height, Document.Baseline.IsSRGB, "Region model preview", true);
                ConfigureModelSampler(modelTexture);
            }
            return modelTexture;
        }
        void ApplyModel(Texture texture)
        {
            if (texture == null || renderer == null) return;
            if (previewTarget == null) previewTarget = new PaintTarget(renderer, new[] { slot }, channel, property);
            previewTarget.ApplyPreview(texture);
        }
        void ConfigureModelSampler(Texture2D texture)
        {
            texture.wrapModeU = (TextureWrapMode)Document.Binding.WrapU; texture.wrapModeV = (TextureWrapMode)Document.Binding.WrapV;
            texture.filterMode = BindingMatches() && Source != null ? Source.filterMode : FilterMode.Bilinear;
        }
        void RestoreModel() { previewTarget?.Dispose(); previewTarget = null; SceneView.RepaintAll(); }

        void DrawNavigation()
        {
            rawLayout = EditorGUILayout.Toggle("Raw transformed layout", rawLayout); edges = EditorGUILayout.Toggle("Triangle edges", edges);
            scenePick = EditorGUILayout.Toggle("Pick selected renderer in Scene", scenePick);
            using (new EditorGUILayout.HorizontalScope())
            {
                zoom = EditorGUILayout.Slider("Zoom × fit", zoom, .25f, 64);
                if (GUILayout.Button("Fit", GUILayout.Width(45))) { zoom = 1; pan = Vector2.zero; }
            }
            EditorGUILayout.LabelField("Left click: choose triangle/sample · middle or Alt drag: pan · wheel: zoom. Shared navigation across all creative workspaces. Raw layout shows material UV × scale + offset.", EditorStyles.wordWrappedMiniLabel);
        }
        void DrawCanvas(Action<Color> sampleColor)
        {
            EnsureBaseline();
            var texture = Document != null ? (!resultStale && resultTexture != null ? resultTexture : baselineTexture) : Source;
            Rect canvas = GUILayoutUtility.GetRect(0, 360, GUILayout.ExpandWidth(true)); GUI.Box(canvas, GUIContent.none);
            float aspect = texture != null ? (float)texture.width / texture.height : 1;
            float height = Mathf.Min(canvas.height - 4, (canvas.width - 4) / aspect) * zoom;
            Vector2 size = new Vector2(height * aspect, height);
            Rect image = new Rect(canvas.center + Vector2.Scale(pan, size) - size * .5f, size);
            HandleCanvas(canvas, image, texture, sampleColor);
            if (Event.current.type != EventType.Repaint) return;
            GUI.BeginGroup(canvas);
            try
            {
                Rect local = new Rect(image.position - canvas.position, image.size);
                if (texture != null && TextureComparePanel.UnsupportedReason(texture, Document != null) == null)
                {
                    var shader = Shader.Find("Hidden/AvatarTextureStudio/Compare");
                    if (shader == null || !shader.isSupported) throw new InvalidOperationException("The Compare preview shader is missing or unsupported.");
                    if (canvasMaterial == null) canvasMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    canvasMaterial.SetTexture("_TexA", texture); canvasMaterial.SetTexture("_TexB", texture);
                    canvasMaterial.SetFloat("_Mode", 0); canvasMaterial.SetFloat("_RawValues", Document == null && ColorSourceError() != null ? 1 : 0);
                    canvasMaterial.SetFloat("_SrgbA", PaintProjectIO.IsSRGB(texture) ? 1 : 0); canvasMaterial.SetFloat("_AlphaOnly", 0);
                    canvasMaterial.SetFloat("_LinearProject", QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
                    EditorGUI.DrawPreviewTexture(local, texture, canvasMaterial, ScaleMode.StretchToFill);
                }
                if (Document != null && showRegion && Region != null) GUI.DrawTexture(local, GetRegionOverlay(), ScaleMode.StretchToFill, true);
                Handles.BeginGUI();
                try
                {
                    if (mesh != null)
                    {
                        Vector3 Point(N2 uv) => new Vector3(local.x + uv.X * local.width, local.yMax - uv.Y * local.height, 0);
                        foreach (var f in rawLayout ? mesh.Layout.Raw : mesh.Layout.Sampled)
                        {
                            var a = Point(f.A.Uv); var b = Point(f.B.Uv); var c = Point(f.C.Uv);
                            if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < 0 || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > canvas.width
                                || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < 0 || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > canvas.height) continue;
                            bool selected = chosen >= 0 && chosen < candidates.Count && candidates[chosen].Triangle == f.Triangle;
                            if (selected) { Handles.color = new Color(1, .65f, .05f, .3f); Handles.DrawAAConvexPolygon(a, b, c); }
                            if (edges || selected) { Handles.color = selected ? Color.yellow : new Color(.8f, .8f, .8f, .55f); Handles.DrawAAPolyLine(1, a, b, c, a); }
                        }
                    }
                }
                finally { Handles.EndGUI(); }
            }
            finally { GUI.EndGroup(); }
        }
        void HandleCanvas(Rect canvas, Rect image, Texture2D texture, Action<Color> sampleColor)
        {
            var e = Event.current; int control = GUIUtility.GetControlID(FocusType.Passive, canvas);
            if (e.rawType == EventType.MouseUp) EndInteraction();
            if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
            {
                float next = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .08f), .25f, 64);
                Vector2 offset = e.mousePosition - canvas.center;
                pan += new Vector2(offset.x / image.width, offset.y / image.height) * (zoom / next - 1); zoom = next; e.Use(); repaint?.Invoke();
            }
            else if (e.type == EventType.MouseDown && canvas.Contains(e.mousePosition) && (e.button == 2 || (e.button == 0 && e.alt)))
            { dragControl = control; GUIUtility.hotControl = control; e.Use(); }
            else if (e.type == EventType.MouseDrag && dragControl == control && GUIUtility.hotControl == control)
            { pan += new Vector2(e.delta.x / image.width, e.delta.y / image.height); e.Use(); repaint?.Invoke(); }
            else if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt && canvas.Contains(e.mousePosition)
                && (image.Contains(e.mousePosition) || (rawLayout && tool == 0)))
            {
                N2 uv = new N2((e.mousePosition.x - image.x) / image.width, 1 - (e.mousePosition.y - image.y) / image.height);
                if (tool == 1 || tool == 2)
                {
                    if (Region != null)
                    {
                        var point = new N2(uv.X * Document.Baseline.Width, uv.Y * Document.Baseline.Height);
                        history.Edit(d => { var r = d.Regions[d.ActiveRegion]; r.Mask = r.Mask.Stroke(maskStroke ? lastMaskPoint : point, point, brushRadius, tool == 1); }, !maskStroke);
                        lastMaskPoint = point;
                        maskStroke = true; Invalidate();
                    }
                }
                else if (e.type == EventType.MouseDown)
                {
                    if (tool == 3) Sample(uv, sampleColor);
                    else if (mesh != null)
                    {
                        if (!rawLayout && mesh.Layout.AmbiguousBorder(uv)) { candidates.Clear(); chosen = -1; status = "Border collapse: a continuum of surfaces samples this edge. Use raw layout or Scene selection."; }
                        else SetCandidates(mesh.Layout.FindUv(uv, !rawLayout, slot));
                    }
                }
                e.Use(); repaint?.Invoke(); SceneView.RepaintAll();
            }
        }
        void Sample(N2 uv, Action<Color> sampleColor)
        {
            if (Document == null) throw new InvalidOperationException("Capture a color baseline to sample without changing importer Read/Write settings.");
            int x = Mathf.Clamp((int)(uv.X * Document.Baseline.Width), 0, Document.Baseline.Width - 1), y = Mathf.Clamp((int)(uv.Y * Document.Baseline.Height), 0, Document.Baseline.Height - 1);
            int p = y * Document.Baseline.Width + x;
            if (!resultStale && resultTexture != null)
            {
                var pixels = resultTexture.GetPixelData<byte>(0);
                sampled = new Color(pixels[p * 4] / 255f, pixels[p * 4 + 1] / 255f, pixels[p * 4 + 2] / 255f, pixels[p * 4 + 3] / 255f);
            }
            else sampled = new Color(Document.Baseline.Channel(p, 0) / 255f, Document.Baseline.Channel(p, 1) / 255f, Document.Baseline.Channel(p, 2) / 255f, Document.Baseline.Channel(p, 3) / 255f);
            hasSample = true;
            sampleColor(Document.Baseline.IsSRGB ? sampled : sampled.gamma);
            status = $"Sample: pixel ({x}, {y}), bottom-left origin · texture UV {uv.X:F5}, {uv.Y:F5}. Shared color brush updated.";
        }
        void SetCandidates(List<SurfaceHit> hits)
        {
            candidates = hits; chosen = hits.Count == 1 ? 0 : -1;
            status = hits.Count == 0 ? "No triangle at this coordinate." : hits.Count > 1 ? $"{hits.Count} possible surfaces / wrap aliases. Choose explicitly below; no surface has been selected." : "One surface selected.";
        }
        void DrawCandidates()
        {
            if (mesh == null || candidates.Count == 0) return;
            var names = new[] { "Choose a surface…" }.Concat(candidates.Select(h => $"Triangle {mesh.Layout.Triangles[h.Triangle].Id} / submesh {mesh.Layout.Triangles[h.Triangle].Submesh} · mesh UV {h.MeshUv.X:F4}, {h.MeshUv.Y:F4} · tile {h.TileX}, {h.TileY}")).ToArray();
            int next = EditorGUILayout.Popup("Surface match", chosen + 1, names) - 1;
            if (next != chosen) { chosen = next; SceneView.RepaintAll(); }
            if (chosen < 0) return;
            var h = candidates[chosen]; var wrapped = mesh.Layout.Wrapped(h.TextureUv);
            int width = Document?.Baseline.Width ?? (Source != null ? Source.width : 0), height = Document?.Baseline.Height ?? (Source != null ? Source.height : 0);
            int x = Math.Min(width - 1, (int)(wrapped.X * width)), y = Math.Min(height - 1, (int)(wrapped.Y * height));
            EditorGUILayout.LabelField($"Mesh UV {h.MeshUv.X:F6}, {h.MeshUv.Y:F6} · material UV {h.TextureUv.X:F6}, {h.TextureUv.Y:F6} · sampled {wrapped.X:F6}, {wrapped.Y:F6} · pixel ({x}, {y}), bottom-left", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(Region == null || mappingWarning != null || !BindingMatches() || !mesh.Layout.Rasterizable.Contains(h.Triangle)))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add triangle to region")) TriangleMask(h, true);
                if (GUILayout.Button("Subtract triangle")) TriangleMask(h, false);
            }
        }
        void TriangleMask(SurfaceHit hit, bool add) => Edit(d =>
        {
            var r = d.Regions[d.ActiveRegion]; r.Mask = r.Mask.Triangles(mesh.Layout.Sampled.Where(f => f.Triangle == hit.Triangle), add);
        });
        void OnSceneGUI(SceneView scene)
        {
            if (!active || painting || mesh == null || renderer == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (mesh.Refresh()) { candidates.Clear(); chosen = -1; repaint?.Invoke(); }
                var e = Event.current;
                if (scenePick && e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if (scenePick && e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    SetCandidates(mesh.Layout.FindNearestRay(CreativeMeshAdapter.ToCore(ray.origin), CreativeMeshAdapter.ToCore(ray.direction), slot));
                    if (chosen >= 0)
                    {
                        N2 uv = rawLayout ? candidates[chosen].TextureUv : mesh.Layout.Wrapped(candidates[chosen].TextureUv);
                        pan = new Vector2(.5f - uv.X, uv.Y - .5f);
                    }
                    e.Use(); repaint?.Invoke(); scene.Repaint();
                }
                if (e.type == EventType.Repaint && chosen >= 0 && chosen < candidates.Count)
                {
                    var t = mesh.Layout.Triangles[candidates[chosen].Triangle]; Color previous = Handles.color;
                    try { Handles.color = new Color(1, .6f, 0, .45f); Handles.DrawAAConvexPolygon(CreativeMeshAdapter.ToUnity(t.A), CreativeMeshAdapter.ToUnity(t.B), CreativeMeshAdapter.ToUnity(t.C)); }
                    finally { Handles.color = previous; }
                }
            }
            catch (Exception e) { status = "Scene correspondence unavailable: " + e.Message; scenePick = false; repaint?.Invoke(); }
        }

        public bool Save(bool saveAs)
        {
            if (Document == null) return true;
            string path = projectPath;
            if (saveAs || string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("Save editable creative project", "Assets", "Creative", "atsstudio");
            if (string.IsNullOrEmpty(path)) return false;
            try { SaveTo(path); AssetDatabase.Refresh(); return true; }
            catch (Exception e) { status = "Creative save failed: " + e.Message; Debug.LogException(e); return false; }
        }
        public void SaveTo(string path)
        {
            if (Document == null) return;
            SyncNavigation(); CreativeProjectIO.Save(path, Document, true); projectPath = path; dirty = false; changed?.Invoke();
        }
        public bool SaveCompanion(string path)
        {
            try
            {
                if (File.Exists(path) && !string.Equals(Path.GetFullPath(path), string.IsNullOrEmpty(projectPath) ? "" : Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase)
                    && !EditorUtility.DisplayDialog("Replace editable creative companion?", "The painting was saved. Replace the existing creative project at " + path + " with the current masks and adjustments?", "Replace companion", "Cancel"))
                { dirty = true; changed?.Invoke(); return false; }
                SaveTo(path); return true;
            }
            catch { dirty = true; changed?.Invoke(); throw; }
        }
        void SyncNavigation() { if (Document != null) { Document.Zoom = zoom; Document.Pan = CreativeMeshAdapter.ToCore(pan); } }
        public bool ConfirmReplace()
        {
            if (!dirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("Creative project has unsaved changes", "Save editable masks and adjustments before replacing this creative document?", "Save", "Cancel", "Discard");
            return choice == 2 || (choice == 0 && Save(false));
        }
        public void Open(string path, PaintSession expectedSession = null)
        {
            var loaded = CreativeProjectIO.Load(path); // Validate completely before touching the current document or preview.
            if (expectedSession != null && !Related(expectedSession, loaded)) throw new InvalidDataException("The creative companion does not match this painting's target, dimensions or transfer function. Current creative work was retained; open the sidecar separately to inspect it.");
            RestoreModel(); history = new CreativeHistory(loaded); ReleaseTextures(); projectPath = path; dirty = false; resultStale = true;
            zoom = loaded.Zoom; pan = CreativeMeshAdapter.ToUnity(loaded.Pan); candidates.Clear(); chosen = -1;
            status = "Editable creative project opened. Select its saved renderer/material/property/channel to restore model correspondence."; changed?.Invoke();
        }
        public void Autosave()
        {
            Leave(); if (Document == null) return; SyncNavigation();
            string path = Path.GetFullPath(Path.Combine("Library", "AvatarTextureStudio", "CreativeAutosave.atsstudio"));
            CreativeProjectIO.Save(path, Document, true);
            SessionState.SetString(RestoreKey, path); SessionState.SetString(ProjectKey, projectPath ?? ""); SessionState.SetBool(DirtyKey, dirty);
        }
        public void TryRestore()
        {
            if (Document != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = SessionState.GetString(RestoreKey, ""); if (string.IsNullOrEmpty(path)) return;
            Open(path); projectPath = SessionState.GetString(ProjectKey, ""); dirty = SessionState.GetBool(DirtyKey, true);
            SessionState.EraseString(RestoreKey); changed?.Invoke();
        }
        public void DiscardRecovery() { SessionState.EraseString(RestoreKey); }
        void Export()
        {
            if (!UpdatePreview()) return;
            string path = EditorUtility.SaveFilePanel("Export new Colorway PNG", "Assets", "Colorway", "png"); if (string.IsNullOrEmpty(path)) return;
            string assetPath = SafeFileIO.ExportAssetPath(path, Path.GetFullPath("."));
            if (File.Exists(path) || File.Exists(path + ".meta")) throw new IOException("Colorway exports require a new asset path. Choose a different filename; existing assets are preserved.");
            byte[] png = resultTexture.EncodeToPNG(); SafeFileIO.Write(path, s => s.Write(png, 0, png.Length), false);
            AssetDatabase.ImportAsset(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new IOException("PNG was written, but its new texture importer is unavailable: " + assetPath);
            importer.sRGBTexture = Document.Baseline.IsSRGB; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Math.Max(32, Math.Max(Document.Baseline.Width, Document.Baseline.Height)));
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = (TextureWrapMode)Document.Binding.WrapU; importer.wrapModeV = (TextureWrapMode)Document.Binding.WrapV;
            importer.SaveAndReimport();
            status = "Exported new PNG: " + assetPath + ". Its new importer matches the source transfer and preserves alpha/RGB without compression. No material assignment or source overwrite was performed.";
        }
        public void EndInteraction() { if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0; dragControl = 0; maskStroke = false; }
        static void Destroy(ref Texture2D texture) { if (texture != null) Object.DestroyImmediate(texture); texture = null; }
        void ReleaseTextures() { Destroy(ref baselineTexture); Destroy(ref resultTexture); Destroy(ref regionTexture); Destroy(ref modelTexture); Destroy(ref compareAfter); }
        public void Dispose()
        {
            Leave(); mesh?.Dispose(); mesh = null; meshKey = null; ReleaseTextures();
            if (canvasMaterial != null) Object.DestroyImmediate(canvasMaterial); canvasMaterial = null;
        }
    }
}
