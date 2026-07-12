---
id: 217
area: general
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-07-07
resolved-date: 2026-07-12
---

# gap_check staleness auto-skip is blind to Sync/, Templates/, Program.cs — reuses stale coverage, can pass a red gate

gap_check.py's staleness detector only watches 6 source dirs; edits under Sync/, Templates/, or Program.cs are invisible, so a plain run skips the test re-run and re-scores stale coverage — risking a false green on the absolute whole-repo gate.

## Description

`gap_check.py` auto-skips the test+coverage run when it detects no source/test changes since the last coverage XML (a ~4-min saver). But its staleness scan (`_find_changed_files_since`) walks a hardcoded subset of source roots, so edits outside that subset are invisible — the run is skipped and the **stale** coverage XML is re-scored, producing a verdict for pre-edit code (a false green that masks a new regression, or a false red that hides a fix).

## Description

The staleness detector watches only:

```python
SOURCE_DIRS = ["Commands", "Services", "Models", "Rules", "Utils", "Serialization"]
```

plus all of `DynaDocs.Tests/`. Compared against the actual source roots (Commands, Sync, Services, Models, Rules, Utils, Serialization, Templates, Program.cs), the scan is **blind to `Sync/`, `Templates/`, and top-level `Program.cs`** — `Sync/` being the entire Notion sync + sync-model subsystem, large and actively developed.

Consequence: after editing e.g. `Sync/Notion/NotionSpineSync.cs`, a plain `python DynaDocs.Tests/coverage/gap_check.py` prints *"Skipping tests: … no source/test changes detected"*, skips the run, and computes its pass/fail from the previous XML. The whole-repo gate that the workflow treats as absolute (reviewer/code-writer skills: "a red gap_check = automatic review FAIL, no such thing as a pre-existing/unrelated failure") can therefore report **green over uncovered/over-complex/failing Sync code**.

The worktree runner `run_tests.py` *does* copy all dirty files, so `--force-run` yields the correct result — only the auto-skip heuristic is blind. But the documented happy path in the code-writer mode file is the bare `gap_check.py`, i.e. the blind one.

Observed live: during the #216 Sync/ complexity refactor, a plain `gap_check.py` reused an 11-min-old XML and reported the pre-refactor red; `--force-run` was required to obtain the true (green) verdict.

## Reproduction

1. With a fresh coverage XML present, edit any `.cs` under `Sync/` (or `Templates/`, or `Program.cs`).
2. Run `python DynaDocs.Tests/coverage/gap_check.py` (no flags).
3. Observe the "Skipping tests … no source/test changes detected" line and a verdict derived from the old XML rather than the edited code.

## Resolution

Make the staleness scan cover every source root rather than a hardcoded subset. Candidate fixes (pick one):

- Derive the scanned roots from `dydo.json` / role `writablePaths` (single source of truth) instead of the hardcoded `SOURCE_DIRS`.
- Add `Sync`, `Templates`, and top-level `Program.cs` to `SOURCE_DIRS`.
- Walk all tracked `*.cs` under the repo root (excluding `obj/` and generated files) for the mtime comparison.

Until fixed, always pass `--force-run` when a change touches anything outside the six currently-listed dirs.