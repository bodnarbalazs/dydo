---
area: backend
name: fix-test-git-helper-deadlock
status: human-reviewed
created: 2026-05-01T17:35:51.8956251Z
assigned: Tara
updated: 2026-05-01T18:17:28.8001971Z
---

# Task: fix-test-git-helper-deadlock

Fix the 30s WaitForExit cliff in WorktreeMergeSafetyIntegrationTests.Git() — pump stdout/stderr concurrently to avoid pipe-buffer deadlock. Diagnosed in #0148.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit aeee461 for fix-test-git-helper-deadlock (#0148).

Plan: dydo/agents/Tara/plan-fix-test-git-helper-deadlock.md
Brief: dydo/agents/Brian/brief-fix-test-git-helper-deadlock.md

Verify:
- Git() helper in WorktreeMergeSafetyIntegrationTests.cs drains stdout/stderr concurrently (ReadToEndAsync before WaitForExit), kills tree on timeout, surfaces (exit N) + stderr on non-zero exit.
- New regression test WorktreeMergeSafetyGitHelperTests.Git_NoisyOutput_DoesNotDeadlock invokes the helper via reflection with a 256 KB commit + git log -p and asserts <10s.
- Wallclock improvement reproducible. Note: cliff did NOT fire on Wendy's runs - this is the preventative case (baseline 3:12 -> 3:01, both clean baselines), not the headline 10:00 -> 3:20 case. Don't expect dramatic numbers from a clean baseline.

Test results to verify:
- Filtered: python DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~WorktreeMergeSafety" - 27/27 pass.
- Full suite (no coverage): 4018/4018 pass.
- gap_check tier: 137/137. NOTE: gap_check flagged two pre-existing flakes under coverage instrumentation - WorktreeCommandTests.InitSettings_Idempotent_NoDuplicateReadEntry (CWD-race family, #0136/#0137) and FileReadRetryTests.Read_ExclusivelyLockedFile_RetriesAndSucceeds (#0119). Brian confirmed both pre-existing, not regressions, out of #0148 scope. Neither reproduces on the non-coverage 4018/4018 run.

Run dydo check too.

Approve or reject.

## Code Review

- Reviewed by: Adele
- Date: 2026-05-01 18:20
- Result: PASSED
- Notes: PASS. Commit aeee461 implements Tara's plan exactly. Git() helper now drains stdout/stderr concurrently via ReadToEndAsync before WaitForExit, kills the process tree on timeout with a clear 'timed out after 30s' message, and surfaces (exit N) + captured stderr on non-zero exit. New regression test WorktreeMergeSafetyGitHelperTests.Git_NoisyOutput_DoesNotDeadlock invokes the private helper via reflection (with TargetInvocationException unwrap), commits a 256 KB file, runs git log -p, and asserts <10s wall — would have hung 30s on the unfixed helper. Test-file scope only — no production code changed. Verified: filtered run (FullyQualifiedName~WorktreeMergeSafety) 27/27 pass; gap_check 137/137 pass. Grepped the test tree — no caller asserts on the exact failure-message text, so the new (exit N) substring is safe. dydo check shows 19 errors / 7 warnings, all pre-existing working-tree state from other in-flight tasks (template-additions missing titles, inquisitions invalid type 'inquisition', issues/_index.md broken links to issues moved to resolved/, fix-test-git-helper-deadlock.md task-file orphan). None are introduced by aeee461. LGTM — approve.

Awaiting human approval.