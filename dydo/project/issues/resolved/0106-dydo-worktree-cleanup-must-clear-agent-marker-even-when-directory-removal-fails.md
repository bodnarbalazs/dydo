---
id: 106
area: platform
type: issue
severity: low
status: resolved
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-21
---

# dydo worktree cleanup must clear agent marker even when directory removal fails (regression guard test added)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fix landed in def1fa4. The existing cleanup order was already correct (agent marker cleared even when RemoveDirectory fails); the test Cleanup_DirectoryLocked_StillClearsAgentMarker was added to WorktreeCommandTests as a regression guard so the ordering can't silently regress.