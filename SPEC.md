# Avatar Texture Studio — Shot 1

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
