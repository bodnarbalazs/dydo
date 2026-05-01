---
area: general
name: fix-docs-quality-workflow
status: human-reviewed
created: 2026-05-01T12:30:25.3526035Z
assigned: Henry
updated: 2026-05-01T14:07:43.7375802Z
---

# Task: fix-docs-quality-workflow

Review commit e338115 for fix-docs-quality-workflow (#0146). Brief: dydo/agents/Brian/brief-fix-docs-quality-workflow.md. Verify: (1) Templates/mode-reviewer.template.md now has a 'Run dydo check' step in Verify (step 4), (2) Templates/mode-docs-writer.template.md completion-block is tighter on dydo check (replaces 'When documentation is complete and dydo check passes' with explicit exit-zero gate; also adds a release-gate note in 'Before Committing'), (3) Templates/mode-docs-writer.template.md adds a 'Writing Content' subsection with the summary-paragraph guideline. Run dydo check yourself as part of the verdict per the new rule. Approve or reject. Note: full suite hit 2 unrelated flakes (PrintInboxItem_TaskItem_IncludesFilePath, WorktreeCommandTests.InitSettings_CopiesSettingsWithReadPermission) on earlier runs; both pass in isolation; third full run was clean (4012/4012). gap_check 137/137. Brian notified to file the flakes.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit e338115 for fix-docs-quality-workflow (#0146). Brief: dydo/agents/Brian/brief-fix-docs-quality-workflow.md. Verify: (1) Templates/mode-reviewer.template.md now has a 'Run dydo check' step in Verify (step 4), (2) Templates/mode-docs-writer.template.md completion-block is tighter on dydo check (replaces 'When documentation is complete and dydo check passes' with explicit exit-zero gate; also adds a release-gate note in 'Before Committing'), (3) Templates/mode-docs-writer.template.md adds a 'Writing Content' subsection with the summary-paragraph guideline. Run dydo check yourself as part of the verdict per the new rule. Approve or reject. Note: full suite hit 2 unrelated flakes (PrintInboxItem_TaskItem_IncludesFilePath, WorktreeCommandTests.InitSettings_CopiesSettingsWithReadPermission) on earlier runs; both pass in isolation; third full run was clean (4012/4012). gap_check 137/137. Brian notified to file the flakes.

## Code Review

- Reviewed by: Adele
- Date: 2026-05-01 14:13
- Result: PASSED
- Notes: All 3 template edits match brief: (1) reviewer template adds 'Run dydo check' as Verify step 4, (2) docs-writer template tightens completion gate to explicit exit-zero plus release-gate note in 'Before Committing', (3) docs-writer template adds 'Writing Content' subsection with summary-paragraph guideline. Build clean (0/0). gap_check 137/137. Pre-existing dydo check errors (41) confirmed out-of-scope per brief; another agent handling cleanup.

Awaiting human approval.