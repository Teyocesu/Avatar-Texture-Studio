# Avatar Texture Studio

- Read SPEC.md and the single PLAN.md before changes. Update HANDOFF.md only at meaningful boundaries.
- Target Unity 2022.3.22f1 / Built-in / Windows 11 DX11. No runtime/avatar components, private assets or new services.
- Painting is derived from the pinned MIT upstream in docs/UPSTREAM.md. Keep license notices, `.meta` GUIDs, project compatibility and existing test coverage. Do not install Studio beside upstream: retained GUIDs collide.
- Preserve default asset safety, explicit overwrite/assignment approval, alpha/data semantics, preview restoration and lifecycle recovery. Do not simplify away required upstream features.
- Prefer native Unity/.NET capabilities. Add tests only for distinct credible behavior or safety regressions. Never claim Unity/GPU/Windows acceptance from static inspection.
- Use the isolated Tests/UnityProject harness, never an owner's avatar project. Keep generated Library/Temp/logs and test assets out of Git.
- Only Teyocesu may author/commit/submit PRs; verify effective identity before committing or pushing. No AI attribution. Ask before external writes unless the current request already authorizes them. Do not merge, tag or publish releases for Shot 1.
