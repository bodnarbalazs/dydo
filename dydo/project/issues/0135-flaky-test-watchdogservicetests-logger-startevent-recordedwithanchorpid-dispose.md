---
title: Flaky test: WatchdogServiceTests.Logger_StartEvent_RecordedWithAnchorPid Dispose IOException on Windows
area: general
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 135
type: issue
found-by: review
date: 2026-04-30
---

# Flaky test: WatchdogServiceTests.Logger_StartEvent_RecordedWithAnchorPid Dispose IOException on Windows
Open low-severity flake report: `WatchdogServiceTests.Logger_StartEvent_RecordedWithAnchorPid` intermittently throws an IOException at `Dispose` on Windows, indicating an unflushed file handle on the watchdog log path. Awaiting fix to ensure the test cleans up the underlying writer before the temp directory is deleted.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
(Filled when resolved)