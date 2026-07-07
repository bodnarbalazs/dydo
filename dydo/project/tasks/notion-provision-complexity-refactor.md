---
area: general
name: notion-provision-complexity-refactor
status: human-reviewed
created: 2026-07-06T22:41:15.7880551Z
assigned: Frank
needs-human: false
---

# Task: notion-provision-complexity-refactor

Behavior-preserving extract-method refactor to green the whole-repo gap_check gate (issue #216). Two Tier-1 methods were CC 32 (CRAP floors at CC, so tests can't help — only lowering CC helps).

CHANGES (pure structural, ZERO behavior change):
- Sync/Notion/Provisioning/NotionProvisioner.cs: BuildConfig (CC 32) split into BuildColumns (column list) + ApplyGroupingAndDates (board group-by / timeline date config). BuildConfig is now a 3-line orchestrator. Max method CC in file now 16.
- Sync/Notion/NotionSpineSync.cs: Provision's post-pass block extracted into PreviewPostPass (dry-run) + RunPostPass (real). Provision keeps the resolve/mint loop; the big ordering-rationale comment stays put above the dispatch. Max method CC in file now 12.

LIVE-API-SENSITIVE: no provisioning semantics, ordering, idempotency, two-pass create/post-pass flow, or API call shapes changed — verbatim code moved into private helpers, same call sequence and output strings. Please scrutinize that the two-pass flow and child-first post-pass ordering are byte-identical.

TESTS: no test files touched (all helpers private; public surface unchanged) — deliberately left NotionProvisionerTests.cs alone (Brian's in-flight Slice-1 edit). Full suite green: 4356 passed / 0 failed. gap_check --force-run exits 0, all 163 modules pass tier requirements.

NOTE: this resolves issue #216 (status still 'open' — issues/ is outside my writable paths, needs an oversight role or docs-writer to close). Unblocks Brian's Slice 1 + Emma's readme fix held behind the gate.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Behavior-preserving extract-method refactor to green the whole-repo gap_check gate (issue #216). Two Tier-1 methods were CC 32 (CRAP floors at CC, so tests can't help — only lowering CC helps).

CHANGES (pure structural, ZERO behavior change):
- Sync/Notion/Provisioning/NotionProvisioner.cs: BuildConfig (CC 32) split into BuildColumns (column list) + ApplyGroupingAndDates (board group-by / timeline date config). BuildConfig is now a 3-line orchestrator. Max method CC in file now 16.
- Sync/Notion/NotionSpineSync.cs: Provision's post-pass block extracted into PreviewPostPass (dry-run) + RunPostPass (real). Provision keeps the resolve/mint loop; the big ordering-rationale comment stays put above the dispatch. Max method CC in file now 12.

LIVE-API-SENSITIVE: no provisioning semantics, ordering, idempotency, two-pass create/post-pass flow, or API call shapes changed — verbatim code moved into private helpers, same call sequence and output strings. Please scrutinize that the two-pass flow and child-first post-pass ordering are byte-identical.

TESTS: no test files touched (all helpers private; public surface unchanged) — deliberately left NotionProvisionerTests.cs alone (Brian's in-flight Slice-1 edit). Full suite green: 4356 passed / 0 failed. gap_check --force-run exits 0, all 163 modules pass tier requirements.

NOTE: this resolves issue #216 (status still 'open' — issues/ is outside my writable paths, needs an oversight role or docs-writer to close). Unblocks Brian's Slice 1 + Emma's readme fix held behind the gate.

## Code Review

- Reviewed by: Dexter
- Date: 2026-07-07 10:09
- Result: PASSED
- Notes: PASS. Byte-identical extract-method refactor in both files (NotionProvisioner.BuildConfig->BuildColumns+ApplyGroupingAndDates; NotionSpineSync post-pass->PreviewPostPass+RunPostPass). No behavior/ordering/idempotency/API-shape change: child-first post-pass order, self-relation->rollup->formula->view dependency chain, per-type MarkPostPassDone, and output strings all preserved; preview keeps created||PostPassPending predicate, run keeps PostPassPending-only. gap_check --force-run exit 0: 4356/4356 tests pass, 163/163 modules at tier (CC/CRAP now green -> resolves #216). Gate validated the full dirty working tree via copy_dirty_files.

Awaiting human approval.