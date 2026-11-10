# Avatar Texture Studio — Canonical specification

Shot 3 below is the active contract. Shots 1 and 2 remain **implemented but not accepted**; no missing native acceptance gate is waived.

## Shot 3 — Quality Studio

### Delivery and integration

Create `codex/shot-3-quality-studio` from exact Shot 2 commit `a5c90a471a8af9c75ead8cb366c891f6ef89738a`; draft PR targets `codex/shot-2-creative-studio`. Leave main/Shot 1/Shot 2 refs unchanged. No merge, tag, release or owner avatar project. Target remains Unity 2022.3.22f1 / Built-in / Linear / Windows 11 DX11; native Unity/Windows/Metal acceptance stays NOT TESTED.

Add SeamDoctor and MipDoctor within the same window, sharing target, UV navigation and TextureLab without changing paint sessions or editable documents. Diagnostics use explicit immutable snapshots: imported GPU mip-0 readback (possibly already platform-compressed), matching painting/mask composite, or the current Colorway result. Label provenance, transfer/data mode and snapshot staleness. No diagnostic warning automatically changes any image, mesh, importer, material or avatar. Existing `.mtpaint` and ATSCRV01 editability stays unchanged; diagnostic results are ephemeral and may be exported as a separate report.

### Conservative seam correspondence

Preserve source triangle, vertex-index, submesh/material-slot and UV-channel identity, source positions and posed world positions. Inspect whole-source topology within a 250,000-triangle ceiling, reusing CreativeMeshAdapter and UvGeometry. Do not weld or mutate source geometry. Group edges by exact source-position endpoints; approximate proximity is insufficient. A measurable pair requires exactly two nondegenerate incident edges, opposing geometric orientation, consistent source vertex data, coincident posed endpoints, the same indexed connected component, the same selected material slot/submesh and separated UV coordinates. Reject duplicate/nonmanifold, disconnected, inconsistent, deformed-apart or cross-material pairs explicitly. UV mirroring alone does not invalidate a pair.

This is a topology-constrained candidate test, not proof of author intent. UV-split islands that are disconnected in source index topology cannot be distinguished reliably from unrelated coincident pieces and remain uncertain/unmeasured. Report those false negatives instead of inventing adjacency. Ordinary unsplit UV edges and open boundaries are counted separately. Respect existing UV tile/fragment limits; unsupported/degenerate mappings do not silently produce valid samples. Custom shader correspondence remains unverified.

For validated candidates, compare matched edge parameters in the correct reversed orientation. Sample pixel-center-scale offsets into each triangle with documented wrap modes; unresolved thin/border samples remain counted as unmeasured. Bound samples to 256 per edge and one million per level, with cancellable analysis. Record per-edge counts, mean/worst values and worst parameter. Color metrics: Euclidean Oklab distance ×100 for two visible samples, absolute alpha difference, and maximum premultiplied/composited-on-black-or-white linear RGB difference. Both-transparent samples have no visible-color distance; hidden RGB is not labelled a visible defect. Data mode compares raw RGBA channels without sRGB decoding or premultiplication.

Probe a one-texel exterior ring separately from interior samples. Normalized composite/raw-channel differences above 0.05 are labelled potential boundary contrast/padding risk, never confirmed errors or insufficient padding. A deliberate hard transition can be correct. Reports distinguish measured numbers, potential concerns, rejected/unsupported pairings and unverified native/shader behavior. Selected seam edges are visible in UV and Scene views. Export a bounded UTF-8 text report to a new filename using SafeFileIO; never replace a source image or existing report silently.

### Deterministic CPU mip diagnostics

Generate a recursive area-box chain through 1×1, with dimensions `max(1, floor(previous / 2))` and full-area integration for odd/NPOT dimensions. Color filtering decodes sRGB when appropriate, averages premultiplied linear RGB and alpha, unpremultiplies when alpha is nonzero and quantizes each level to RGBA8. Fully transparent output RGB is zero. Data filtering averages stored RGBA independently. Limits are 8192 per axis, at most 14 levels and 384 MiB of decoded chain pixels; construction is cancellable and never changes the source image.

Display every level with color/raw/alpha inspection, source/reduced dimensions and explicit **CPU diagnostic** provenance. Compare base/selected levels through owned TextureLab snapshots; unequal dimensions retain its existing overlay restriction. Measure validated seams at selected reduced levels with the same metrics. Exterior-ring contrasts and increases at reduced resolution are potential bleeding indicators, not proof of Unity/VRChat appearance. CPU box filtering/quantization is not Unity's mip filter, actual imported mip data or GPU compression. Real Unity-generated mip and platform-compression comparisons remain NOT TESTED here.

