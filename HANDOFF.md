# Creative Studio handoff

Shot 2 is implemented on `codex/shot-2-creative-studio`, stacked from exact Shot 1 commit `9b1d2f0e43883079e5a3596a49832cbd0668b77e`. Its draft PR must target `codex/shot-1-painting-foundation`. Both shots remain **implemented but not accepted**. Shot 1/main are unchanged; do not merge, tag or release. Use Git/PR for the delivery head. Follow the single SPEC.md and PLAN.md.

UV Explorer, Colorway and Face share target/navigation/regions. Face borrows the existing color brushes/layers; TextureLab accepts owned snapshots. Custom shader correspondence is unverified; shared UV texels cannot isolate surfaces. Source assets and original upstream algorithms/metas remain unchanged. Remove VRC Texture Painter before installation because retained GUIDs conflict.

Self-contained `.atsstudio` / ATSCRV01 version 1 stores baseline RGBA8, binary masks, editable Oklab adjustments, Face flags and binding/navigation, with checked payload length/bounds. MTPAINT1/2 stays unchanged. Painting/companion saving is two separate transactions; failure keeps creative dirty. Library recovery retains path/dirty state; restart recovery is manual. See architecture/README for workflows and limits.

Executed CPU/file regressions pass, including UV correspondence before masks and a failing-then-passing truncated gzip-footer regression. Native adapter/capture/recovery checks are authored but unexecuted. Final review, package and delivery evidence belongs in docs/VERIFICATION.md and Git/GitHub.

Next gate: licensed Unity 2022.3.22f1 / Built-in / Linear / Windows 11 DX11 in **Tests/UnityProject only**. Run both suites and critical Shot 1/2 flows in docs/WINDOWS_QA.md. Unity API/shader compilation, GPU/readback, Scene/IMGUI, skinning, Play/reload/close, actual Mono/Windows replacement, avatar QA and Metal remain NOT TESTED. No owner avatar project was touched.

For Shot 3, first resolve observed acceptance failures and attach real logs/screenshots. Preserve provenance, sidecar editability, asset safety and explicit unsupported-mapping rules. No Shot 3 implementation has started.
