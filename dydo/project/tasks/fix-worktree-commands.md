---
area: general
name: fix-worktree-commands
status: human-reviewed
created: 2026-03-20T13:28:48.4142757Z
assigned: Brian
updated: 2026-03-20T14:05:09.7095830Z
---

# Task: fix-worktree-commands

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed all three issues: (1) ExecuteInitSettings now adds Read(**) alongside the absolute path Read entry so Claude Code's relative paths match. (2) Added console progress message before git merge. (3) Worktree setup now creates a junction/symlink for dydo/_system/roles/ alongside dydo/agents/ in both Unix (TerminalLauncher.WorktreeSetupScript) and Windows (WindowsTerminalLauncher.GetArguments), and cleanup removes both junctions. Refactored RemoveAgentsJunction to generic RemoveJunction accepting a junction path. Added 7 new tests covering all three fixes. All 44 WorktreeCommandTests pass, no coverage regressions.

## Code Review (2026-03-20 13:49)

- Reviewed by: Frank
- Result: FAILED
- Issues: One formatting defect: WorktreeCommand.cs:408 has 16 spaces indent instead of 12 (misaligned with lines 409-410 and the identical block at 160-161). Fix the indentation and it passes.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-20 16:53
- Result: PASSED
- Notes: LGTM. All three original fixes correct: Read(**) wildcard permission, merge progress output, roles junction setup/cleanup. RemoveJunction refactor clean. Indentation fix verified. 7 meaningful tests. No coverage regressions (5 gap_check failures are pre-existing in unrelated files). Code is clean.

Awaiting human approval.