### Quality review, safety and completion

A compact summary links UV inspection, existing editing, before/after comparison, seam/mip measurements, limitations and explicit export. Never show generic PASS/readiness for unverified rendering. Reuse the new-PNG export contract: new Assets filename, source transfer/alpha/dimensions/wrap, no assignment or source overwrite; only the explicitly exported new asset's importer may be configured. Diagnostics themselves remain read-only. Reject incompatible texture types/normal packing/HDR/virtual-only formats visibly.

Keep quality snapshots/owned textures isolated from creative undo/persistence. Refresh invalidates stale geometry overlays; tab/lifecycle teardown releases owned textures and subscriptions. Failed/cancelled analysis retains the previous labelled snapshot/results and editable work. Preserve upstream arithmetic, capability gates, file safety, assertions, GUIDs and MIT notices. No universal seam repair, UV/mesh editing, automatic compression/material changes, new image editor, runtime components, services or publishing.

Execute focused CPU regressions for known connected seams, reversal, disconnected coincidence, hard/continuous colors, mirrored UVs, slot/channel boundaries, alpha/color/data semantics, NPOT mip determinism/contamination, preservation, cancellation and limits. Preserve and run all existing available checks. Author proportionate native integration checks but leave them unexecuted without Unity. Consolidate Windows QA and review the stacked product for lifecycle, preview, persistence, ownership, installation, licensing and negative scope. Implementation of the planned product may finish; release/acceptance stays blocked until the real native gates pass.

## Shot 2 — Creative Studio

### Delivery and scope

Stack `codex/shot-2-creative-studio` from exact Shot 1 commit `9b1d2f0e43883079e5a3596a49832cbd0668b77e`; draft PR targets `codex/shot-1-painting-foundation`. Do not modify that branch or main, merge, tag, release, or load the owner's avatar project. Retain editor-only Unity 2022.3.22f1 / Built-in / Linear / Windows 11 DX11 as the primary target, with Metal unverified.

One IMGUI window retains Painting, Masks and Compare and adds UV Explorer, Colorway and Face Studio. All creative workspaces share one inspected renderer/material slot/property/channel, UV navigation, explicit surface selection and creative document. Existing painting sessions/layers remain open when changing workspace. Face Studio invokes the existing color-paint session/tools, not a second paint engine. Region restrictions apply to Colorway adjustments; existing brush behavior remains the upstream layer workflow.

### UV correspondence before derived editing

Use source mesh UV0–UV7 and submesh/material-slot associations, with posed world vertices for Scene correspondence. Reuse upstream mesh selection, submesh association, scale-aware skin bake resolution, pose hashing and restorable property-block preview. Keep source geometry/UVs immutable. The mathematical correspondence kernel is independent of Unity and must pass synthetic forward/reverse/wrap/ambiguity tests before region-derived operations are implemented.

UV Explorer shows texture, triangle edges, selected polygons, UV/pixel coordinates, material/property/channel, and shared pan/zoom. Scene picking maps a nearest surface to source UV, material scale/offset and wrapped texture UV. Reverse lookup returns **all** candidate surfaces/aliases; multiple hits require explicit user choice and never default to the first. Equal-depth Scene hits are also explicit candidates. Collapsed UVs are reported, not rasterized into regions. Repeat/Mirror clip triangles into sampled tiles; Clamp/MirrorOnce border collapse is many-to-one and flagged rather than treated as a unique inverse. Raw transformed UV layout remains inspectable outside 0–1. Tile spans beyond 16 per axis are reported as unsupported for wrapped selection, matching the existing painting ceiling.

Shader correspondence is supported for ordinary UV texture mapping and material scale/offset. Known non-UV modes, transformed decals, channel disagreement and unverified custom shader mappings must be labelled. Raw UV inspection remains available; derived geometry selections require a supported correspondence. Explicit pixel/brush masking remains an image-space operation. Color sampling is available for supported color sources and can update the shared brush color; normal/data-mask sampling must not masquerade as color editing.

### Regions and color processing

Capture an immutable RGBA8 baseline from the chosen color-paint document's composite, or a supported imported color texture without changing its importer. Named regions are immutable binary pixel masks refined with UV triangles and add/subtract circular pixel brushes. Every edit is reversible through bounded creative Undo/Redo. Region overlays are visible on the texture and model through temporary previews. Shared/mirrored UV pixels necessarily affect all corresponding surfaces; disclose that limitation.

