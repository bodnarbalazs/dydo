---
area: general
name: fix-read-permissions-final
status: human-reviewed
created: 2026-03-27T13:33:41.1712808Z
assigned: Frank
updated: 2026-03-27T17:45:36.8252703Z
---

# Task: fix-read-permissions-final

Plan ready. See agents/Frank/plan-fix-read-permissions-final.md. Defensive multi-layered fix for intermittent Read permission prompts in inherited worktree agents. Four changes: (1) add explicit Set-Location/cd in inherited agent launch scripts, (2) init-settings dual-writes to both worktree AND main repo, (3) init-settings adds both slash variants of absolute paths, (4) 1s sleep buffer after init-settings before claude launch. All platforms (Windows/Linux/Mac). See plan for exact code changes and test requirements.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented all 3 plan items: (1) WindowsTerminalLauncher inherited worktree now adds Set-Location to workingDirectory before init-settings and Start-Sleep 1 after. (2) Added WorktreeInheritedSetupScript helper in TerminalLauncher with cd + sleep 1, used by Linux/Mac inherited cases. (3) ExecuteInitSettings now writes both forward-slash and backslash Read entries, and dual-writes to main repo settings.local.json. Updated 4 existing tests for new count, added 7 new tests. All 3289 tests pass, 131/131 coverage modules pass.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-27 17:52
- Result: PASSED
- Notes: LGTM. All 4 plan items implemented correctly: cd/Set-Location for inherited worktrees, dual-write to main repo, both slash variants, sleep buffer. AddMissingEntries helper justified. Shell escaping consistent. 50/50 targeted tests pass. 131/131 coverage gates pass.

Awaiting human approval.