---
id: 120
area: backend
type: issue
severity: low
status: open
found-by: review
date: 2026-04-27
---

# WatchdogServiceTests.EnsureRunning_SpawnedFromWorktree_SetsWorkingDirectoryToMainProjectRoot flakes — file-in-use on wt-abc at Dispose

Open low-severity flake report: `WatchdogServiceTests.EnsureRunning_SpawnedFromWorktree_SetsWorkingDirectoryToMainProjectRoot` intermittently throws a file-in-use IOException on the `wt-abc` directory at `Dispose` time. Awaiting investigation into the lingering handle that prevents deterministic teardown.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)