---
area: general
type: changelog
date: 2026-04-26
---

# Task: investigate-worktree-race

PowerShell fails with `ERROR_DIRECTORY (0x8007010b)` / "Could not access starting directory" when a dispatched agent is launched in a worktree path that was removed between dispatch and launch. Surfaced in LC after the `07d4208` test-writer allowlist change, in a worktree-forest + merge-queue flow.

## Root cause

`Commands/WorktreeCommand.cs:899-901` — `FinalizeMerge` calls `TeardownWorktree` unconditionally. Unlike `ExecuteCleanup` (`:316-321`), it does not consult `CountWorktreeReferences`. When a merger finalizes while another agent (or a queued dispatch carrying `WorkingDirOverride` = worktree path via `Services/QueueService.cs:143-148`) still references the worktree, the directory is deleted out from under them. At dequeue (`Commands/AgentLifecycleHandlers.cs:99-101`, `Services/WatchdogService.cs:316-318`), `TerminalLauncher.LaunchNewTerminal` → `WindowsTerminalLauncher.Launch` sets `psi.WorkingDirectory` to the now-gone path and Process.Start fails with 0x8007010b.

## Plan

See `dydo/agents/Adele/plan-investigate-worktree-race.md` for implementation steps.

## Progress

- [x] Identified root cause (FinalizeMerge skipping reference check).
- [x] Plan written.
- [ ] Failing regression tests added.
- [ ] Fix applied.
- [ ] Full suite green.

## Files Changed

(Pending implementation.)

## Review Summary

# Review brief: worktree race (PowerShell ERROR_DIRECTORY 0x8007010b)

## What happened (user-reported)

User saw PowerShell windows fail to launch with `error 2147942667 (0x8007010b)` / `Could not access starting directory "…\dydo\_system\.local\worktrees\tier-registry-warnings-fix"` while running a worktree-forest merge queue in their LC project. Surfaced after commit `07d4208` ("test writer merge bug fix" — widened reviewer → code-writer dispatch allowlist to include test-writer-originated chains).

## Root cause (see `dydo/agents/Adele/plan-investigate-worktree-race.md` for the long form)

`Commands/WorktreeCommand.cs :: FinalizeMerge` called `TeardownWorktree(worktreePath, mainRoot)` unconditionally (old lines 899-901). Unlike `ExecuteCleanup` (:316-321), it never checked `CountWorktreeReferences`. Meanwhile, queued **inheriting-worktree** dispatches persist the worktree path as `WorkingDirOverride` in `Services/QueueService.cs:143-148` and re-launch with it at dequeue time from `Commands/AgentLifecycleHandlers.cs:99-101` or `Services/WatchdogService.cs:316-318`. When a merger dequeued ahead of a sibling inheriting dispatch, `FinalizeMerge` deleted the directory out from under it; the dequeued sibling was then handed to `Process.Start` with a now-invalid `WorkingDirectory` and crashed with `ERROR_DIRECTORY`. The widened allowlist made the forest produce more concurrent inheriting dispatches coexisting with mergers, which is why it started hitting.

## The fix (two surgical edits)

1. **`Commands/WorktreeCommand.cs` → `FinalizeMerge`**
   - Moved `RemoveAllMarkers(workspace)` to the top so the merger's own `.worktree-hold` is not counted as a reference.
   - Before `TeardownWorktree`, now calls `CountWorktreeReferences(registry, worktreeId)`. When `> 0`, the directory is kept with an informational line ("… agent(s) still referencing — directory kept; the last cleanup will remove it.") and the last referrer's `dydo worktree cleanup` (already in the PS `finally` block) tears it down when refs hit zero.
   - `git worktree prune` and `git branch -D -- {mergeSource}` still run — the merged branch is always deleted, only the physical directory teardown is gated.

2. **`Services/TerminalLauncher.cs` → `Launch`**
   - If `workingDirectory != null && !Directory.Exists(workingDirectory)`, throw `DirectoryNotFoundException` immediately. The existing catch block then prints the usual "WARN: Could not launch terminal … Please manually open a new terminal and run: claude '…'" and returns 0. Keeps the target terminal from ever crashing with the cryptic `0x8007010b`.

## New regression tests (both red on master, green after fix)

- `DynaDocs.Tests/Commands/WorktreeCommandTests.cs :: Merge_Finalize_SkipsDirectoryRemoval_WhenOtherAgentReferences` — Adele is the merger with `.merge-source`/`.worktree-hold`, Brian holds `.worktree = wtId`, worktree dir exists on disk. After `ExecuteMerge(finalize: true)`: directory preserved, Brian's `.worktree` untouched, Adele's merger markers cleared, `git branch -D -- worktree/<id>` attempted, `git worktree remove` NOT attempted.
- `DynaDocs.Tests/Services/TerminalLauncherTests.cs :: Launch_WorkingDirectoryMissing_DoesNotStartProcess` — passes a missing directory as `workingDirectory`; asserts `pid == 0` and `recorder.Started` is empty.

## What to verify

- Re-read `plan-investigate-worktree-race.md` against the diff — did I miss any call site that could still tear down with refs present? (Checked `ExecutePrune` already consults `CountWorktreeReferences`; `ExecuteCleanup` already correct; `FinalizeMerge` was the only gap.)
- Full test suite: `python DynaDocs.Tests/coverage/run_tests.py` — expect 3779/3779 pass. I confirmed this.
- Coverage gate: `python DynaDocs.Tests/coverage/gap_check.py` — expect 136/136 modules at tier. I confirmed this.
- Does the existing `Merge_Finalize_RemovesZombieWorktreeDirectory` still pass? It uses only `.worktree-path` on Brian (not `.worktree`), so refs stay at 0 and teardown proceeds — yes, still passes.
- Sanity: branch deletion after finalize is safe even when the directory is still populated (other agents won't be on a deleted branch in practice — the merger path only fires after a review pass in that worktree).

## Files changed

- `Commands/WorktreeCommand.cs` (FinalizeMerge body reordered + reference check)
- `Services/TerminalLauncher.cs` (DirectoryNotFoundException pre-check at top of `Launch` try block)
- `DynaDocs.Tests/Commands/WorktreeCommandTests.cs` (+1 test)
- `DynaDocs.Tests/Services/TerminalLauncherTests.cs` (+1 test)
- `dydo/agents/Adele/plan-investigate-worktree-race.md`, `dydo/project/tasks/investigate-worktree-race.md` (records)

## Out of scope (acknowledged, not fixed here)

- Separate auto-close regression from commit `def1fa4` — being handled on its own task (`fix-watchdog-anchor-regression`). Do NOT try to fix it as part of this review.
- Broader refactor of worktree lifecycle reference tracking.


## Code Review

- Reviewed by: Dexter
- Date: 2026-04-24 17:27
- Result: PASSED
- Notes: Second-round review PASS. Charlie's fixes address both first-round findings: test now pins the refs>0 branch via Assert.Contains('still referencing', stdout), and the final log line ('branch deleted') is true in both paths. Full suite 3781/3781 green; gap_check 136/136 modules pass. Original FinalizeMerge reorder + CountWorktreeReferences gate + TerminalLauncher DirectoryNotFoundException pre-check all intact. Report: dydo/agents/Dexter/review-investigate-worktree-race.md

Awaiting human approval.

## Approval

- Approved: 2026-04-26 19:39
