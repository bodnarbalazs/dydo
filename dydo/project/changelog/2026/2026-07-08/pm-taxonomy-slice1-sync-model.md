---
area: general
type: changelog
date: 2026-07-08
needs-human: false
---

# Task: pm-taxonomy-slice1-sync-model

Review slice-1 commit 525d96e (diff it against parent 4b411b5). DR-034 PM-Record-Taxonomy, gating slice — sync-model object types only. Files in scope:
- Templates/sync-model.template.json
- DynaDocs.Tests/Sync/Model/SyncModelLoaderTests.cs
- DynaDocs.Tests/Sync/Notion/NotionProvisionerTests.cs

What it does:
1. Adds Task type (dir project/tasks): status backlog->in-progress->in-review->done; folders map { backlog: backlog } (routes status:backlog into tasks/backlog/; done left UNMAPPED on purpose — the date-nested changelog archive is placed by TaskApproveHandler, not RepoFolderLayout, per DR-034 §4). Standard priority/work-type/needs-human/last-activity/done/stale/attention family; every formula inlined (no formula-reads-formula — guarded by DefaultModel_NoFormulaReferencesAnotherFormula).
2. Adds FutureFeature type (dir project/future-features): status raw->shaping->promoted->dropped, area select; no subfolder partition (low volume).
3. Drops the "dydo " prefix from all 5 notionTitle values, plain titles for the 2 new types (provisioner matches DBs by stored id, never title — no orphan/duplicate).
4. Issue #215: flips both Issue views (Open, Needs Attention) severity sort descending->ascending (Notion sorts a select by option POSITION, options are critical-first, so ascending puts critical/high on top).

Key decisions to scrutinize:
- Task folders map: only backlog mapped, done deliberately unmapped (root convention: unmapped => never moved). Verify against DR-034 §4.
- Kept SprintTask `ready` status (declined the optional prune — no data uses it, 3 tests assert it; cosmetic-only, out of scope for the gating slice).
- Task/FutureFeature append to model file order; topo sort places them last: [Release, Campaign, Sprint, SprintTask, Issue, Task, FutureFeature] — asserted in the two updated list tests.

Verification already done: gap_check --force-run = exit 0, 163/163 modules, 4356/4356 tests green (includes new Load_DefaultModel_CarriesTaskAndFutureFeatureTypes test).

OUT OF SCOPE — do NOT flag as missing: the generated dydo/_system/sync-model.json (agent-off-limits, seeded only when missing) and the live Notion board stay unchanged. That is create-only by design and tracked for the live-provision slice (slice 6), per the brief caveat. Also the file-move slices (2-6) are intentionally NOT in this slice.

Report your verdict to origin (Brian).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review slice-1 commit 525d96e (diff it against parent 4b411b5). DR-034 PM-Record-Taxonomy, gating slice — sync-model object types only. Files in scope:
- Templates/sync-model.template.json
- DynaDocs.Tests/Sync/Model/SyncModelLoaderTests.cs
- DynaDocs.Tests/Sync/Notion/NotionProvisionerTests.cs

What it does:
1. Adds Task type (dir project/tasks): status backlog->in-progress->in-review->done; folders map { backlog: backlog } (routes status:backlog into tasks/backlog/; done left UNMAPPED on purpose — the date-nested changelog archive is placed by TaskApproveHandler, not RepoFolderLayout, per DR-034 §4). Standard priority/work-type/needs-human/last-activity/done/stale/attention family; every formula inlined (no formula-reads-formula — guarded by DefaultModel_NoFormulaReferencesAnotherFormula).
2. Adds FutureFeature type (dir project/future-features): status raw->shaping->promoted->dropped, area select; no subfolder partition (low volume).
3. Drops the "dydo " prefix from all 5 notionTitle values, plain titles for the 2 new types (provisioner matches DBs by stored id, never title — no orphan/duplicate).
4. Issue #215: flips both Issue views (Open, Needs Attention) severity sort descending->ascending (Notion sorts a select by option POSITION, options are critical-first, so ascending puts critical/high on top).

Key decisions to scrutinize:
- Task folders map: only backlog mapped, done deliberately unmapped (root convention: unmapped => never moved). Verify against DR-034 §4.
- Kept SprintTask `ready` status (declined the optional prune — no data uses it, 3 tests assert it; cosmetic-only, out of scope for the gating slice).
- Task/FutureFeature append to model file order; topo sort places them last: [Release, Campaign, Sprint, SprintTask, Issue, Task, FutureFeature] — asserted in the two updated list tests.

Verification already done: gap_check --force-run = exit 0, 163/163 modules, 4356/4356 tests green (includes new Load_DefaultModel_CarriesTaskAndFutureFeatureTypes test).

OUT OF SCOPE — do NOT flag as missing: the generated dydo/_system/sync-model.json (agent-off-limits, seeded only when missing) and the live Notion board stay unchanged. That is create-only by design and tracked for the live-provision slice (slice 6), per the brief caveat. Also the file-move slices (2-6) are intentionally NOT in this slice.

Report your verdict to origin (Brian).

## Code Review

- Reviewed by: Dexter
- Date: 2026-07-07 11:40
- Result: PASSED
- Notes: PASS. Slice-1 sync-model types verified against DR-034 §4/§5: Task (backlog->in-progress->in-review->done; folders {backlog:backlog}; done correctly left unmapped for handler-placed changelog archive) and FutureFeature (raw->shaping->promoted->dropped; area select; no partition) match the DR exactly. All formulas inlined (no formula-reads-formula) — done/stale/attention reference only status/needs-human/last-activity; attention inlines the stale body; now covered model-wide by DefaultModel_NoFormulaReferencesAnotherFormula. #215 sort fix correct: severity options are critical-first (pos 0), ascending surfaces critical/high on top. notionTitle prefix drop safe (provisioner matches by stored id). Tests meaningful (new Load_DefaultModel_CarriesTaskAndFutureFeatureTypes asserts routing incl. done-not-mapped; provisioner+order tests updated). gap_check --force-run = exit 0, 4356/4356 tests, 163/163 modules. NOTE (pre-existing, out of scope): dydo check reports 76 orphan-doc errors on resolved issues — not from this slice (touches no project/ docs), present on parent.

Awaiting human approval.

## Approval

- Approved: 2026-07-08 10:15
