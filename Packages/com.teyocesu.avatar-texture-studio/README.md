# Avatar Texture Studio — Shot 1

Editor-only painting, layers, per-channel shader masks, editable `.mtpaint` projects, safe PNG export and read-only TextureLab Compare. Target **Unity 2022.3.22f1 / Built-in / Linear / Windows 11 DX11**. **Implemented but not accepted:** real Unity compilation, GPU interaction and Windows QA are pending.

Install in an isolated empty 3D Built-in project first. Remove VRC Texture Painter: retained asset GUIDs prevent co-installation. Add this package's `package.json` through Package Manager > Add package from disk, or install its locally built `.tgz` through Add package from tarball.

Open **Tools > Avatar Texture Studio**. In Painting or Masks select a scene avatar/renderer, choose mesh/material slots, texture properties and UV channels, then paint in Scene view. Retained color tools include hard/soft/blur/blend/eraser/gradient/custom/stamp. Layers retain blending, opacity, visibility, history and existing operations. Masks use supported per-channel tools. Preview is temporary and adds no components.

Save/Save As and Open Project keep editable `.mtpaint` work. Reload/Play transitions recover through `Library/AvatarTextureStudio`; history is not serialized. Closing dirty work asks Save/Discard/Cancel. Reopen saved/recovery projects manually after restarting Unity. Errors remain visible. Projects support 32 textures, 1024 layers, 8192 pixels per axis and 2 GiB decoded pixels; GPU memory can limit work earlier.

Export PNG creates a flattened 8-bit image. Assignment defaults off. Existing-file replacement and shared-material assignment require explicit approval. Overwrite Source and Create Texture remain available with these checks. Export must use ordinary Assets paths, not links/junctions. Save projects separately to retain layers.

Replacement failures identify retained `.backup.tmp` and completed `.tmp` recovery files. Keep them and copy recoverable data to a new `.mtpaint` or `.png` filename. Successful replacement removes its backup. Unity Mono/Windows recovery remains unverified.

Compare supports imported A/B assets with side-by-side, A, B, opacity-controlled overlay, alpha and absolute RGBA difference. Scroll/drag share fit-relative zoom and normalized pan. Unequal dimensions disable overlay/difference. Read/Write may stay off. Normal-map, virtual-only, HDR/float and GPU-unsupported formats explain their limits. Use Raw data values for masks. Comparison never writes assets; returning to painting keeps sessions open.

Painting derives from **VRC Texture Painter 1.2.0**, commit `29bbf3c728d55091190413066e549b0bbbb21bf6`, © 2026 ToffelsKater, MIT. See LICENSE.md, THIRD_PARTY_NOTICES.md and the historical `Documentation~/Upstream-README.md` for brush details. Follow Studio's current installation/export rules where the old manual differs.

Full installation, workflow, verification and Windows QA: [Avatar Texture Studio](https://github.com/Teyocesu/Avatar-Texture-Studio).
