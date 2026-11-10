# Quality Studio handoff

Shot 3 is implemented on `codex/shot-3-quality-studio`, exact base `a5c90a471a8af9c75ead8cb366c891f6ef89738a`. [Draft PR #3](https://github.com/Teyocesu/Avatar-Texture-Studio/pull/3) targets `codex/shot-2-creative-studio` under Teyocesu. Earlier branches/main remain unchanged. No merge/tag/release. Planned implementation is complete; Shots 1/2/3 are **NOT ACCEPTED**. Follow the single SPEC.md and PLAN.md; next phase is actual native acceptance, not more assumed feature work.

SeamDoctor conservatively accepts unique opposite geometric edges only within the same indexed connected component/material/submesh. Disconnected UV islands/coincident pieces stay uncertain and unmeasured. Metrics separate visible Oklab, alpha, composite/raw contrast and exterior-ring potential padding risk. A deliberate transition is not a defect verdict.

MipDoctor generates recursive CPU area-box RGBA8 levels with correct color/data/alpha and NPOT semantics. These are not actual Unity mips/compression. Shared UV/Scene inspection, frozen provenance, quality summary, TextureLab and explicit new PNG/report export are implemented. Diagnostic results are ephemeral; editable MTPAINT1/2/ATSCRV01 formats remain unchanged.

Executed CPU diagnostics and all prior filesystem/creative checks pass, including a failing-then-passing sparse-alpha quantization regression. Structural/meta/provenance checks pass. Native quality/session/source-preservation checks are authored but unexecuted. Product source review also fixed inherited TextureLab caller-lifetime coupling by cloning owned snapshots; that regression needs Unity execution. Do not turn source inspection into a native PASS.

Source/product review, docs/evidence/consolidated Windows QA and all available checks are complete. The 0.3.0 local package and exact bytes were verified; see docs/VERIFICATION.md for artifact SHA and evidence limits. Use Git/PR for final head. Original MIT notices/GUIDs remain; remove upstream VRC Texture Painter before installation.

Release blockers remain licensed Unity 2022.3.22f1 / Built-in / Linear / Windows 11 DX11 compilation, both suites and physical QA in Tests/UnityProject; GPU/shader/readback/mips, Scene/IMGUI, skinning, reload/Play/close recovery, actual Mono replacement, Poiyomi/lilToon and real-avatar-copy visuals, plus separate Metal QA. Never open the owner's original avatar project. No planned phase beyond Shot 3 is started or authorized by a diagnostic result.
