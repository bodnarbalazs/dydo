---
title: RunProcessWithExitCode masks failures when only void override is set
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 18
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# RunProcessWithExitCode masks failures when only void override is set
Resolved medium-severity correctness bug: `RunProcessWithExitCode` fell through to a void override when its own override wasn't set, masking failures that would have surfaced via the exit code. Fixed by checking the dedicated override first.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed: RunProcessWithExitCode properly checks its own override first, no longer falls through to void override