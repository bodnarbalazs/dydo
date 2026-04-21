---
area: general
name: fix-ci-xunit1031
status: human-reviewed
created: 2026-04-21T10:30:10.1215190Z
assigned: Brian
updated: 2026-04-21T10:49:51.8141299Z
---

# Task: fix-ci-xunit1031

Review fix-ci-xunit1031. Part 1: Converted three WatchdogServiceTests to async Task (Run_ExitsWhenAnchorProcessDies, Run_ExitsWhenCancellationRequested, Run_DeletesPidFileOnExit) — replaced blocking runTask.Wait(ts) with await Task.WhenAny(runTask, Task.Delay(ts)) + Assert.Same + await runTask (per brief pattern); Thread.Sleep(250) → await Task.Delay(250). Part 2: Added repo-root Directory.Build.props with <TreatWarningsAsErrors>true</TreatWarningsAsErrors> (recommended option from brief). Note: code-writer lacks repo-root write perms, human granted a temporary guard lift (dydo guard lift) to create the file. Verification: dotnet build DynaDocs.sln --warnaserror → 0 warnings, 0 errors; run_tests.py → 3776/3776 passed; gap_check.py --force-run → 136/136 modules passing.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review fix-ci-xunit1031. Part 1: Converted three WatchdogServiceTests to async Task (Run_ExitsWhenAnchorProcessDies, Run_ExitsWhenCancellationRequested, Run_DeletesPidFileOnExit) — replaced blocking runTask.Wait(ts) with await Task.WhenAny(runTask, Task.Delay(ts)) + Assert.Same + await runTask (per brief pattern); Thread.Sleep(250) → await Task.Delay(250). Part 2: Added repo-root Directory.Build.props with <TreatWarningsAsErrors>true</TreatWarningsAsErrors> (recommended option from brief). Note: code-writer lacks repo-root write perms, human granted a temporary guard lift (dydo guard lift) to create the file. Verification: dotnet build DynaDocs.sln --warnaserror → 0 warnings, 0 errors; run_tests.py → 3776/3776 passed; gap_check.py --force-run → 136/136 modules passing.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-21 11:01
- Result: PASSED
- Notes: PASS. Part 1: Three WatchdogServiceTests converted to async Task exactly per brief pattern (WhenAny + Assert.Same + await runTask for the two with assertions; WhenAny + Assert.False + await runTask for Run_DeletesPidFileOnExit, matching brief note that 'File.Exists is the real assertion'). Thread.Sleep(250) -> await Task.Delay(250) applied consistently. Part 2: Directory.Build.props at repo root matches recommended XML exactly. Scope matches brief's 'Files expected to change' list. Verification on my end: dotnet build DynaDocs.sln --warnaserror = 0/0; run_tests.py = 3776/3776; gap_check.py --force-run = 136/136.

Awaiting human approval.