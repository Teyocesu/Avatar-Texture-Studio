# Avatar Texture Studio

An MIT Unity Editor package for painting avatar textures and shader masks, keeping editable layers/projects, exporting PNGs, and comparing imported textures in one window.

**Shot 1: implemented but not accepted.** File-level verification is available; actual Unity compilation, GPU rendering, interaction and Windows 11 avatar QA have not been run. Unity/Hub is unavailable on the development Mac. See [verification](docs/VERIFICATION.md) and the [Windows checklist](docs/WINDOWS_QA.md). No release has been published.

The painting foundation derives from [VRC Texture Painter](https://github.com/ToffelsKater/VRC-Texture-Painter/tree/29bbf3c728d55091190413066e549b0bbbb21bf6), pinned to `29bbf3c728d55091190413066e549b0bbbb21bf6` (1.2.0), © 2026 ToffelsKater, MIT. [Attribution](THIRD_PARTY_NOTICES.md).

## Install in an isolated project first

Target **Unity 2022.3.22f1, Built-in Render Pipeline, Linear color space**. Windows 11 / Direct3D 11 is the primary acceptance platform; macOS / Metal remains unverified. No VRChat SDK, standalone executable or avatar-specific asset is needed.

1. Create an empty **3D (Built-In)** Unity project, separate from your avatar project. Set Player > Other Settings > Color Space to **Linear**.
2. **Remove VRC Texture Painter if present.** Retained asset GUIDs prevent co-installation. Save `.mtpaint` projects before removing it; Studio keeps the version 1/2 format.
3. Clone this repository/feature branch. In Unity, open **Window > Package Manager > + > Add package from disk** and select `Packages/com.teyocesu.avatar-texture-studio/package.json` from the clone. The repository root is not a Unity project.
4. Alternatively, run `python3 tools/package.py` and use **Add package from tarball** to select `artifacts/com.teyocesu.avatar-texture-studio-0.1.0.tgz`. This builds locally without publishing. Tarball installation avoids editing the checkout through a local package reference.
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

## Compare without ending painting

In **Compare**, select imported Texture2D A/B assets. Use **Side by Side**, **A**, **B**, **Overlay** (B opacity), or **Difference**. Scroll to zoom, drag to pan, double click or **Fit** to reset. Navigation is shared in normalized coordinates; Zoom × fit is relative to each pane, not a 1:1 pixel measure.

Metadata shows imported/source dimensions, format, color/data interpretation, mip count, CPU readability, type and alpha import mode. Read/Write may stay off. **Raw data values** displays sampled stored channels; use for masks. **Alpha only** exposes alpha separately; transparency uses a checkerboard. Overlay composites B over A using B's alpha multiplied by opacity. Difference shows `max(abs(A.rgb-B.rgb), abs(A.alpha-B.alpha))`, or alpha alone, with gain/clipping. It samples imported mip 0; it is not a perceptual score, source-file diff or seam diagnostic.

Different dimensions keep individual views available and block overlay/difference; no resampling is silently introduced. Null/deleted selections show placeholders. Normal-map imports, virtual-only textures, HDR/float formats and GPU-unsupported formats show their limitation. No texture/importer/material is modified. Returning to Painting/Masks retains session/history; Compare suspends Scene input and preview while visible.

## Limits

- Compatible arbitrary meshes are supported without a commercial avatar dependency. Shader mapping controls can still require manual UV selection. Normal-vector authoring is not provided.
- Retained upstream limits: 8-bit layers, memory-heavy GPU history (30 steps / approximately 2 GiB history budget), UV coverage limited to 16 tiles per triangle axis, and occlusion by the painted renderer only. A 4096² layer consumes about 64 MiB before history/scratch buffers.
- Projects support up to 32 textures, 1024 layers, 8192 pixels per axis, 1 MiB JSON fields and 2 GiB decoded layer pixels. Layer additions beyond the budget fail visibly; split large work. Actual GPU memory limits can be lower. Standard upstream version 1/2 files remain compatible within these bounds.
- Windows tablets, shader visuals, mask/color accuracy, recovery, interaction and real avatar integration are **NOT TESTED**. Metal is also untested. Inspection does not establish acceptance.

## Develop and verify

- `python3 tools/verify_repository.py`: package/GUID/shader/license/provenance integrity.
- `dotnet run --project Tests/FileSafety/FileSafety.csproj -- <absolute-repository-path>`: .NET 8 filesystem regressions and C# 9 syntax parsing using SDK-bundled Roslyn. This is **not Unity compilation**. Symlink tests need permission to create links.
- Open **Tests/UnityProject** in licensed Unity 2022.3.22f1. Its local dependency points at this checkout and Player settings declare Linear. Run the preserved GPU suite and Studio checks with [Windows QA commands](docs/WINDOWS_QA.md), never in an avatar project.

[SPEC](SPEC.md) · [PLAN](PLAN.md) · [architecture](docs/ARCHITECTURE.md) · [upstream audit](docs/UPSTREAM.md) · [verification](docs/VERIFICATION.md) · [HANDOFF](HANDOFF.md).
