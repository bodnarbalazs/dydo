---
title: dydo worktree cleanup must clear agent marker even when directory removal fails (regression guard test added)
area: platform
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 106
type: issue
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-21
---

# dydo worktree cleanup must clear agent marker even when directory removal fails (regression guard test added)
Resolved low-severity invariant: `dydo worktree cleanup` must clear the agent marker even when `RemoveDirectory` fails (otherwise the agent stays pinned to a phantom worktree). The existing cleanup order was already correct; the fix in `def1fa4` adds `Cleanup_DirectoryLocked_StillClearsAgentMarker` as a regression guard so the ordering cannot silently regress.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fix landed in def1fa4. The existing cleanup order was already correct (agent marker cleared even when RemoveDirectory fails); the test Cleanup_DirectoryLocked_StillClearsAgentMarker was added to WorktreeCommandTests as a regression guard so the ordering can't silently regress.