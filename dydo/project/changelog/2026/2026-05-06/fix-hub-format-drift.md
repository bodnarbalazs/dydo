---
area: general
type: changelog
date: 2026-05-06
---

# Task: fix-hub-format-drift

Review commit 844579f for fix-hub-format-drift (#0166).

Scope: Services/HubGenerator.cs + DynaDocs.Tests/Services/HubGeneratorTests.cs only. Two-file commit (+143 / -7).

What changed: HubGenerator.GenerateDocumentLinks now branches on whether relativeFolderPath is under project/changelog/. For changelog folders it renders bare links via KebabToTitleCase(filename) and omits the summary trail. For everything else it keeps the existing doc.Title (with KebabToTitleCase fallback) + first-sentence summary behavior. New IsChangelogFolder helper sits next to the other path helpers in the same file.

Why: HubGenerator was using doc.Title for every entry. Changelog template H1s are `# Task: <kebab-name>`, so doc.Title produced verbose `Task: foo-bar-baz` labels with redundant brief summaries — drifted from the committed shape on ~18 changelog _index.md files; `dydo fix` regenerated all of them on every run. Hybrid rule scoped narrowly to project/changelog/. Content/concept hubs (guides, understand, reference, decisions, issues) are unchanged because they already match doc.Title shape and benefit from curated H1s.

Tests: 6 unit tests cover both branches with concrete fixtures (changelog with doc.Title set + summary present → bare KebabToTitleCase link, no summary; non-changelog with doc.Title null → KebabToTitleCase fallback; non-changelog with summary → first-sentence trail; backslash path normalization on the changelog branch). Full suite 4080/4080 green; gap_check.py 139/139 pass.

Notes for review:
  - Plan deviation from my initial proposal to Adele: bundled the summary-suppression rule into the same commit as the label rule. Both target the same hub class (changelog) and both are required to make `dydo fix` a true no-op once shipped. Surfaced in my completion message to Adele; she may ask to split.
  - Cannot run `dydo fix` as a no-op verification because the installed dotnet tool is the older 1.4.5 build (my fix is in source only, not yet packed/installed). Verification leaned on unit tests + visual diff comparison against the committed shape. Real no-op confirms once the next release ships.
  - Pre-existing unrelated working-tree noise (PR1-in-flight modifications to Commands/, Models/, DocScanner, etc.) was deliberately not staged. The commit is exactly two files.
  - Background context per Adele: PR1 of dydo-check-drift is review-passed / merge-ready; PR2 will run `dydo fix` as part of its BC verification probe. This commit must land before PR2 runs that probe to avoid the 18-file changelog noise sweeping into PR2's diff.

Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 844579f for fix-hub-format-drift (#0166).

Scope: Services/HubGenerator.cs + DynaDocs.Tests/Services/HubGeneratorTests.cs only. Two-file commit (+143 / -7).

What changed: HubGenerator.GenerateDocumentLinks now branches on whether relativeFolderPath is under project/changelog/. For changelog folders it renders bare links via KebabToTitleCase(filename) and omits the summary trail. For everything else it keeps the existing doc.Title (with KebabToTitleCase fallback) + first-sentence summary behavior. New IsChangelogFolder helper sits next to the other path helpers in the same file.

Why: HubGenerator was using doc.Title for every entry. Changelog template H1s are `# Task: <kebab-name>`, so doc.Title produced verbose `Task: foo-bar-baz` labels with redundant brief summaries — drifted from the committed shape on ~18 changelog _index.md files; `dydo fix` regenerated all of them on every run. Hybrid rule scoped narrowly to project/changelog/. Content/concept hubs (guides, understand, reference, decisions, issues) are unchanged because they already match doc.Title shape and benefit from curated H1s.

Tests: 6 unit tests cover both branches with concrete fixtures (changelog with doc.Title set + summary present → bare KebabToTitleCase link, no summary; non-changelog with doc.Title null → KebabToTitleCase fallback; non-changelog with summary → first-sentence trail; backslash path normalization on the changelog branch). Full suite 4080/4080 green; gap_check.py 139/139 pass.

Notes for review:
  - Plan deviation from my initial proposal to Adele: bundled the summary-suppression rule into the same commit as the label rule. Both target the same hub class (changelog) and both are required to make `dydo fix` a true no-op once shipped. Surfaced in my completion message to Adele; she may ask to split.
  - Cannot run `dydo fix` as a no-op verification because the installed dotnet tool is the older 1.4.5 build (my fix is in source only, not yet packed/installed). Verification leaned on unit tests + visual diff comparison against the committed shape. Real no-op confirms once the next release ships.
  - Pre-existing unrelated working-tree noise (PR1-in-flight modifications to Commands/, Models/, DocScanner, etc.) was deliberately not staged. The commit is exactly two files.
  - Background context per Adele: PR1 of dydo-check-drift is review-passed / merge-ready; PR2 will run `dydo fix` as part of its BC verification probe. This commit must land before PR2 runs that probe to avoid the 18-file changelog noise sweeping into PR2's diff.

Approve or reject.

## Code Review

- Reviewed by: Frank
- Date: 2026-05-05 19:10
- Result: PASSED
- Notes: Hub fix code, tests, and visual readback all clean. Full-suite gate failures are issue #0167 parallelism flakes (different test each run, both static-state races) — confirmed unrelated by balazs. Mergeable.

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
