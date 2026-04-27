---
area: general
name: fix-ci-after-audit-recovery
status: review-pending
created: 2026-04-27T13:17:07.5062651Z
assigned: Adele
updated: 2026-04-27T13:44:04.7099767Z
---

# Task: fix-ci-after-audit-recovery

# Fix CI failures after audit recovery (3 tests, 2 obsolete + 1 to investigate)

## Why you're here

Master is RED on CI after the audit cherry-pick recovery. Three test failures:

1. **`AuditEdgeCaseTests.ComputeBaselineId_DifferentFileOrder_ProducesDifferentHash`** — `Assert.NotEqual()` Failure: Strings are equal.
   - Test asserts different file order produces different hash.
   - `#0080`'s correct fix (sort `Files`/`Folders` before hashing) makes them produce the **same** hash now. That's the intended new behaviour.
   - Test name and assertion are obsolete.
2. **`AuditEdgeCaseTests.GetSession_SessionIdWithForwardSlash_ThrowsDirectoryNotFound`** — `Assert.Throws()` Failure: Exception type was not an exact match.
   - Test asserts `DirectoryNotFoundException`.
   - `#0073`'s validation throws a different type (probably `ArgumentException` per the standard sanitization pattern).
   - Test needs to assert the new exception type.
3. **`WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning`** — `Assert.True()` Failure.
   - May or may not be related to the audit work. Could be a CI-environment-only flake similar to `#0116`'s sibling-test pattern. Investigate before assuming it's a real regression.

CI run reference: `24996949358` (latest red on master). `gh run view 24996949358 --log-failed` shows the full output.

## Scope

1. **Read each test in `DynaDocs.Tests/Services/AuditEdgeCaseTests.cs`** for failures #1 and #2. Read the corresponding production code (`Services/SnapshotCompactionService.cs:368-379` for `ComputeBaselineId`; `Services/AuditService.cs` `GetSession` for `#0073` validation).

2. **Fix the obsolete tests:**
   - **#1** — rename `ComputeBaselineId_DifferentFileOrder_ProducesDifferentHash` to something accurate (e.g., `ComputeBaselineId_DifferentFileOrder_ProducesSameHash`). Flip the assertion from `Assert.NotEqual` to `Assert.Equal`. Add a one-line comment explaining the change references `#0080`.
   - **#2** — update `GetSession_SessionIdWithForwardSlash_ThrowsDirectoryNotFound` to assert the actual exception type the new sanitization throws. Read `GetSession` to see what it throws now. If the type is `ArgumentException`, rename the test accordingly (e.g., `..._ThrowsArgumentException`).

3. **Investigate failure #3** — `WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning`:
   - Read the test. Read the `WatchdogService.Stop` method.
   - If it's the same CI-only flake pattern `#0116` was about (CWD captured-then-deleted by sibling test), apply the same fix (don't capture CWD; use a test-context dir).
   - If it's a different pattern, report what you found in your message to Brian and DO NOT fix it without asking — could be a real regression we need to understand before patching over.
   - Run the test in isolation: `dotnet test --filter "FullyQualifiedName~WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning"`. Does it pass alone? Fails consistently or intermittently?

4. **Run the full test suite locally** to verify all three are fixed (or two, if #3 turns out to be out of scope).

5. **Commit incrementally and push to `origin/master` between commits** (lessons from this morning's losses):
   ```
   git commit -am "test(audit): align ComputeBaselineId test with #0080's sort-before-hash"
   git push origin master
   git commit -am "test(audit): align GetSession test with #0073's validation"
   git push origin master
   git commit -am "fix(watchdog): <whatever the actual fix is, or skip if not addressing>"
   git push origin master
   ```

6. **Verify CI green** — after pushing, watch the run with `gh run watch` or `gh run list --limit 1`. If the new run is also red, surface to Brian before further commits.

## Hard constraints

- **No worktree.** Tight scope, single agent in flight.
- **`git push origin master` after every commit.** No accumulating local-only commits.
- **Send Brian a status message after each push.** Crash-resilience.
- **If failure #3 is unrelated to the audit work, DON'T fix it as part of this dispatch.** Ask Brian. We're trying to keep the recovery clean.
- **Run the full test suite before declaring done.** All three tests must pass (or two + a clear "this third is out of scope" message).

## Deliverable

1. Up to three commits on master, each pushed to `origin/master` immediately after.
2. Status messages to Brian after each push:
   - `"Pushed test fix for #0080-aligned baseline test."`
   - `"Pushed test fix for #0073-aligned GetSession test."`
   - (if doing #3) `"Pushed Watchdog fix"` or `"Watchdog test fail #3 looks unrelated — leaving for separate investigation. Details: <one paragraph>."`
3. Verify CI green via `gh run list --limit 1`. Wait for the run to complete (or note its in-progress status).
4. Final message to Brian:
   ```
   dydo msg --to Brian --subject fix-ci-after-audit-recovery --body "
   Done. Commits: <list of SHAs>.
   CI: <green | still-red | run in progress>.
   Tests: <count>/<count> locally.
   #3 (Watchdog) outcome: <fixed | flagged for separate investigation | not in scope>.
   Notes: <any deviations>."
   ```

5. Dispatch a reviewer with explicit `--agent` (per `#0108`):
   ```
   dydo dispatch --no-wait --auto-close --agent <chosen-reviewer> --role reviewer --task fix-ci-after-audit-recovery --brief "Review CI fix. See git log master..HEAD."
   ```

Then release.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review CI fix. See git log master..HEAD (2 commits: fc548ce, d012105). Both aligned obsolete tests with #0080 (sort-before-hash) and #0073 (path-separator validation). Watchdog test #3 left for separate investigation per Brian's discretion (different from #0116 pattern). gap_check: 1 pre-existing FAIL on RoleConstraintEvaluator (CRAP 32 from #0045) — unrelated to this task. Verify: (1) test renames and assertion flips correctly reference the production behaviour now in Services/SnapshotCompactionService.cs ComputeBaselineId and Services/AuditService.cs ValidateSessionId; (2) full suite passes (3823/3823 locally); (3) CI green on master HEAD (run 24998191977).