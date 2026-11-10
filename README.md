# Avatar Texture Studio

An MIT Unity Editor package for avatar painting, UV inspection, editable region Colorways, manual Face Studio, texture comparison and SeamDoctor/MipDoctor diagnostics in one window.

**Shots 1–3: planned implementation complete, NOT ACCEPTED.** CPU/file and structural verification is available; actual Unity compilation, GPU rendering, interaction and Windows 11 avatar QA have not been run. Unity/Hub is unavailable on the development Mac. See [verification](docs/VERIFICATION.md) and the [Windows checklist](docs/WINDOWS_QA.md). Version 0.3.0 is unreleased; no release has been published.

The painting foundation derives from [VRC Texture Painter](https://github.com/ToffelsKater/VRC-Texture-Painter/tree/29bbf3c728d55091190413066e549b0bbbb21bf6), pinned to `29bbf3c728d55091190413066e549b0bbbb21bf6` (1.2.0), © 2026 ToffelsKater, MIT. [Attribution](THIRD_PARTY_NOTICES.md).

## Install in an isolated project first

Target **Unity 2022.3.22f1, Built-in Render Pipeline, Linear color space**. Windows 11 / Direct3D 11 is the primary acceptance platform; macOS / Metal remains unverified. No VRChat SDK, standalone executable or avatar-specific asset is needed.

1. Create an empty **3D (Built-In)** Unity project, separate from your avatar project. Set Player > Other Settings > Color Space to **Linear**.
2. **Remove VRC Texture Painter if present.** Retained asset GUIDs prevent co-installation. Save `.mtpaint` projects before removing it; Studio keeps the version 1/2 format.
3. Clone this repository/feature branch. In Unity, open **Window > Package Manager > + > Add package from disk** and select `Packages/com.teyocesu.avatar-texture-studio/package.json` from the clone. The repository root is not a Unity project.
4. Alternatively, run `python3 tools/package.py` and use **Add package from tarball** to select `artifacts/com.teyocesu.avatar-texture-studio-0.3.0.tgz`. This builds locally without publishing. Tarball installation avoids editing the checkout through a local package reference.
5. Wait for import/compilation, inspect Console, and open **Tools > Avatar Texture Studio**. Complete [Windows QA](docs/WINDOWS_QA.md) with synthetic fixtures first. Only then evaluate a backed-up copy of an avatar project.

The package adds no runtime scripts or avatar components. Installation and real compilation remain unverified in this phase.

## Paint and keep editable work

1. Select a scene avatar root or mesh, open **Painting**, and click **Use Selection**. With multiple compatible children choose the **Mesh**. You can also drop a renderer or use its component context menu. Prefab/model assets need a scene instance.
2. Choose material slots, texture properties and UV channels. Main/decal color slots live in Painting; other shader slots live in **Masks**. Unsupported non-UV mappings and transformed decals cannot start painting.
3. Set resolution, UV wrap and seam padding, then **Start Painting**. Original pixels form Base; color painting starts on a separate empty layer. Left drag paints in Scene view; Alt + drag orbits.
4. Use **Hard, Soft, Blur, Blend, Eraser, Gradient, Custom, Stamp** where supported. Masks offer supported tools, R/G/B/White/Black, Fill/Clear/Invert and optional on-model preview. Layers retain opacity, visibility, 13 blend modes, lock alpha, reorder, rename, duplicate, merge, fill/clear and image import.
5. Use **Undo/Redo** or Unity's Ctrl+Z/Ctrl+Y (platform editor shortcuts also apply). Multi-texture operations keep layer stacks together.
6. **Save / Save As** writes editable `.mtpaint` files inside or outside Assets. **Open Project** restores layers and renderer linkage. If the renderer cannot resolve, select its compatible scene instance and reopen. Save before important editor changes.

Reload and Edit/Play transitions commit the current stroke and autosave both sessions to `Library/AvatarTextureStudio/Autosave.mtpaint` and `AutosaveMask.mtpaint`. Closing dirty work asks Save/Discard/Cancel; cancelling/failing Save keeps the window open. A clean close retains session recovery for reopening the window. Undo history resets on reload/open. Failed restore files remain available for manual Open Project. SessionState recovery keys do not survive quitting Unity; reopen a saved project or the Library recovery file after restart. Autosave is not a backup and cannot retain new pixels if all writes fail.

The preserved [upstream manual](Packages/com.teyocesu.avatar-texture-studio/Documentation~/Upstream-README.md) explains detailed brush/UV behavior. It is historical: follow Studio's installation and export rules here.

## Export safely

**Export PNG** writes the flattened, padded 8-bit image. **Assign To Material is off by default.** Save a painting project separately to retain layers. Choose an Assets destination or set Output Folder; automatic names are numbered when occupied and use portable Windows-safe characters.

Existing PNG replacement always asks for approval. **Overwrite Source** remains explicit for PNG sources; **Create Texture** for an empty slot exports and asks before assignment. Enabling assignment asks before changing selected shared material assets and every renderer using them. Assignment supports Unity Undo; file replacement does not. Extract embedded FBX materials before assignment. New exports copy source importer settings; new masks use linear data semantics and do not paint alpha. Export rejects paths outside Assets and symbolic links/junctions.

If existing-file replacement fails, the error identifies retained recovery paths: the previous data may be at the original path or a `.backup.tmp` file, and completed new data may remain in a `.tmp` file. Keep those files. Copy project data to a new `.mtpaint` filename before reopening, or PNG data to a new `.png` filename. Successful replacement removes its transaction backup. Actual Unity Mono/Windows failure recovery still requires the QA checklist.

## Compare without ending painting

In **Compare**, select imported Texture2D A/B assets. Use **Side by Side**, **A**, **B**, **Overlay** (B opacity), or **Difference**. Scroll to zoom, drag to pan, double click or **Fit** to reset. Navigation is shared in normalized coordinates; Zoom × fit is relative to each pane, not a 1:1 pixel measure.

Metadata shows imported/source dimensions, format, color/data interpretation, mip count, CPU readability, type and alpha import mode. Read/Write may stay off. **Raw data values** displays sampled stored channels; use for masks. **Alpha only** exposes alpha separately; transparency uses a checkerboard. Overlay composites B over A using B's alpha multiplied by opacity. Difference shows `max(abs(A.rgb-B.rgb), abs(A.alpha-B.alpha))`, or alpha alone, with gain/clipping. It samples imported mip 0; it is not a perceptual score, source-file diff or seam diagnostic.

Different dimensions keep individual views available and block overlay/difference; no resampling is silently introduced. Null/deleted selections show placeholders. Normal-map imports, virtual-only textures, HDR/float formats and GPU-unsupported formats show their limitation. No texture/importer/material is modified. Returning to Painting/Masks retains session/history; Compare suspends Scene input and preview while visible.

## Limits

- Compatible arbitrary meshes are supported without a commercial avatar dependency. Shader mapping controls can still require manual UV selection. Normal-vector authoring is not provided.
- Retained upstream limits: 8-bit layers, memory-heavy GPU history (30 steps / approximately 2 GiB history budget), UV coverage limited to 16 tiles per triangle axis, and occlusion by the painted renderer only. A 4096² layer consumes about 64 MiB before history/scratch buffers.
- Projects support up to 32 textures, 1024 layers, 8192 pixels per axis, 1 MiB JSON fields and 2 GiB decoded layer pixels. Layer additions beyond the budget fail visibly; split large work. Actual GPU memory limits can be lower. Standard upstream version 1/2 files remain compatible within these bounds.
- Windows tablets, shader visuals, mask/color accuracy, recovery, interaction and real avatar integration are **NOT TESTED**. Metal is also untested. Inspection does not establish acceptance.

## Explore UVs and make Colorways

**UV Explorer**, **Colorway** and **Face Studio** share the same target, UV navigation, selection and creative document. Painting/Masks retain their own existing sessions and layers when switching workspaces.

1. In UV Explorer, select a scene MeshRenderer/SkinnedMeshRenderer, material slot, texture property and available UV0–UV7. **Use target** adopts a chosen texture from the open Painting session. **Refresh source geometry** updates mesh edits made elsewhere; pose/transform changes refresh automatically.
2. Inspect triangle edges and material scale/offset on the texture. **Raw transformed layout** exposes UVs outside 0–1; otherwise Repeat/Mirror triangles are clipped into sampled texture tiles. Wheel zoom and middle/Alt drag pan are shared across the creative workspaces. Pick a triangle in the canvas or enable **Pick selected renderer in Scene**. A Scene pick centers its UV point. The surface chooser lists all overlapping/mirrored/wrap matches; ambiguous picks select nothing until you choose explicitly. The selected triangle is highlighted in Scene view.
3. Read source UV, material UV, sampled UV and bottom-left pixel coordinates. **Capture color baseline** takes a frozen RGBA8 snapshot without enabling Read/Write on the source. For a matching Painting target it captures the current composite. The eyedropper samples the displayed baseline/result, excluding mask tint, and updates the shared color brush.
4. In Colorway, create/name a region, choose a triangle, and **Add triangle to region**. Refine with **Add pixels / Subtract pixels** and a pixel radius; strokes interpolate between pointer events. Cyan shows the active mask. Shared UV texels recolor every surface using them; choosing a surface cannot split a shared texture pixel.
5. Adjust **Hue rotation**, **Chroma ×** and **Strength** or disable a region's adjustment. **Update preview** computes the current result; cancellation retains the editable work. Oklab preserves perceptual lightness through hue/chroma changes and gamut reduction. It does not promise exact physical luminance. Unselected RGBA and all alpha bytes remain exact; RGB is quantized once to RGBA8 after the ordered adjustments. Creative Undo/Redo restores masks and parameters independently of Unity's painting history.
6. **Preview on model** temporarily displays the result, with optional region tint. **Compare before / after** opens owned snapshots in TextureLab. **Export new PNG** requires a new Assets filename, preserves source transfer/alpha/dimensions/wrap in the new import and never assigns it to a material. Save the editable Studio project separately. A same-size baseline refresh retains masks and can be undone; replacing dimensions prompts for saving/discarding creative work.

Verified shader correspondence is deliberately limited to `_MainTex` / UV0 on Standard, Standard (Specular setup), Unlit/Texture, Unlit/Transparent and Unlit/Transparent Cutout with material scale/offset. Other/custom/non-UV/decal mappings remain geometric inspection with an explicit warning; triangle-to-region and model previews are blocked for them. Direct image masking remains available for supported main/decal color properties. Normal-map, data-mask, HDR and virtual-only sources are excluded from color capture. UV1–UV7 can still be inspected geometrically; an arbitrary shader's sampling is not inferred from its channel number.

Collapsed UVs have no reverse area. Clamp/MirrorOnce tails collapse onto a border and cannot produce a unique border inverse; raw/Scene inspection remains available. Triangle masks exclude those collapsed tails. Wrapped selection is limited to 16 tiles per axis; geometry is bounded to 250,000 triangles per selected slot and approximately one million sampled fragments. Excess geometry is reported, not silently truncated into regions. Picking considers the selected renderer's triangles, including backfaces; it does not resolve occlusion by other renderers. Large layouts/images can be slow and memory intensive; performance is not yet measured in Unity.

## Keep editable creative and Face work

**Save / Save As / Open Studio** use self-contained `.atsstudio` files: baseline pixels, named masks, editable adjustments, Face flags, texture binding and navigation. `ATSCRV01` version 1 is independent of MTPAINT1/2; `.mtpaint` bytes/loaders are unchanged. Saving a related color painting also saves `<painting>.atsstudio`; opening that painting restores a matching companion after unsaved-work handling. Each file is replaced independently. If companion saving fails or is cancelled, the painting may already be saved; creative work stays dirty with an explicit message. Replacing a different existing companion asks first.

Creative recovery uses `Library/AvatarTextureStudio/CreativeAutosave.atsstudio` and Unity SessionState across reload/Play, retaining saved path and dirty state. Failed loads retain the current document and recovery file. After quitting Unity, manually open the saved/Library `.atsstudio` file and select its recorded renderer/material/property/UV binding. No Unity object references or history are stored in the sidecar. Limits are 8192 per axis, 32 regions, 512 MiB decoded data with metadata reserve, and 4096-character fields. Creative history retains at most 24 steps / 128–256 MiB of additional unique snapshots. Large work may hit machine memory limits earlier.

In **Face Studio**, manually select the mesh/texture and create a **New face preset** or mark any shared region as a **Face preset**. Name it as useful for your avatar; there is no anatomy or face detection assumption. Use the same UV/mask/color controls for precise work. **Paint with existing tools** activates the matching color Painting session and shows its real brushes/layers/output below. If another color session is open, save/stop it or adopt that target; it is never discarded. Region masks constrain Colorway recoloring; existing paint brushes keep their ordinary layer behavior. Face painting requires identity material tiling/offset because the retained painter uses raw UVs. Capture a new composite explicitly to bring painting edits into the frozen Colorway baseline. While Face painting is active, Compare uses that composite as the after snapshot.

## Review seams, mipmaps and export

**SeamDoctor** and **MipDoctor** share the existing target and UV navigation. Use the two-row workspace selector to move between editing, comparison and diagnostics without closing painting/mask/creative documents.

1. Select/adopt a renderer, material property and UV channel. Choose an explicit diagnostic source: imported texture GPU mip 0, matching Painting/Masks composite, or current Colorway result. Shader-data/mask sources use raw-channel diagnostics. Normal packing, HDR and virtual-only inputs are rejected. A GPU readback may already contain platform compression; it is not original file pixels.
2. **Capture and analyze** freezes pixels/topology and builds CPU diagnostic levels. Recapture after editing, Undo, posing, reimport or shader changes. Geometry/selection/Colorway changes invalidate model overlays; source freshness is not reverified automatically. Failed/cancelled work leaves the previous labelled snapshot and all editable work intact.
3. In **SeamDoctor**, choose a candidate pair. Cyan/magenta edges show its two UV sides and corresponding 3D edge. Read triangle/vertex/submesh/slot/channel identities, sample counts, unresolved samples, mean/worst Oklab×100, alpha and composite/raw-channel differences. Oklab is N/A for data or without two visible colors. Both-transparent hidden RGB is not a visible-color defect.
4. Candidate geometry is deliberately conservative: exact source positions, unique opposing edges, indexed manifold connectivity, matching posed endpoints and one material/submesh. Disconnected UV islands, unrelated coincident pieces, nonmanifold/duplicate edges, collapsed UVs and uncertain shader mappings remain unmeasured. Consequently some real seams are missed. No relationship is invented from positional coincidence alone. Cross-material/ambiguous-slot seams are unsupported.
5. In **MipDoctor**, select CPU levels through 1×1, inspect alpha and **Measure this CPU level**. The recursive area-box filter integrates odd dimensions, decodes sRGB for color, filters premultiplied linear color/alpha, clears RGB when output alpha rounds to zero, and quantizes each level to RGBA8. Raw data averages RGBA independently. The deterministic filter is **not exact Unity mip generation, platform compression or VRChat rendering**.
6. **Compare base / CPU level** opens independent owned copies in TextureLab, with explicit CPU provenance. Differing dimensions retain its side-by-side/individual views and overlay restriction. These copies survive regeneration/release of caller previews and are freed by TextureLab when replaced/closed.
7. The quality summary separates measurements, potential boundary/exterior-ring contrasts, unmeasured/unsupported geometry and unverified rendering. A normalized contrast above 0.05 is a review hint, not an error verdict; deliberate hard transitions and alpha boundaries may be correct. Exterior probes and increases at reduced resolution can suggest bleeding/padding concerns without proving a visible defect.
8. **Export report** writes a new bounded UTF-8 `.txt` report with provenance, limitations, counts and per-level metrics. **Export snapshot PNG** writes the frozen **base** image, not the selected reduced mip, to a new Assets filename. It preserves the existing explicit new-PNG contract and never assigns materials or overwrites sources. Diagnostic warnings do not change compression, importers, texture pixels or avatar data. Painting/Colorway export remains available through the linked workspaces.

Limits: whole-source topology ≤250,000 triangles, ≤10,000 candidate pairs, ≤256 samples/edge and one million/level, existing UV tile/fragment limits, ≤14 CPU mip levels / 384 MiB decoded chain pixels and ≤8 MiB report text. Very thin triangles at low resolution can have no interior samples; those results are N/A. Ordinary bilinear diagnostic sampling and area-box mip filtering do not model a shader's actual sampler. Large-workload latency/memory and native source preservation remain physical QA gates. No automatic seam repair or export-readiness PASS is provided.

## Develop and verify

- `python3 tools/verify_repository.py`: package/GUID/shader/license/provenance integrity.
- `dotnet run --project Tests/FileSafety/FileSafety.csproj -- <absolute-repository-path>`: .NET 8 filesystem, creative and quality regressions plus C# 9 syntax parsing using SDK-bundled Roslyn. This is **not Unity compilation**. Symlink tests need permission to create links.
- Open **Tests/UnityProject** in licensed Unity 2022.3.22f1. Its local dependency points at this checkout and Player settings declare Linear. Run the preserved GPU suite and Studio checks with [Windows QA commands](docs/WINDOWS_QA.md), never in an avatar project.

[SPEC](SPEC.md) · [PLAN](PLAN.md) · [architecture](docs/ARCHITECTURE.md) · [upstream audit](docs/UPSTREAM.md) · [verification](docs/VERIFICATION.md) · [HANDOFF](HANDOFF.md).
