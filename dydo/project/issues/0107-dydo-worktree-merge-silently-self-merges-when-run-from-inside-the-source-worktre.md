---
id: 107
area: general
type: issue
severity: high
status: open
found-by: manual
date: 2026-04-24
---

# `dydo worktree merge` silently self-merges when run from inside the source worktree, then consumes markers

## Description

When `dydo worktree merge` is invoked from inside the source worktree directory (i.e., CWD is `dydo/_system/.local/worktrees/<branch>` which is checked out to `worktree/<branch>`), the tool:

1. Reports "Already up to date" (git merged branch X into branch X) rather than detecting the self-merge and erroring out
2. Does NOT advance `master` — the worktree commits stay unmerged
3. **Deletes the `.merge-source`, `.worktree-base`, and `.worktree-hold` markers from the agent's workspace as part of its "finalize" step** — so the agent cannot retry via `dydo worktree merge`
4. Also emits a contradictory error `cannot delete branch 'worktree/<branch>' used by worktree at '...'` alongside a success-looking `Merge finalized` line

The result: the worktree is stuck in an unmerged state with no dydo-sanctioned recovery path, and the agent has to fall back to a manual `git merge --no-ff` on master to land the work (producing the `Merge worktree/<branch> (recovery)` commits that have accumulated in at least one downstream project's `master` history).

## Reproduction

1. Dispatch a code-writer with `--worktree` on some task.
2. Agent performs normal work, commits on the worktree branch.
3. Agent's chain produces the normal `.merge-source` / `.worktree-base` / `.worktree-hold` markers in their workspace via the reviewer→code-writer merge-dispatch handoff.
4. Agent does: `cd dydo/_system/.local/worktrees/<branch>` (CWD is now inside the source worktree, HEAD is on `worktree/<branch>`).
5. Agent runs `dydo worktree merge`.
6. Output is the internally contradictory message above.
7. Inspect master: `git log --oneline -5` on the main repo shows master unchanged.
8. Inspect marker state: `.merge-source` / `.worktree-base` / `.worktree-hold` have been removed from the agent's workspace.
9. Re-running `dydo worktree merge` from the worktree OR from the main repo errors with `No .merge-source marker found. Nothing to merge.`

Observed this session on tasks `frontend-refactor-R1-impl-merge` (Frank, 2026-04-24) with worktree branch `worktree/frontend-refactor-R1-impl` at commits `5b08934` + `fe2eb34`.

The same failure mode appears to be the origin of at least four `Merge worktree/<branch> (recovery)` commits on the same downstream project's master:

- `908aad3` Merge `worktree/frontend-refactor-R2-impl` (recovery)
- `ac41d5c` Merge `worktree/frontend-slice-15-frontend-hooks-cluster` (recovery)
- `327bb88` Merge `worktree/frontend-slice-06-file-editor-inspector-panels` (recovery)
- `1fc27a5` Merge `worktree/frontend-slice-10-editor-modals-sections` (recovery)

All produced by a single consolidated recovery merger agent who had to fall back to manual `git merge` on master because the dydo-sanctioned flow kept self-merging. Pattern is recurring.

## Likely root cause

`dydo worktree merge` does not check whether CWD is inside the source worktree (or equivalently, whether git HEAD in the process's CWD is already on the worktree branch) before invoking `git merge`. If it invokes `git merge worktree/<branch>` while already on `worktree/<branch>`, git trivially returns "Already up to date" — the self-merge case.

Suggested guard: at entry, verify `git rev-parse --show-toplevel` and the current branch. If CWD is inside the source worktree OR HEAD is on the source branch, either:

- (a) internally switch to the base branch (via `git -C <main-repo-path>`) before merging, or
- (b) hard-error with an actionable message: "dydo worktree merge must be run from the main repo, not from inside the source worktree. Run `cd <main-repo-path>` first."

Either fixes the self-merge. (a) is friendlier. (b) surfaces the misconception explicitly.

Separately: the marker-cleanup step should not run when the actual merge didn't advance master. Today the "finalize" step blindly deletes markers even after the merge was a no-op — leaving no retry path. Markers should persist unless `git merge-base --is-ancestor <branch> master` returns true post-merge (or equivalent assertion).

## Impact

- At least 5 observed occurrences across a single session (4 merged via manual recovery, 1 currently stuck pending recovery).
- Each stuck merge blocks the downstream task's human-approval step and may mask real regressions: if the agent assumes the merge landed because "Merge finalized" was printed, they may not check master and may release their worktree slot, leaving orphan commits.
- The workflow rule "never use git merge directly" in `how-to-merge-worktrees.md` has been overridden ad-hoc in orchestrator messaging to unstick these. This is a de facto policy drift that should not be needed.

## Suggested fix

1. **Guard** `dydo worktree merge` against self-merge (one of the two options above).
2. **Preserve markers** when the merge is a no-op so the agent can retry after correcting CWD.
3. **Document the correct invocation CWD** in `dydo/guides/how-to-merge-worktrees.md` — the current guide does not specify where to run the command, and the natural assumption ("the workspace where the markers live" or "the worktree where the branch lives") leads directly to the self-merge bug.
4. **Add a regression test** that simulates an agent running `dydo worktree merge` from inside the source worktree — assert either a clean merge or a clear error, never a silent no-op.

## Related context

- `dydo/guides/how-to-merge-worktrees.md` — current guide doesn't specify invocation location
- `dydo/_system/templates/mode-code-writer.template.md` — refers agents to the guide
- The downstream project's `dydo/agents/Adele/archive/` and `dydo/agents/Frank/archive/` contain the complete conversation log from the 2026-04-24 session in which this issue was observed repeatedly.

## Resolution

(Filled when resolved)
