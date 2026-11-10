# Shot 1 handoff

Shot 1 is **implemented but not accepted** on `codex/shot-1-painting-foundation`, based on `12bf05d397c46dbab7a12f438cf0878d217b52a9`. [Draft PR #1](https://github.com/Teyocesu/Avatar-Texture-Studio/pull/1) is open against main under Teyocesu. Follow SPEC.md and PLAN.md. Use Git/PR for the final head; do not merge or release before real acceptance.

Upstream is pinned to `29bbf3c728d55091190413066e549b0bbbb21bf6` (1.2.0); see docs/UPSTREAM.md and THIRD_PARTY_NOTICES.md. All original metas/GUIDs remain; do not co-install with upstream. Painting/masks retain the engine; Compare is independent in the same window. Export assignment defaults off; overwrite/assignment is explicit. Project files retain MTPAINT1/2 within documented bounds.

Available checks passed for file safety, C# 9 syntax, package/provenance/GUID/shader integrity and tarball contents. See docs/VERIFICATION.md for exact evidence and the inherited upstream-meta whitespace exception. The ignored local tarball is reproducible with `python3 tools/package.py`.

Next: use licensed Unity 2022.3.22f1 on Windows 11, open **Tests/UnityProject**, and execute **docs/WINDOWS_QA.md**. Real Unity compilation, GPU/Metal, lifecycle/undo/project/export interaction and Windows/avatar QA remain NOT TESTED. No owner avatar project was touched. Preserve those blocked gates; fix observed failures before accepting Shot 1 or starting broader features.
