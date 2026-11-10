# Shot 1 execution plan

Contract: [SPEC.md](SPEC.md). Baseline: `12bf05d397c46dbab7a12f438cf0878d217b52a9`. Branch: `codex/shot-1-painting-foundation`.

1. **Audit and pin — complete.** Inspect both repositories, license, package, shaders, persistence, UI and harness before implementation. Upstream pin: `29bbf3c728d55091190413066e549b0bbbb21bf6`. Unity/Hub absent; Unity compilation, GPU and Windows QA blocked. Record findings in `docs/UPSTREAM.md`.
2. **Package integration and safety — implemented.** Package/harness imported with original metas, isolated identities, retained workflows, explicit export permissions, bounded/failure-safe persistence and focused resource/recovery fixes. File-safety and provenance checks executed successfully; native Unity regressions are implemented but unexecuted.
3. **TextureLab Compare — implemented.** Independent comparison in the same window, with A/B/side-by-side/overlay/alpha/difference, shared navigation, metadata and explicit limits. File integrity and C# syntax checks executed; actual rendering/session interaction remains NOT TESTED.
4. **Verification, documentation and draft PR — complete.** Available checks and source review complete; see docs/VERIFICATION.md. Local UPM tarball built; implementation committed/pushed as Teyocesu. [Draft PR #1](https://github.com/Teyocesu/Avatar-Texture-Studio/pull/1) is open against main. No merge/release. Shot 1 is **implemented but not accepted**: Windows/Unity acceptance stays blocked on the documented missing environment. Continue with docs/WINDOWS_QA.md.
