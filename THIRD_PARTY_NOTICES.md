# Third-party notices

Avatar Texture Studio is MIT licensed. The root LICENSE retains the project's existing copyright notice. The distributed package includes that license as LICENSE and the unchanged upstream license as LICENSE.md.

## VRC Texture Painter

- Original repository: https://github.com/ToffelsKater/VRC-Texture-Painter
- Exact source commit: `29bbf3c728d55091190413066e549b0bbbb21bf6`
- Original package: `Packages/com.vrctexturepainter.tool`, version 1.2.0.
- License: MIT — **Copyright (c) 2026 ToffelsKater**.
- Derived components: painting Core, UI, GPU shaders/includes, six default stamp PNGs and their Unity metadata; upstream README/changelog; the separate MeshTexturePainterTests harness.
- Studio changes: package/assembly/namespace/shader/editor identity, focused target selection and UI integration, explicit export safety, bounded project decoding, save/recovery/resource cleanup, and the independent TextureLab Compare module. Upstream painting algorithms and existing tests are retained. The overwrite regression test explicitly opts in to the new overwrite permission; its assertions are unchanged.

The complete upstream permission and warranty notice is retained verbatim in `Packages/com.teyocesu.avatar-texture-studio/LICENSE.md`. Original asset metadata and stamp GUID relationships are preserved. A source-file hash inventory is recorded in `docs/upstream-files.json`; audit details and exclusions are in `docs/UPSTREAM.md`.

No upstream website media, avatar models/materials, VRChat bootstrapper, native binaries or third-party packages are redistributed. No separate restrictive notices were found in the imported paths. Unity APIs and built-in includes are referenced from the installed editor, not copied into this package. Development syntax checks use Roslyn bundled with a locally installed .NET SDK; the SDK and Roslyn are not shipped in the package or repository.
