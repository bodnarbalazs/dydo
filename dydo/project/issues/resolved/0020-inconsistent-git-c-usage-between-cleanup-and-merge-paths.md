---
title: Inconsistent git -C usage between cleanup and merge paths
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 20
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Inconsistent git -C usage between cleanup and merge paths
Resolved medium-severity correctness finding: cleanup and merge paths used `git -C` inconsistently, sometimes against the worktree and sometimes against the main repo, producing surprises when the working directory differed. Fixed by routing both paths through `mainRoot` consistently.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: Git -C usage now consistent between cleanup and merge paths, using mainRoot throughout