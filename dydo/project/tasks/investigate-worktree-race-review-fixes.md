---
area: general
name: investigate-worktree-race-review-fixes
status: pending
created: 2026-04-24T16:37:21.2080742Z
assigned: Brian
---

# Task: investigate-worktree-race-review-fixes

# Brief: address reviewer findings on investigate-worktree-race

Reviewer Brian FAILED the first-round review. Full report lives at `dydo/agents/Brian/archive/20260424-163706/review-investigate-worktree-race.md` — read it first, it is concise and correct. Your job is to land the two substantive fixes below. Do not touch anything else.

## Context

Primary bug (already fixed on master): `Commands/WorktreeCommand.cs :: FinalizeMerge` used to tear down the worktree directory unconditionally via `TeardownWorktree`, crashing queued inheriting-worktree dispatches. Fix landed: reorder `RemoveAllMarkers(workspace)` to the top of `FinalizeMerge` so the merger's own `.worktree-hold` is not counted, then call `CountWorktreeReferences(registry, worktreeId)` and skip `TeardownWorktree` when refs > 0. Long version in `dydo/agents/Adele/plan-investigate-worktree-race.md`.

The review did NOT find a logic bug in the fix. It found a weak test and a misleading log line.

## Fix 1 — strengthen the regression test (blocking)

`DynaDocs.Tests/Commands/WorktreeCommandTests.cs :: Merge_Finalize_SkipsDirectoryRemoval_WhenOtherAgentReferences` currently passes for the wrong reason: Brian's setup only writes `.worktree`, not `.worktree-path`. After `RemoveAllMarkers` runs for Adele, `ResolveWorktreePath` returns null and the whole refs>0 branch is skipped. In production `Services/DispatchService.cs :: InheritWorktree` (lines ~384-397) copies `.worktree-path`, `.worktree-base`, and `.worktree-root` alongside `.worktree`. Mirror that.

Exact fix: Brian's setup in the test is around lines 461-467. After the existing `File.WriteAllText(Path.Combine(brianWs, ".worktree"), worktreeId);` line, add:

```csharp
File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);
```

(`worktreePath` is already in scope.)

Also add a stdout assertion to pin the reference-check branch. Capture stdout from the `ExecuteMerge` call and assert it contains `"still referencing"` — the informational line from `FinalizeMerge:913`, emitted only on the refs>0 branch. Use the `CaptureStdout` / `CaptureAll` helper already used around lines 135-160, 633, 883, 1106, 1626.

## Fix 2 — reword misleading "cleaned up" line

`Commands/WorktreeCommand.cs:924`:

```csharp
Console.WriteLine($"Merge finalized. Worktree {worktreeId} cleaned up.");
```

Runs unconditionally, contradicting the :913 line in the refs>0 branch. Reword to something true in both paths:

```csharp
Console.WriteLine($"Merge finalized. Worktree {worktreeId} branch deleted.");
```

## Don't touch

- The `FinalizeMerge` reorder itself — correct.
- The `CountWorktreeReferences` reference-check — correct.
- `Services/TerminalLauncher.cs` and `TerminalLauncherTests.cs :: Launch_WorkingDirectoryMissing_DoesNotStartProcess` — reviewer approved.
- `Services/WatchdogService.cs` and its tests — separate `fix-watchdog-anchor-regression` task, already passed review (Dexter LGTM 3781/3781) and awaiting human approval. Don't flag those as your problem.
- `DynaDocs.csproj` / `npm/package.json` version bumps — unrelated, leave them.

## Verification

1. `python DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~WorktreeCommandTests"` — all green, including strengthened test.
2. `python DynaDocs.Tests/coverage/run_tests.py` — full suite, expect 3781/3781.
3. `python DynaDocs.Tests/coverage/gap_check.py` — exit 0.

## When done

Message orchestrator Brian with a concise status and release. Brian will dispatch a reviewer next.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)