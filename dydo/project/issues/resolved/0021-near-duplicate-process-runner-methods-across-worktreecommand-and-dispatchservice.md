---
title: Near-duplicate process runner methods across WorktreeCommand and DispatchService
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 21
type: issue
found-by: inquisition
date: 2026-04-07
resolved-date: 2026-04-10
---

# Near-duplicate process runner methods across WorktreeCommand and DispatchService
Resolved medium-severity duplication finding: `WorktreeCommand` and `DispatchService` carried near-duplicate process-runner methods with subtly different `ProcessStartInfo` setup. Fixed in commit `3b554de` by routing `RunProcess` through `RunProcessWithExitCode` internally and eliminating the duplicate PSI setup.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit 3b554de: RunProcess now calls RunProcessWithExitCode internally, eliminating duplicate PSI setup