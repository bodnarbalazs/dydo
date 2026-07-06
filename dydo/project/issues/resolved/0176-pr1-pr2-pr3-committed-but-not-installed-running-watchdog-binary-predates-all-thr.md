---
id: 176
area: backend
type: issue
severity: critical
status: resolved
found-by: inquisition
date: 2026-05-07
resolved-date: 2026-07-04
---

# PR1/PR2/PR3 committed but not installed: running watchdog binary predates all three fixes — install + watchdog restart required

Source HEAD is post-PR3 (commit 036b88c) but ~/.dotnet/tools/dydo.exe mtime is 2026-05-06 21:06 UTC, predating PR1's commit by 16+ hours. DynaDocs watchdog PID 64796 (start 2026-05-06 21:09:35 UTC) and LC PID 35796 (start 2026-05-07 14:41:05 UTC) both run that pre-fix image. Behavioural fingerprint confirms: 14/14 DynaDocs and 5/5 LC sampled resume/resume_blocked pairs are 60-66s apart — including pairs at 2026-05-07 15:47 UTC and 16:32 UTC, AFTER PR1's 13:46 UTC commit (PR1's 5-min gate would prevent these). Zero resume_outcome events anywhere, zero recovery_kind audit fields anywhere — PR3 emit-sites never fire. Fix: build current HEAD, run global-tool publish step, kill PID 64796 and PID 35796 so they respawn from new binary.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated: point-in-time install-drift observation (2026-05-07); the PR1/PR2/PR3 fixes it flags as uninstalled are present at HEAD source (RecoveryClassifier, WatchdogLogger ResumeOutcomeEvent, WatchdogService resume_outcome emits). Superseded by the standing 'update the installed dydo to 2.0' hardening item. Triage sweep 2026-07-04 (Brian, CoS).