using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace AvatarTextureStudio.TextureLab
{
    /// <summary>Read-only asset/snapshot comparison. Owns its material and independent snapshot copies, never imported assets.</summary>
    [Serializable]
    internal sealed class TextureComparePanel : IDisposable
    {
        static readonly string[] Views = { "Side by Side", "A", "B", "Overlay", "Difference" };
        [SerializeField] Texture2D textureA;
        [SerializeField] Texture2D textureB;
        [SerializeField] int view;
        [SerializeField] bool rawValues;
        [SerializeField] bool alphaOnly;
        [SerializeField] float opacity = 0.5f;
        [SerializeField] float gain = 1f;
        [SerializeField] float zoom = 1f;
        [SerializeField] Vector2 pan;
        [NonSerialized] Material material;
        [NonSerialized] int dragControl;
        [NonSerialized] bool ownedPreviews;
        [NonSerialized] string snapshotProvenance;
        [NonSerialized] Texture2D ownedA, ownedB;

        public void SetPreviews(Texture2D before, Texture2D after, bool asRawData = false, string provenance = null)
        {
            string error = UnsupportedReason(before, true) ?? UnsupportedReason(after, true);
            if (error != null) throw new InvalidOperationException(error);
            var nextA = Object.Instantiate(before); Texture2D nextB;
            try { nextB = Object.Instantiate(after); }
            catch { Object.DestroyImmediate(nextA); throw; }
            nextA.hideFlags = nextB.hideFlags = HideFlags.HideAndDontSave;
            ReleaseOwned(); ownedA = nextA; ownedB = nextB;
            textureA = ownedA; textureB = ownedB; ownedPreviews = true;
            rawValues = asRawData; alphaOnly = false; view = 0; snapshotProvenance = provenance; ResetNavigation();
        }

        public void Draw(Action repaint)
        {
            if (Event.current.rawType == EventType.MouseUp) EndInteraction();
            EditorGUILayout.LabelField("TextureLab · Compare", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(ownedPreviews ? "Read-only Studio snapshots. Editable work stays in its workspace. Select assets below to return to asset comparison."
                : "Read-only imported assets. Painting and mask sessions stay open while you compare. CPU Read/Write access is not required.", MessageType.Info);
            if (ownedPreviews && !string.IsNullOrEmpty(snapshotProvenance)) EditorGUILayout.HelpBox(snapshotProvenance, MessageType.Info);
            EditorGUI.BeginChangeCheck();
            textureA = (Texture2D)EditorGUILayout.ObjectField("Texture A", textureA, typeof(Texture2D), false);
            textureB = (Texture2D)EditorGUILayout.ObjectField("Texture B", textureB, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck()) { ReleaseOwned(); ownedPreviews = false; snapshotProvenance = null; ResetNavigation(); }
            DrawMetadata("A", textureA);
            DrawMetadata("B", textureB);

            view = GUILayout.Toolbar(Mathf.Clamp(view, 0, Views.Length - 1), Views);
            rawValues = EditorGUILayout.Toggle(new GUIContent("Raw data values", "Show sampled stored channels without interpreting them as color. Use for masks; never changes import settings."), rawValues);
            alphaOnly = EditorGUILayout.Toggle("Alpha only", alphaOnly);
            if (view == 3) opacity = EditorGUILayout.Slider("B opacity", opacity, 0f, 1f);
            if (view == 4)
            {
                gain = EditorGUILayout.Slider("Difference gain", gain, 1f, 16f);
                EditorGUILayout.HelpBox("Absolute sampled difference: max(|ΔRGB|, |ΔAlpha|), or |ΔAlpha| in Alpha only. Gain multiplies and clips at white. This is not a perceptual score or a source-file comparison.", MessageType.None);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                zoom = EditorGUILayout.Slider("Zoom × fit", zoom, 0.25f, 32f);
                if (GUILayout.Button("Fit", GUILayout.Width(42))) ResetNavigation();
            }
            EditorGUILayout.LabelField("Scroll to zoom · drag to pan · double click to fit. Navigation is shared in normalized image coordinates.", EditorStyles.wordWrappedMiniLabel);

            string errorA = UnsupportedReason(textureA, ownedPreviews);
            string errorB = UnsupportedReason(textureB, ownedPreviews);
            if (textureA != null && errorA != null) EditorGUILayout.HelpBox("A: " + errorA, MessageType.Warning);
            if (textureB != null && errorB != null) EditorGUILayout.HelpBox("B: " + errorB, MessageType.Warning);
            bool mismatch = textureA != null && textureB != null && (textureA.width != textureB.width || textureA.height != textureB.height);
            if (mismatch) EditorGUILayout.HelpBox("Dimensions differ. Each side uses its own aspect-correct fit; overlay and difference require matching dimensions. No resizing is performed.", MessageType.Warning);
            if (view >= 3 && (textureA == null || textureB == null || errorA != null || errorB != null || mismatch))
            {
                EditorGUILayout.HelpBox("Select two supported textures with matching dimensions for this view. Side by Side and individual A/B views remain available.", MessageType.Info);
                return;
            }

            var shader = Shader.Find("Hidden/AvatarTextureStudio/Compare");
            if (shader == null || !shader.isSupported)
            {
                EditorGUILayout.HelpBox("The Compare shader is missing or unsupported on this GPU. Reimport the package or use a supported graphics device.", MessageType.Error);
                return;
            }
            if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            Rect canvas = GUILayoutUtility.GetRect(0, 360, GUILayout.ExpandWidth(true));
            if (view == 0)
            {
                float half = (canvas.width - 8) * 0.5f;
                DrawPane(new Rect(canvas.x, canvas.y, half, canvas.height), textureA, 0, "A", errorA, repaint);
                DrawPane(new Rect(canvas.x + half + 8, canvas.y, half, canvas.height), textureB, 1, "B", errorB, repaint);
            }
            else
                DrawPane(canvas, view == 2 ? textureB : textureA, view - 1, Views[view], view == 2 ? errorB : errorA, repaint);
        }

        void DrawPane(Rect area, Texture2D primary, int mode, string label, string error, Action repaint)
        {
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 6, area.y + 3, area.width - 12, 18), label, EditorStyles.boldLabel);
            var viewport = new Rect(area.x + 1, area.y + 24, Mathf.Max(1, area.width - 2), Mathf.Max(1, area.height - 25));
            if (primary == null || error != null)
            {
                GUI.Label(viewport, primary == null ? "Select texture " + label : "Preview unavailable", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleNavigation(viewport, primary, repaint);
            if (Event.current.type != EventType.Repaint) return;
            float scale = FitScale(viewport, primary) * zoom;
            Vector2 size = new Vector2(primary.width, primary.height) * scale;
            Vector2 center = viewport.size * 0.5f + Vector2.Scale(pan, size);
            var image = new Rect(center - size * 0.5f, size);
            material.SetTexture("_TexA", textureA != null ? textureA : primary);
            material.SetTexture("_TexB", textureB != null ? textureB : primary);
            material.SetFloat("_Mode", mode);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Gain", gain);
            material.SetFloat("_RawValues", rawValues ? 1f : 0f);
            material.SetFloat("_AlphaOnly", alphaOnly ? 1f : 0f);
            material.SetFloat("_LinearProject", QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f);
            material.SetFloat("_SrgbA", IsSRGB(textureA != null ? textureA : primary) ? 1f : 0f);
            material.SetFloat("_SrgbB", IsSRGB(textureB != null ? textureB : primary) ? 1f : 0f);
            GUI.BeginGroup(viewport);
            try { EditorGUI.DrawPreviewTexture(image, primary, material, ScaleMode.StretchToFill); }
            finally { GUI.EndGroup(); }
        }

        void HandleNavigation(Rect viewport, Texture2D primary, Action repaint)
        {
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive, viewport);
            if (e.type == EventType.MouseDown && viewport.Contains(e.mousePosition) && (e.button == 0 || e.button == 2))
            {
                if (e.clickCount == 2) ResetNavigation();
                else
                {
                    GUIUtility.hotControl = control;
                    dragControl = control;
                }
                e.Use();
                repaint();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == control && dragControl == control)
            {
                float scale = FitScale(viewport, primary) * zoom;
                pan += new Vector2(e.delta.x / (primary.width * scale), e.delta.y / (primary.height * scale));
                e.Use();
                repaint();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == control && dragControl == control)
            {
                GUIUtility.hotControl = 0;
                dragControl = 0;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition))
            {
                float next = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * 0.08f), 0.25f, 32f);
                Vector2 size = new Vector2(primary.width, primary.height) * FitScale(viewport, primary) * zoom;
                Vector2 offset = e.mousePosition - viewport.center;
                pan += new Vector2(offset.x / size.x, offset.y / size.y) * (zoom / next - 1f);
                zoom = next;
                e.Use();
                repaint();
            }
        }

        static float FitScale(Rect viewport, Texture texture) => Mathf.Min(viewport.width / texture.width, viewport.height / texture.height);
        void ResetNavigation() { EndInteraction(); zoom = 1f; pan = Vector2.zero; }
        static bool IsSRGB(Texture texture) => GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);

        internal static string UnsupportedReason(Texture2D texture) => UnsupportedReason(texture, false);

        internal static string UnsupportedReason(Texture2D texture, bool allowOwnedPreview)
        {
            if (texture == null) return "Select a Texture2D asset.";
            if (!allowOwnedPreview && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture))) return "Select an imported asset from the Project window.";
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                return "Normal-map imports use platform-specific packing. Compare plain color/data copies instead; source import settings will not be changed.";
            if (importer != null && importer.vtOnly) return "Virtual-texture-only assets have no ordinary sampled preview.";
            switch (texture.format)
            {
                case TextureFormat.RHalf: case TextureFormat.RGHalf: case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat: case TextureFormat.RGFloat: case TextureFormat.RGBAFloat:
                case TextureFormat.RGB9e5Float: case TextureFormat.BC6H:
                    return "HDR/float textures require an exposure-aware comparison, deferred beyond Shot 1.";
            }
            if (!SystemInfo.IsFormatSupported(texture.graphicsFormat, FormatUsage.Sample))
                return "The graphics device cannot sample this imported format: " + texture.graphicsFormat;
            return null;
        }

        static void DrawMetadata(string label, Texture2D texture)
        {
            if (texture == null) return;
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            string source = "";
            if (importer != null)
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                source = $" · source {w}×{h} · {importer.textureType} · alpha {importer.alphaSource}";
            }
            EditorGUILayout.LabelField($"{label}: {texture.width}×{texture.height} · {texture.format} · {(IsSRGB(texture) ? "sRGB" : "linear/data")} · {texture.mipmapCount} mips · Read/Write {(texture.isReadable ? "on" : "off")}{source}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(path, EditorStyles.wordWrappedMiniLabel);
        }

        public void EndInteraction()
        {
            if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            dragControl = 0;
        }

        public void Dispose()
        {
            EndInteraction();
            ReleaseOwned();
            if (material != null) Object.DestroyImmediate(material);
            material = null;
        }
        void ReleaseOwned()
        {
            if (textureA == ownedA || textureA == ownedB) textureA = null;
            if (textureB == ownedA || textureB == ownedB) textureB = null;
            if (ownedA != null) Object.DestroyImmediate(ownedA);
            if (ownedB != null) Object.DestroyImmediate(ownedB);
            ownedA = ownedB = null;
        }
    }
}
