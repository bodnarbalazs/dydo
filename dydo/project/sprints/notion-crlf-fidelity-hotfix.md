---
title: Notion CRLF Fidelity Hotfix
status: done
area: backend
type: context
---

# Notion CRLF Fidelity Hotfix

This completed one-slice hotfix fixed the checkout-dependent PM-spine body regression without weakening DR 043's byte-preservation guarantee. It delivered source-span ownership, hermetic full/delta fixtures, release-gate protection, and live proof as one correctness boundary.

## Prior art

Prior art searched: [DR 043](../decisions/043-dual-projection-format-preserving-notion-body-sync.md), [Slice 6](../slices/notion-body-fidelity-6-fidelity-and-live-proof.md), `Sync/Projection/MarkdownPatchPlanner.cs`, `Sync/SyncDocFile.cs`, and dual-projection full/delta tests. Rejected alternatives: fixture pinning, global replacement, and external-LF-as-canonical. `MarkdownPatchPlanner` owns mapped replacements; `SyncDocFile.PatchExisting` owns only the surrounding file.

## Objective

An external one-span body edit must preserve every untouched canonical byte on both LF and CRLF clean checkouts. Fixture setup must recognize frontmatter independently of the checkout newline convention, and the release test workflow must exercise that invariant before packaging.

## Design — byte ownership

Outside a mapped replacement span, canonical bytes are byte-identical. Local separators and terminal newlines stay local; external Markdown owns only replacement or inserted bytes. A deletion removes only its mapped node bytes and cannot consume or synthesize a neighboring separator/terminator. `CleanForPersist` remains the external LF projection; it is not a canonical newline conversion. `MarkdownPatchPlanner` must carry source-span ownership explicitly; `SyncDocFile` must not globally remap line endings.

Tests programmatically materialize canonical LF and explicit CRLF variants regardless of checkout; clean checkout is outer proof. Literal update: `before\nSPAN\nafter\n` → `before\nREMOTE\nafter\n`; CRLF: `before\r\nSPAN\r\nafter\r\n` → `before\r\nREMOTE\r\nafter\r\n`. Insertions are `INSERT\n\nbefore\nspan\nafter\n`, `before\nspan\n\nINSERT\n\nafter\n`, and `before\nspan\nafter\n\nINSERT\n`. Middle deletion `before\n\nDELETE\n\nafter\n` and terminal deletion `before\r\n\r\nDELETE\r\n` retain local neighboring/terminal bytes.

## Scope and owned files

- Trace `MarkdownPatchPlanner`, `SyncDocFile.Read`, and `PatchExisting` ownership boundaries.
- Preserve untouched LF/CRLF bytes through real projected full and delta imports without a fixture-only pin or broad newline rewrite.
- Make the two Slice-11 setup paths newline-agnostic and hermetic under `core.autocrlf`.
- Add focused clean-checkout regressions for LF and CRLF one-span imports plus terminal/deletion boundaries.
- Make release protection mandatory: Ubuntu/LF and Windows/CRLF jobs, each a build dependency of every publish path.

Owned implementation: `Sync/Projection/MarkdownPatchPlanner.cs`, `Sync/SyncDocFile.cs`, the two dual-projection Slice-11 fixture tests, focused projection tests, and the release workflow/gate only as required for the mandatory matrix.

## Acceptance

1. Genuine clean `core.autocrlf=false` and `true` checkouts pre-assert fixture bytes, then pass the three reproduced failures.
2. Full and delta one-import/no-remote-write/next-tick convergence and local-push lossy-echo quiet pass for LF and CRLF.
3. The exact update/insertion/deletion/start/end/terminal cases preserve local prefix/suffix/frontmatter bytes by the ownership rule.
4. One authorized live scratch-child CRLF external edit proves exact prefix/suffix/frontmatter and a quiet next delta.
5. Ubuntu/LF and Windows/CRLF release jobs gate every publish path through a build dependency.

## Completion evidence

Offline projection, full/delta, release-workflow, build, and final coverage gates passed. On 2026-08-18, the authorized isolated live suite ran its exact three body-fidelity facts successfully (3 passed, 0 skipped); scratch children under the configured dydo parent were archived after the run.

## Slice map, ordering, and isolation

| Slice | Work | Depends on |
| --- | --- | --- |
| 1 | Implement ownership seam, hermetic fixtures, release matrix, and LF/CRLF proofs. | Plan review |

Run serially in a clean worktree; do not touch the Anthropic plan/model lane or unrelated dirty files.

## Exact gates and release topology

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~NotionDualProjectionFullSyncTests|FullyQualifiedName~NotionDualProjectionDeltaTests|FullyQualifiedName~ProjectedMarkdownPatchTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dotnet build DynaDocs.sln --no-restore
dydo check
git diff --check
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "Category=notion-live&FullyQualifiedName~NotionSpineBodyFidelityLiveTests"
```

One `newline-fidelity` matrix job runs `ubuntu-latest`/`windows-latest`. After `actions/checkout`, it runs `git config core.autocrlf false|true` then `git checkout -- DynaDocs.Tests/Sync/Notion/Fixtures/slice-11-sanitized.md`; PowerShell asserts bytes with `[IO.File]::ReadAllBytes(...)` and `([Text.Encoding]::UTF8.GetString($bytes)).Contains("`r`n")` before the focused filter. `build` needs `newline-fidelity`; `release` and `nuget` need `build`; `npm` needs `release`. Run `actionlint .github/workflows/release.yml` and add `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, filtered by `FullyQualifiedName~ReleaseWorkflowTests`, to assert those `needs` edges.

## Literal span matrix

Mapped spans are bracketed. LF start: `before\nspan\nafter\n` → `[INSERT\n\n]before\nspan\nafter\n`; CRLF start: `before\r\nspan\r\nafter\r\n` → `[INSERT\n\n]before\r\nspan\r\nafter\r\n`. LF middle: `before\nspan\n\nafter\n` → `before\nspan\n\n[INSERT\n\n]after\n`; CRLF middle: `before\r\nspan\r\n\r\nafter\r\n` → `before\r\nspan\r\n\r\n[INSERT\n\n]after\r\n`. LF end: `before\nspan\nafter\n` → `before\nspan\nafter\n\n[INSERT\n]`; CRLF end: `before\r\nspan\r\nafter\r\n` → `before\r\nspan\r\nafter\r\n\r\n[INSERT\n]`. LF middle deletion: `before\n\n[DELETE]\n\nafter\n` → `before\n\n\n\nafter\n` (both local adjacent separators survive); CRLF: `before\r\n\r\n[DELETE]\r\n\r\nafter\r\n` → `before\r\n\r\n\r\n\r\nafter\r\n`. LF terminal deletion: `before\n\n[DELETE]\n` → `before\n\n`; CRLF terminal deletion: `before\r\n\r\n[DELETE]\r\n` → `before\r\n\r\n`; the preceding local separator survives and deletion owns only bracketed bytes.

## Watch-outs and rollback

Do not pin the fixture, globally replace newlines, or treat external LF as canonical formatting. If the planned span cannot preserve byte ownership, shadow rather than widen a replacement. Rollback is the focused hotfix commit only; no board/reset/release mutation is authorized.

## Q&A

**Why not normalize after patching?** It would change bytes outside the external-owned span. **Why test two operating systems?** Git checkout rules differ, so one platform cannot prove the release artifact. **Why live CRLF?** Fake transport cannot prove native Markdown plus filesystem bytes together; it uses the authorized configured production dydo parent through one unique scratch child, best-effort archive cleanup, and no token/page-ID logging.
