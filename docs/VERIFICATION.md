# Shot 1 verification — 2026-11-09

**Implemented but not accepted.** Scope: `codex/shot-1-painting-foundation` relative to baseline `12bf05d397c46dbab7a12f438cf0878d217b52a9`. Final delivery commits are recorded by Git and the draft PR. This report does not assert successful Unity compilation or rendering.

## Environment and execution

Development host: macOS, Apple Silicon (`arm64`). No Unity editor/Hub was found through PATH, Spotlight or the standard `/Applications/Unity/Hub/Editor` install location; standard Unity license directories were also absent. A usable **Unity 2022.3.22f1 + license** pair was unavailable. No activation or owner avatar project was attempted. Physical Windows 11 testing was unavailable.

A temporary official .NET **8.0.427** SDK was extracted outside the repository, with its download verified against Microsoft's release-metadata SHA-512. No SDK/compiler/native binary is redistributed. Tests compile the actual pure-.NET SafeFileIO source and parse Unity source syntax; they do not substitute for Unity assemblies, compilation or Mono behavior.

## Evidence classification

| Classification | Evidence and practical limit |
| --- | --- |
| **Automatically tested — executed** | `dotnet run --project Tests/FileSafety/FileSafety.csproj -- <repository>` exited 0 after the final code changes. Real filesystem checks cover refused overwrite, preservation of prior bytes on partial writer failure, cleanup, approved replacement/new creation, Assets containment, traversal/sibling-prefix/extension/missing-folder rejection, and symlink directory/file rejection with source preservation. Writer failure is deliberately injected; disk-full/device-loss/power-loss behavior was not physically tested. |
| **Automatically tested — executed** | SDK-bundled Roslyn parsed all **17 package/Unity-harness C# sources** using C# 9, with no syntax errors. The console project compiled and ran SafeFileIO on .NET 8/macOS. **No semantic compile against Unity APIs or shader compiler ran.** |
| **Automatically tested — executed** | `python3 tools/verify_repository.py --archive artifacts/com.teyocesu.avatar-texture-studio-0.1.0.tgz` exited 0. All **73 imported files** exist; **60** are byte-preserved or match declared identity-only transformations. Every original `.meta` is byte-identical. **51 unique asset GUIDs**, all six default-tip references, all five branded shader references, Editor-only assemblies, the local harness dependency, licensing, unchanged GPU shader arithmetic/includes, and original test preservation were checked. The remaining imported files are deliberately adapted integration/docs/manifest files reviewed separately. |
| **Automatically tested — executed** | `python3 tools/package.py` built a local UPM tarball; its member set and bytes match exactly the installable package. It contains no test project, compiler, owner assets or credentials. Python tooling syntax parsed successfully. |
| **Automatically tested — executed with disclosed exception** | `git diff --check -- . ':!*.meta'` is clean. The unfiltered check reports **96 trailing-whitespace lines in 32 original upstream meta files**, all proven byte-identical to upstream. New metadata was cleaned. No Git check configuration or suppression was added to conceal those inherited blank YAML values. |
| **Inspected** | Both repositories and canonical source paths were audited before modification. Full baseline diff, package contents and normalized upstream adaptation diff were reviewed for scope, lifecycle, resource ownership, asset safety, compatibility, licensing, dependencies and private files. Root LICENSE is unchanged; upstream MIT copyright/permission notice and six stamps are retained. No avatar models/materials/scenes, native DLLs or external package dependencies were added. |
| **Inspected** | Existing upstream assertions/test bodies are unchanged after namespace adaptation and the single explicit `allowOverwrite: true` argument for its intentional overwrite test. No tests were deleted/skipped, assertions removed, expected values weakened, thresholds lowered, checks disabled, empty catches or unfinished stubs introduced. Cleanup catches rethrow; settings/recovery failures are surfaced. |
| **Manually validated** | No Unity UI, physical GPU, Windows interaction or avatar workflow was manually validated. Repository/source inspection is recorded as inspected, not manual product QA. |
| **Inferred** | Retaining the upstream renderer/brush/layer/history/shader algorithms is intended to preserve their behavior. This is an integration rationale, **not a painting/rendering PASS**. |
| **Simulated** | No Unity/GPU stand-ins or emulated Windows product runs were used. The filesystem test deliberately injects a writer exception; no physical disk fault is claimed. |
| **NOT TESTED** | Actual Unity package import/assembly/API compilation, native shader compilation, GPU painting/comparison/readback, real `.mtpaint` round trips/exports in Unity, undo/layers/mask accuracy, Preview/Play/recompile/close recovery, input/tablets, Windows visuals and real avatar integration. macOS Metal is likewise NOT TESTED. Genuine upstream version 1 fixtures and proprietary shader projects were not available. |
| **Blocked** | Final acceptance requires the missing licensed Unity environment, a graphics device running the suites, and actual Windows 11 manual QA. See [WINDOWS_QA.md](WINDOWS_QA.md). |

