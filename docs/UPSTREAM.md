# Upstream integration audit

Inspected 2026-11-09 before implementation. Source: [VRC Texture Painter](https://github.com/ToffelsKater/VRC-Texture-Painter/tree/29bbf3c728d55091190413066e549b0bbbb21bf6), exact commit `29bbf3c728d55091190413066e549b0bbbb21bf6`, package version 1.2.0. Target repository baseline `12bf05d397c46dbab7a12f438cf0878d217b52a9` contained only README, MIT LICENSE and .gitignore.

## Reuse decision

Vendor `Packages/com.vrctexturepainter.tool` as `Packages/com.teyocesu.avatar-texture-studio`. Its approximately 568 KB package contains no external DLLs or declared package dependencies. Retain all painting source, shaders, six stamps, docs/history and metas. Keep `Assets/MeshTexturePainterTests` in an isolated development project, with assembly/namespace references adapted only. Exclude upstream's website, VRChat bootstrapper, project assets and unrelated Unity dependencies. Do not pull upstream Git history into this repository.

Package LICENSE.md grants MIT redistribution and names **Copyright (c) 2026 ToffelsKater**. No separate restrictive notices or third-party dependencies were found in the imported package/test paths. The six stamp PNGs were added as package assets in upstream commit `d871a3d91b58087cc02415ed6886ee52485bd4bb`; they fall under the package MIT license, with no separate license files or metadata restrictions found. This is repository-license evidence, not independent authorship verification.

## Architecture inspected

- `BrushEngine`: GPU dabs; texture-space reverse projection; view-space capture for blur/blend; occlusion, normals, mirror, gradient and tips.
- `PaintTarget` / `TriangleBVH`: posed/skinned world mesh baking, CPU cursor raycast, UV ownership/wrap/padding, restorable per-material property blocks. No added avatar components.
- `PaintDocument` / `PaintLayer`: raw RGBA8 layers, sRGB presentation, special channel-data mask path. `PaintHistory` owns GPU snapshots and a Unity undo proxy, including multi-texture compound operations.
- `PaintSession`: activation owns SceneView/update subscriptions, preview and tools; each color/mask tab retains its session. Open/save/export and optional shared-material assignment currently live here.
- `PaintProjectIO`: MTPAINT1/2 binary files with JSON metadata and PNG/deflated raw layers; importer copying and PNG output. Upstream save deleted the destination before moving the temporary file; this needs a failure-safe replacement. Parser counts and lengths need validation.
- `MeshTexturePainterWindow`: proven IMGUI setup, brushes/layers/masks/output, two session tabs, Library autosave on reload/play. Assignment defaulted on; window close discarded recovery keys; static shader materials/command buffer lacked an explicit release path. These are focused integration corrections, not an engine rewrite.
- `MTPTests`: executable editor/GPU harness; primitive generated meshes and textures, no proprietary avatar fixtures. Covers projection/orientation, seams/padding, blur/blend, occlusion, composite modes, files, scaled skinned meshes, preview restoration, wrapping, UV channels, multiple textures, masks, gradients and stamps/custom brushes. No tests will be skipped or removed.

## Identity and conflicts

New UPM/assembly/namespace/shader/preferences/recovery identities prevent name lookup and session-state collisions. Keep upstream asset GUIDs and all six brush-tip references unchanged. Consequently **co-installation with VRC Texture Painter is unsupported**: duplicate GUIDs are an import-time conflict. Remove the original package in an isolated project first; retain `.mtpaint` files. There is no reliable runtime fix for an import-time GUID collision.

## Platform risks

Upstream explicitly targets 2022.3.22f1, DX11, Built-in, Linear. It has no native DLL or OS process dependency. GPU shaders require target 4.5, Max blending, floating-point render targets and correct texture/readback orientation. Upstream calibrates UV flip and full/partial readback independently; preserve those algorithms. Direct3D and Metal share some coordinate/depth conventions, but this does not establish identical output. Tablet pressure, editor shortcuts, file naming and reload/Play transitions also need target-machine testing.

Unity 2022.3 sources checked for the integration: [platform differences](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-PlatformDifferences.html), [preview drawing with a material](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorGUI.DrawPreviewTexture.html), [unsaved-window prompts](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow-hasUnsavedChanges.html), [SaveChanges failure/cancel behavior](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorWindow.SaveChanges.html). These document API contracts, not execution evidence.

Also checked [local UPM paths relative to Packages](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-localpath.html), [virtual-only texture imports](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-vtOnly.html), and [native GPU capability queries](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SystemInfo.html). The isolated harness declares Linear rather than copying upstream's unrelated project configuration.
