---
title: Extract TestProcess.cs helper for the three test-side git invocations
id: 171
area: general
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-05-06
resolved-date: 2026-07-04
---

# Extract TestProcess.cs helper for the three test-side git invocations

Three test-side git invocation sites duplicate the same drain-both-pipes-and-signal-stdin-EOF pattern; extract a single TestProcess.RunGit helper in DynaDocs.Tests/ so the same drift cannot accrete on a fourth caller.

## Description

### Sites

- `DynaDocs.Tests/Services/SnapshotServiceTests.cs` (RunGit helper)
- `DynaDocs.Tests/Commands/InquisitionTests.cs` (InitGitRepo helper)
- `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs` (Git helper)

All three open both stdout/stderr pipes, drain concurrently, and close stdin to signal EOF. The pattern was deliberately duplicated in PR3 of the runtime-regression batch (commit 6d00b4c, #0168) for reviewer-clarity; the long-term plan to centralise it on a TestProcess helper was deferred during PR4 review.

### Fix path

Add `DynaDocs.Tests/TestProcess.cs` exposing one static `(int ExitCode, string Stdout, string Stderr) RunGit(string args, string workingDir, int timeoutMs = 5000)` and migrate all three sites onto it. Mechanical, no behaviour change.

### Why this matters

Without a tracked issue the deferral exists only in archived inbox messages and the changelog — both forms easy to lose. Surfaced by pre-tag-audit Finding #5.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated: two of the three duplicated git-invocation sites are gone (SnapshotServiceTests.cs and InquisitionTests.cs removed with their subsystems); only WorktreeMergeSafetyIntegrationTests.cs remains, so the three-site refactor target no longer exists. Triage sweep 2026-07-04 (Brian, CoS).