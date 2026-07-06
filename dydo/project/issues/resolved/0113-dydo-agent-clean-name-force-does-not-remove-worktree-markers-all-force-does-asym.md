---
title: dydo agent clean <name> --force does not remove worktree markers; --all --force does (asymmetry causes broken per-agent recovery)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 113
type: issue
found-by: manual
date: 2026-04-26
resolved-date: 2026-04-26
---

# dydo agent clean <name> --force does not remove worktree markers; --all --force does (asymmetry causes broken per-agent recovery)
Resolved medium-severity correctness bug: `dydo agent clean <name> --force` left worktree markers behind while `dydo agent clean --all --force` cleared them, breaking per-agent recovery from a half-cleaned state. Fixed in commit `4d59fe4` by extracting a `RemoveWorktreeMarkers` helper covering all 7 markers and wiring it into `CleanAgent`, `CleanByTask`, and `CleanAll` symmetrically; covered by repurposed and new tests.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
WorkspaceCleaner asymmetry fixed in commit 4d59fe4. Extracted private static RemoveWorktreeMarkers helper (7 markers); wired into CleanAgent, CleanByTask, and CleanAll. Tests: flipped existing bug-codifying Clean_SingleAgent_PreservesWorktreeMarkers into Clean_Agent_WithWorktreeMarkers_RemovesAllSeven; added Clean_ByTask_WithWorktreeMarkers_RemovesAllSeven. 3796/3796 pass, gap_check 100%. Investigation by Emma (dydo/agents/Emma/notes-per-agent-clean-marker-leak.md), implementation by Charlie, reviewed by Dexter. Adjacent observation for cluster A slice F: WorktreeCommand.cs:379-393 RemoveAllMarkers omits .needs-merge — third asymmetry, subsumed by upcoming dydo workspace gc consolidation.