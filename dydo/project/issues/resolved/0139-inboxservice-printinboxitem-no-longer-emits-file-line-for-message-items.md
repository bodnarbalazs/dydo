---
title: InboxService.PrintInboxItem no longer emits File: line for message items
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 139
type: issue
found-by: manual
date: 2026-04-30
resolved-date: 2026-04-30
---

# InboxService.PrintInboxItem no longer emits File: line for message items
Resolved medium-severity finding: an observation that `InboxService.PrintInboxItem` had stopped emitting the `File:` line on message items. Closed as no-repro on master HEAD — five consecutive runs of the targeted test passed (10/10 executions), CI green at run 25179154263, and the emit is in place via commit `22b2c5a`. Likely an environment artifact in the original observation.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
No-repro on master HEAD (473af47). 5 consecutive runs of the targeted test via DynaDocs.Tests/coverage/run_tests.py passed (10/10 test executions). CI green at run 25179154263 (post-v1.4.0). The File: line emit in InboxService.PrintInboxItem is in place via 22b2c5a and continues to function. Adele's original observation during her review of c8e6d83/473af47 was likely a transient environment artifact (dirty worktree pickup or runner glitch), not a real regression.