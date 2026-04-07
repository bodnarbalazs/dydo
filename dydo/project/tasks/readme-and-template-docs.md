---
area: general
name: readme-and-template-docs
status: review-failed
created: 2026-04-07T19:17:38.2189513Z
assigned: Grace
updated: 2026-04-07T19:26:22.1127130Z
---

# Task: readme-and-template-docs

Documentation updates for review: (1) Added Tip section about gap_check.py and run_tests.py to dydo/reference/about-dynadocs.md. (2) Added --files, --gaps-only, --summary, --path, --since flags to dydo inquisition coverage in dydo/reference/dydo-commands.md and updated the command table in about-dynadocs.md. Note: the root README.md also needs the same Tip and command table updates but was outside docs-writer writable scope — the root README should be updated separately.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Documentation updates for review: (1) Added Tip section about gap_check.py and run_tests.py to dydo/reference/about-dynadocs.md. (2) Added --files, --gaps-only, --summary, --path, --since flags to dydo inquisition coverage in dydo/reference/dydo-commands.md and updated the command table in about-dynadocs.md. Note: the root README.md also needs the same Tip and command table updates but was outside docs-writer writable scope — the root README should be updated separately.

## Code Review (2026-04-07 19:32)

- Reviewed by: Henry
- Result: FAILED
- Issues: FAIL: (1) Template sync broken — Tip section added to about-dynadocs.md but not Templates/about-dynadocs.template.md. (2) Template sync broken — --files/--gaps-only/--summary flags added to dydo-commands.md but not the template. Both caught by ReadmeClones_ContentInSync and ReferenceDocAndTemplate_HaveSameOptions tests. (3) Doc accuracy — dydo-commands.md examples show --summary/--path/--since without --files, but code (InquisitionCommand.cs:39) only applies those flags when --files is present. (4) Unrelated pre-existing failure: WorktreeCommandTests.Merge_Finalize_DecodesWorktreeIdFromBranchSuffix (ArgumentOutOfRangeException).

Requires rework.