Use one method: sRGB transfer decoding when the source is sRGB, Oklab hue rotation/chroma scaling at fixed perceptual lightness L, strength interpolation in a/b, and radial chroma reduction into the RGB gamut at fixed L. Encode back to the source transfer function and quantize once to RGBA8. Alpha is copied exactly; unselected pixels remain byte-identical. Identity/zero-strength adjustments are exact no-ops. Regions apply in list order; disabling or changing any adjustment remains editable. This preserves Oklab lightness/shading within 8-bit quantization, not exact physical luminance Y. No normal maps, shader-data masks, HDR or unsupported color sources enter this processing path.

Preview is explicit and cancellable for large textures; export computes current edits and writes a **new** PNG asset without assignment or overwrite. Colorway results remain a separate editable adjustment document, avoiding a flattened layer that would falsely claim editable parameters or change alpha through double compositing. Refreshing a baseline is explicit and reversible at the same dimensions; replacing it at different dimensions requires saving/discarding the prior creative work.

Face Studio is a focused view of the same regions, with named face-preset flags, precise navigation, sampling, the existing painting tools and masked recoloring. Users choose anatomy/mesh/regions manually. Compare accepts the owned before/after snapshots as read-only transient previews in TextureLab, while regular asset comparison remains available.

### Persistence decision (fixed before implementation)

Keep `.mtpaint` MTPAINT1/2 bytes and loader compatibility unchanged. Creative documents use a self-contained, versioned `.atsstudio` sidecar: magic `ATSCRV01`, version 1, a checked compressed-byte length, and bounded binary metadata and gzip payload containing immutable baseline RGBA8, source transfer semantics, renderer/material/property/UV/wrap/transform binding, named mask bitsets, editable adjustment parameters, Face preset flags and navigation/active-region state. No Unity object references are serialized. This is editable state, not just an exported image. The length field was added before any delivery after an executed regression showed that a complete decoded payload with a truncated gzip footer can otherwise be accepted by .NET.

Standalone Save/Open is available. Saving a related color painting project also writes `<painting>.atsstudio`; opening it offers/restores a matching companion without discarding dirty creative work. Each file is independently replaced using Shot 1 SafeFileIO; the pair is **not** a multi-file transaction. A partial save is reported and the creative document stays dirty. Autosave/restore across Play/reload uses a separate Library creative file and retains original project path/dirty state. Failed loads never replace the current document; malformed payloads are bounded and rejected visibly. Editor restart recovery is manual as in Shot 1.

Limits: 8192 per texture axis, 32 regions, 512 MiB decoded creative data (1 MiB reserved for metadata), 4096 characters per text field, and session-local history of at most 24 steps / 128 MiB of retained unique data, raised to one baseline's byte size when larger (maximum 256 MiB) so a full-size baseline refresh stays undoable. Geometry is bounded to 250,000 source triangles per selected slot and approximately one million sampled fragments; triangles beyond the fragment budget stay raw-inspectable and are flagged. Mask precision is one bit per pixel; recolored output is RGBA8. No source textures/materials/meshes/importers, runtime/avatar components or upstream GUIDs are mutated by creative operations. New PNG importer settings may be set to preserve the exported result. All temporary textures, previews and Scene subscriptions are released/restored.

### Evidence and exclusions

Execute synthetic CPU checks for correspondence/ambiguity, mirrored/wrapped/collapsed UVs, material/channel association, region isolation, alpha/outside-mask preservation, color semantics, reversible edits, sidecar round trips/corruption and file safety. Preserve all existing tests. Run package/GUID/license checks, inspect the complete stacked diff and document Windows procedures. Native Unity import/API compilation, GPU/Metal, Scene/IMGUI interaction, skinning and Windows/avatar acceptance remain NOT TESTED without actual execution. CPU tests are not substitutes for those gates.

No UV remapping, mesh editing, anatomy recognition, AI textures, texture transfer, seam/mipmap repair, new painting engine, accounts/cloud, monetization or publishing. Shot 2 may be implemented but cannot be accepted until its native gates and inherited Shot 1 gates pass.

## Shot 1 foundation contract

## Objective and users

An MIT, editor-only Unity texture studio for VRChat avatar creators. Shot 1 delivers the upstream painting workflow plus independent, read-only texture comparison. Target Unity 2022.3.22f1, Built-in Render Pipeline, Windows 11 / Direct3D 11 / Linear color space. macOS / Metal is a secondary, unverified development platform. Compatible MeshRenderer and SkinnedMeshRenderer meshes are supported without an avatar-specific dependency.

## Scope and workflow