## Implemented surfaces awaiting Unity evidence

The editor package, Tools menu, scene renderer/avatar-root selection, original painting controls and layer/history/mask operations are integrated. Project persistence keeps the MTPAINT1/2 representation, adds bounded validation and failure-safe replacement, commits an in-progress stroke before saving, retains recovery diagnostics, and explicitly releases shared/temporary GPU resources. Export defaults to a separate PNG without assignment, validates destinations, and requires overwrite/assignment approval. Compare is independent of the engine, retains background sessions, samples imported assets without CPU read access, and implements side-by-side/A/B/overlay/alpha/absolute-difference views with clear limits.

The preserved GPU harness is `AvatarTextureStudio.Painting.Tests.MTPTests.Run`. Added, **unexecuted** Unity regression checks are `AvatarTextureStudio.Tests.StudioTests.Run`: malformed dimensions/counts/encoding/deflate/string lengths; source overwrite refusal/default-export preservation; non-readable comparison input; direct renderer/root selection; Compare session/history/preview continuity; alpha-aware difference pixels. These tests earn their place by protecting altered boundaries and new rendering behavior, not by increasing a count.

## Package artifact

Local file: `artifacts/com.teyocesu.avatar-texture-studio-0.1.0.tgz` (ignored build output), **141502 bytes**.

SHA-256: `46b6e38969d000c1523ca2c9e36ed903b2bcc2e2f30b267139158c5064362287`.

This is a structurally verified UPM tarball, **not a Unity-tested installation**. Rebuild using `python3 tools/package.py`; no release/publication is required.

## Remaining risks and next gate

- Retained GUIDs deliberately prevent co-installation with VRC Texture Painter. Remove upstream in an isolated project first; retain `.mtpaint` files.
- Metal/Direct3D orientation, reversed depth, render/blend formats, gamma/alpha, import settings, tablet input, GPU memory/error paths and event/recovery behavior need real Unity execution. Native capability checks disable unsupported painting setups but do not prove correct rendering.
- .NET 8/macOS file replacement tests do not prove Unity 2022.3 Mono or Windows/filesystem behavior. Unsupported atomic replacement fails visibly rather than deleting the prior file.
- Bounded project files have documented ceilings: 32 textures, 1024 layers, 8192 pixels per axis, 1 MiB JSON fields and 2 GiB decoded pixels. Older files outside these limits are rejected explicitly. Undo snapshots are not serialized; restart recovery is manual from saved/Library files.
- Compare uses imported mip-0 sampling, not source-file pixels or a perceptual metric. Unequal dimensions disable overlay/difference; normal/virtual-only/HDR/unsupported formats explain their limitation. Custom shader heuristics and masks require target-project QA.

Run [Windows QA](WINDOWS_QA.md) in the isolated project, fix observed regressions, and attach actual logs/screenshots before acceptance. Recommended Shot 2: close these foundation gates first, then consider a small read-only UV/texture inspection view with pixel sampling. UV remapping and advanced seam/mipmap/recoloring features remain later work.
