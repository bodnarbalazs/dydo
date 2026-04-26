---
id: 110
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-04-26
---

# Code-writer release lacks a requires-commit gate; uncommitted worktree changes are silently destroyed by worktree cleanup

## Description

A code-writer dispatched into a worktree is required (per `code-writer.role.json`'s `requires-dispatch` constraint) to dispatch a reviewer before releasing. There is **no** corresponding `requires-commit` gate. As a result, a code-writer can:

1. Edit source/test files in their worktree.
2. Run tests against those uncommitted edits.
3. Dispatch a reviewer with a brief that describes the changes.
4. Send a "complete" message to their orchestrator.
5. Release.

…all without ever running `git commit`. On release, `dydo worktree cleanup` runs and (depending on path — see the entanglement with #0108 below) can empty the worktree directory, removing the on-disk uncommitted work. The branch reflog shows zero commits beyond HEAD; `git fsck --lost-found` produces no recoverable dangling commits from the affected session. The work is unrecoverable.

The dispatched reviewer, when it later arrives in the worktree, finds an empty directory and a branch at the original HEAD — no changes to review.

The orchestrator who received the code-writer's "complete" message has every reason to think the fix shipped — the message described the diff in detail, listed the test results, named the regression scenarios. Discovery of the loss happens later, when an attempted merge or a status check reveals the empty branch.

This was hit in this project today. Code-writer Grace was dispatched into `worktree/fix-worktree-merge-self-merge` to fix issue #0107. Her completion message described 4 modified files, 5 new test scenarios, and 138/138 passing tests. After release, `dydo/_system/.local/worktrees/fix-worktree-merge-self-merge/` is empty (timestamp 2026-04-26 13:40 UTC), the branch is at `db3d7f7` with zero commits, and `git fsck` has no dangling commits from the relevant session. Grace's #0107 fix is gone — must be redone.

## Reproduction

1. Dispatch a code-writer with `--worktree`:
   `dydo dispatch --no-wait --auto-close --worktree --role code-writer --task <task> --brief "..."`
2. As that code-writer: edit some source files (do not commit). Run tests. Pass.
3. Dispatch a reviewer per the `requires-dispatch` constraint:
   `dydo dispatch --no-wait --auto-close --role reviewer --task <task> --brief "..."`
4. Send a completion message to the orchestrator:
   `dydo msg --to <orchestrator> --subject <task> --body "..."`
5. `dydo agent release` — release succeeds despite zero commits on the worktree branch.
6. Inspect the worktree directory: it is empty. Inspect the branch: zero commits beyond HEAD. `git fsck --lost-found`: no recoverable dangling commits from this session.

## Likely root cause

Two separate issues compound:

1. **No `requires-commit` constraint on code-writer release.** The `requires-dispatch` of reviewer is enforced because the reviewer needs *something* to review, but the constraint does not check that the worktree branch has any commits beyond the base. The role machinery is structurally capable of expressing this (see `requires-dispatch` for the pattern); a `requires-commit` constraint type would mirror it.
2. **`dydo worktree cleanup` does not refuse to empty a worktree with uncommitted work.** Cleanup should be safe-by-default: if the working tree has unstaged or staged changes, refuse to remove files (or refuse to remove the `.git` link, leaving the worktree intact for recovery). Today it appears to nuke the directory regardless.

The interaction with #0108 (dual-claim) is suspected to make this worse: when the dual-claim's release path runs `dydo worktree cleanup` twice (once per claim of the same agent), the second cleanup is the one that empties the working tree. A pre-cleanup "is the branch ahead of base?" check would have caught this.

## Suggested fix

1. **Add a `requires-commit` constraint** to `code-writer.role.json` that blocks release in a worktree if the branch is not ahead of the base branch. Error message should be actionable: "You have uncommitted work in <worktree>. Run `git add -A && git commit -m '<message>'` before releasing, or `git stash` if you intend to discard."
2. **Make `dydo worktree cleanup` refuse to delete a working tree with pending changes** (untracked or modified, plus any stash entries on the branch). Add a `--force` opt-in for the rare case where cleanup is genuinely desired. Cleanup-on-release should never pass `--force` automatically.
3. Add regression tests:
   - Code-writer with uncommitted changes attempts release → blocked with actionable error.
   - Code-writer commits, releases, cleanup runs → success path.
   - Cleanup against a worktree with uncommitted changes → refused (without `--force`).
   - Dual-cleanup attempt (the #0108 interaction) → second cleanup is a no-op when the worktree is already gone, never destructive.
4. Optionally: when `dydo worktree cleanup` *would* delete a working tree, write a salvage tarball / patch dump to `dydo/_system/.local/salvage/<worktree-id>/` first. Cheap insurance against the next variant of this failure mode.

## Impact

- Silent data loss with no audit trail — the orchestrator's view (the completion message) is the only record of what was supposedly done. The audit log captures events but not file contents, so re-creating the work depends on whatever description the code-writer included in their message.
- High blast radius for any worktree-dispatched code-writer that doesn't think to commit. The mode template (`mode-code-writer.template.md`) does not currently emphasize "commit before dispatching reviewer" as a hard requirement, so this is not a documentation-only gap — the system permits the wrong thing.
- Compounded by #0108 in observed cases. Either issue alone is bad; together they produce silent destruction of work that the dispatcher believes shipped.
- Recovery cost in this project so far: redo of the entire #0107 fix (described in Grace's brief at `dydo/agents/Brian/brief-issue-107-fix.md` and her completion message at `dydo/agents/Brian/inbox/c696a046-fix-worktree-merge-self-merge.md`).

## Related context

- `dydo/_system/roles/code-writer.role.json` — currently has `requires-dispatch` of reviewer; missing `requires-commit`.
- `Commands/WorktreeCommand.cs#ExecuteCleanup` and the cleanup-on-release path triggered from `dydo agent release` — needs the safety check.
- `dydo/_system/templates/mode-code-writer.template.md` — should also document the commit requirement, even after the constraint is added.
- Issue #0108 — the dual-claim issue. Likely amplifier; investigate together.
- The lost session: code-writer Grace, task `fix-worktree-merge-self-merge`, dispatched 2026-04-24, released 2026-04-24T22:50:50Z, work destroyed by 2026-04-26T13:40 UTC.
- Brian's completion message from Grace, preserved as `dydo/agents/Brian/inbox/c696a046-fix-worktree-merge-self-merge.md`, is the only record of what the fix contained.

## Resolution

(Filled when resolved)
