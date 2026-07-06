---
title: Tests-under-coverage produce 3 unrelated failures (xunit Console-capture isolation + concurrency timing)
id: 165
area: general
type: issue
severity: medium
status: open
found-by: review
date: 2026-05-05
---

# Tests-under-coverage produce 3 unrelated failures (xunit Console-capture isolation + concurrency timing)

`gap_check.py --force-run` exits 0 and the coverage gate reports 139/139 modules passing T1, but the underlying tests-under-coverage run produces three failures that are *not* covered by the gate's pass/fail signal. Two of them are a smoking-gun for xunit parallel collections sharing `Console` capture between concurrently-running tests when slowed by coverage instrumentation; the third may be a real concurrency race in `IncrementResumeAttempts`. None of the three touch the PR1 (#0163) surface they were observed under, so they pre-date that change. The plain `run_tests.py` (no coverage) is reproducibly 4076/4076 clean.

## Description

Surfaced during PR1 (#0163 scan boundary) review. Reviewer Charlie ran both `run_tests.py` and `gap_check.py --force-run`. The latter exits 0 because the coverage gate is the only thing checked — but the underlying test process reports the three failures below.

### Failures

1. **`AgentRegistryTests.IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount`** — `Expected: 10, Actual: 9`. Concurrent-counter timing race. May be a real bug in increment-and-flush exposed when coverage instrumentation slows execution; warrants its own probe before being chalked up to "test timing under coverage".

2. **`WorktreeCommandTests.Merge_BranchNotAdvanced_Blocks_WithoutRunningGitMerge`** — substring assertion expects something containing `"0 commits ahead"`; actual stdout is `"[dydo] WARNING: No role files found at dy..."`. That second string is the message `AuditCompactionTests` (failure #3) is supposed to be asserting against.

3. **`AuditCompactionTests.Compact_CorruptBaseline_LogsWarningInsteadOfSilentSkip`** — substring assertion expects `"[dydo] WARNING"`; actual stdout is `"Refusing to merge worktree/Adele-20260316..."`. That is the message `WorktreeCommandTests` (failure #2) is supposed to be asserting against.

The cross-over in #2 and #3 — each test sees the *other* test's expected `Console.WriteLine` output — is the smoking gun. Two tests asserting against `Console` output are running in parallel collections, and the captured stream is not isolated per test. Coverage instrumentation slows both tests enough that their output windows overlap.

### Why it matters

- `gap_check`'s exit code is what we trust as the test gate; the embedded test failures are silent. The gate is therefore lying — it claims passing, but the assertions inside are failing.
- Under `run_tests.py` (no coverage) the failures don't reproduce because the tests run faster than the overlap window. So we have a state where merging a PR that passes both run_tests.py and gap_check doesn't mean the test suite is actually clean.
- Related: prior parallelism work (issue #148: test-suite runtime 3min → 10min) used a worktree-lock fix. balazs noted that "in theory we got it to be faster, but in practice I don't think the fix was the fix" — this issue is consistent with that observation; the real isolation problem may still be live underneath the previous fix.

## Reproduction

```
$ python DynaDocs.Tests/coverage/gap_check.py --force-run
# Exits 0, coverage gate 139/139.
# Embedded test process reports 3 failures (above) — silent to gap_check exit code.

$ python DynaDocs.Tests/coverage/run_tests.py
# 4076/4076 PASS — no failures.
```

The contrast between the two is the diagnostic.

## Suggested fix paths

1. **Console-capture isolation (failures #2 and #3).** Audit any test asserting against `Console.WriteLine` / `Console.Out`-captured output. Two paths:
   - Migrate to `ITestOutputHelper` (xunit's per-test isolated output), which is the canonical xunit pattern.
   - Or, mark the affected test classes with `[Collection("Sequential")]` (or similar) so they don't run in parallel with each other and other Console-asserting tests.
   The first is the more durable fix.

2. **AgentRegistry concurrency (failure #1).** `IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount` expects exactly 10 and got 9. Probe whether the increment is actually thread-safe (interlocked? lock? naive `++`?). If it's a real race, fix it; if it's a flaky-under-load test, mark/redesign appropriately. Don't paper over.

3. **Reopen the parallelism-fix question** per balazs's observation tied to issue #148 — the previous worktree-lock fix may not have addressed the real isolation problem, just masked symptoms.

4. **Consider making `gap_check.py` non-zero on embedded test failures** so the gate signal matches the underlying test outcome. Today the divergence is the trap.

## Related

- Issue #148 — test suite runtime 3min → 10min, parallelism investigation.
- PR1 (#0163) — where this was surfaced; not the cause.

## Resolution

(Filled when resolved)
