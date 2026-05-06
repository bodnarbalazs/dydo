---
area: general
type: changelog
date: 2026-05-04
---

# Task: cleanup-docs-check-backlog

Review commits b92e1b3 and 756bedb for cleanup-docs-check-backlog.

Brief: `dydo/agents/Brian/brief-cleanup-docs-check-backlog.md`.

What changed:
- b92e1b3 — fixed schema errors across `dydo/project/`: invalid `area` enums in 6 decisions (cli/process → project), missing frontmatter + invalid `area` in 9 inquisitions, broken `Templates/*.template.md` links (dropped to inline code), `.cs` link-without-md-ext (dropped to inline code), broken `issues/0133` path (→ `issues/resolved/0133`), summary paragraphs for inquisitions and `_coverage.md`, link from `_inquisitions.md` and `agent-deaths.md` to `_coverage.md` so it's no longer orphaned.
- 756bedb — added 2-3 sentence summary paragraphs to issue stubs, derived from each file's title and (where present) Resolution section. Active issue 0148 plus all resolved issues from 0046 onward (the earlier resolved set 0001-0045 was committed inside Brian's wait-race batch b33a171).

Verify:
1. `dydo check` reports `Found 13 errors, 0 warnings`. The 13 errors are all known dydo-tool defects Brian acknowledged in his reply on this task:
   - 4x `_system/template-additions/extra-*.md` "Missing title" — `SummaryRule.cs` is missing the `_system/template-additions/` exclusion the other rules already have. Brian directed: don't fix in this commit; folded into a parallel dydo-tool fix batch.
   - 9x `dydo/project/inquisitions/*.md` "Invalid type 'inquisition'" — the type enum doesn't include `inquisition`. Brian directed: don't force-fit; same parallel dydo-tool fix batch will extend the enum.
2. Summaries are accurate to the doc body — not invented. Bulk-pattern: `<title-paraphrase>. <resolution-paraphrase>.` per Brian's explicit go-ahead on bulk thin summaries.
3. Enum fixes (cli/process/services/process-lifecycle → project) are within the valid set.
4. Frontmatter additions on the 5 inquisitions that lacked it use `area: project` consistently.
5. `dotnet build` clean (verified). `gap_check` skipped tests (no source/test changes since last run); 137/137 modules cached green.

Approve or reject.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commits b92e1b3 and 756bedb for cleanup-docs-check-backlog.

Brief: `dydo/agents/Brian/brief-cleanup-docs-check-backlog.md`.

What changed:
- b92e1b3 — fixed schema errors across `dydo/project/`: invalid `area` enums in 6 decisions (cli/process → project), missing frontmatter + invalid `area` in 9 inquisitions, broken `Templates/*.template.md` links (dropped to inline code), `.cs` link-without-md-ext (dropped to inline code), broken `issues/0133` path (→ `issues/resolved/0133`), summary paragraphs for inquisitions and `_coverage.md`, link from `_inquisitions.md` and `agent-deaths.md` to `_coverage.md` so it's no longer orphaned.
- 756bedb — added 2-3 sentence summary paragraphs to issue stubs, derived from each file's title and (where present) Resolution section. Active issue 0148 plus all resolved issues from 0046 onward (the earlier resolved set 0001-0045 was committed inside Brian's wait-race batch b33a171).

Verify:
1. `dydo check` reports `Found 13 errors, 0 warnings`. The 13 errors are all known dydo-tool defects Brian acknowledged in his reply on this task:
   - 4x `_system/template-additions/extra-*.md` "Missing title" — `SummaryRule.cs` is missing the `_system/template-additions/` exclusion the other rules already have. Brian directed: don't fix in this commit; folded into a parallel dydo-tool fix batch.
   - 9x `dydo/project/inquisitions/*.md` "Invalid type 'inquisition'" — the type enum doesn't include `inquisition`. Brian directed: don't force-fit; same parallel dydo-tool fix batch will extend the enum.
2. Summaries are accurate to the doc body — not invented. Bulk-pattern: `<title-paraphrase>. <resolution-paraphrase>.` per Brian's explicit go-ahead on bulk thin summaries.
3. Enum fixes (cli/process/services/process-lifecycle → project) are within the valid set.
4. Frontmatter additions on the 5 inquisitions that lacked it use `area: project` consistently.
5. `dotnet build` clean (verified). `gap_check` skipped tests (no source/test changes since last run); 137/137 modules cached green.

Approve or reject.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-01 16:59
- Result: PASSED
- Notes: PASS. 13 errors / 0 warnings on dydo check; all 13 are pre-acknowledged dydo-tool defects (4x SummaryRule needs _system/template-additions/ exclusion; 9x type enum needs 'inquisition' added) deferred per Brian. Summaries spot-checked across decisions 014-022, all 9 inquisitions, issues 0028/0046-0054/0099/0148 — faithful to title + Resolution body, no invention. Enum fixes (cli/process/services/process-lifecycle -> project) within valid set. 5 frontmatter additions all use area: project consistently. No source/test changes. gap_check: 4017/4017 passed, 137/137 modules at tier.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:51

## Resolution

Superseded by the dydo-check-drift batch on master (2026-05-04 to 2026-05-06). The SummaryRule template-additions exclusion landed in PR1 (`8b71cd4`) and the project/tasks skip in PR1 (`d05f696`); the `inquisition` type enum was added in PR2 (`3213931`); the `--summary` flag + placeholder detection that closes the SummaryRule warning floor on issue files landed in PR3 (`c85947a`), with reference-doc sync committed alongside this Resolution. Earlier prep work referenced in the entry above: `fc83e31`. Each layered fix discharges one of the deferred dydo-tool defects called out under "Verify".