- Install `com.teyocesu.avatar-texture-studio` as a UPM package; open **Tools > Avatar Texture Studio**.
- One window contains **Painting**, **Masks**, and **Compare**. Select a renderer (or choose a compatible renderer below an avatar root), material slots, texture properties, UV channels, and resolution. Start a session and paint in Scene view.
- Retain upstream hard/soft/blur/blend/eraser/gradient/custom/stamp brushes, multi-texture painting, mirroring, seam padding, layers and their operations, GPU undo/redo, per-channel masks, and pose baking. Mask tools stay restricted to supported operations.
- Save/reopen editable `.mtpaint` projects, including upstream version 1 and 2 files. Autosave across script reload and Edit/Play transitions. Undo history is session-local and is not serialized.
- Export flattened PNGs separately. Assignment is off by default. Existing-file replacement and shared-material assignment require explicit UI approval; retain explicit Overwrite Source functionality.
- Compare accepts two imported Texture2D assets with side-by-side, A, B, overlay opacity, and absolute RGBA difference views; shared fit-relative zoom/pan, dimensions/metadata, RGB and alpha inspection. Unequal dimensions are disclosed and disable overlay/difference. No importer changes or CPU-readable requirement. Unsupported virtual/normal/HDR formats are explained rather than presented as reliable comparisons.

## Architecture and provenance

Vendor the complete small upstream package from `ToffelsKater/VRC-Texture-Painter` commit `29bbf3c728d55091190413066e549b0bbbb21bf6` (1.2.0), retaining its GPU algorithm and UI. Painting lives under `Editor/Core`, presentation under `Editor/UI`; Studio comparison and file safety are separate modules. Rename package, assemblies, C# namespace, shader names, preferences and recovery directory. Preserve upstream `.meta` files and stamp GUIDs. Because retained GUIDs conflict, upstream and Studio must not be installed together. Preserve the upstream test harness in an isolated development project; do not bring in its VRChat bootstrapper, website, scenes, or unrelated dependencies/assets.

## Safety and lifecycle invariants

- Source textures, meshes and material assets remain unchanged by default. Preview uses restorable MaterialPropertyBlocks. No runtime scripts or avatar components are installed.
- Never silently overwrite a source or unrelated asset. Validate PNG destinations within Assets; preserve the old project/export if writing fails. Report errors visibly.
- Retain texture alpha and upstream sRGB/raw-value semantics. Masks are channel data, bypass color-layer compositing, and do not paint alpha. Comparison labels its color/data and difference semantics; it is diagnostic, not a perceptual metric.
- Opening malformed project data fails visibly with bounded counts/dimensions and releases partially loaded resources. Never discard the existing session to open an invalid project.
- File safety ceilings are 32 textures, 1024 layers, 8192 pixels per axis, 1 MiB per JSON field and 2 GiB decoded layer pixels. Reject unsupported sizes visibly; prevent adding layers that could not be saved. Preserve standard upstream version 1/2 compatibility within these bounds.
- Release GPU resources, command buffers, temporary textures and event subscriptions. Preserve a recovery file when autosave/restore fails. Closing with unsaved work prompts for save/discard/cancel.
- No private avatars, credentials, proprietary materials, services, publishing, runtime dependencies or standalone executable. MIT upstream notices ship inside the package.

## Acceptance and evidence

Implementation requires an installable package, retained painting/mask/layer/project/export features, Compare, provenance, usable documentation, verification report, Windows QA checklist, and a draft PR against the specified baseline. Inspect the complete diff for scope, asset integrity, licensing, private files, and weakened verification.

Run available file-level checks and focused safety regressions. With a usable Unity license, compile the actual package, run the preserved GPU suite and new integration checks, then manually exercise painting, projects, exports, comparison, reload/play/close recovery. Windows visual, shader, interaction and avatar QA remains **NOT TESTED** until performed there. Evidence must distinguish automatically tested, manually validated, inspected, inferred, simulated, not tested and blocked. Missing Unity/Windows environments permit **implemented but not accepted**, never an inferred PASS.

## Risks and exclusions

UV/readback orientation, reversed depth, shader model 4.5, float formats/blend operations, tablets and preview gamma need actual GPU testing on Direct3D and Metal. Upstream retains 8-bit layers, substantial VRAM usage and limited UV tile coverage. External shader mappings beyond supported UV modes remain unsupported; normal-map editing is not a normal-vector authoring workflow.

Shot 1 excludes a new paint engine, UV editing/remapping, Face Studio, advanced recoloring, SeamDoctor/MipDoctor, avatar-to-avatar transfer, AI textures, VRChat API/publishing, monetization, accounts/cloud, release publication and merging.
