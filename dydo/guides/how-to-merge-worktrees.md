---
area: guides
type: guide
must-read: true
---

# How to Merge Worktrees

You have a `.merge-source` marker in your workspace. Your job is to merge a worktree branch back into the base branch.

---

## Steps

```bash
dydo worktree merge
```

Run from anywhere — `dydo worktree merge` rehomes to the main repo automatically using the `.worktree-root` marker (with a fallback that walks up out of any worktree CWD). You do not need to `cd` to the main repo first.

If conflicts are detected, resolve them, commit, then finalize:

```bash
dydo worktree merge --finalize
```

After the merge completes, dispatch a reviewer and release as normal.

---

## When the merge does not advance base

`dydo worktree merge` only commits to cleanup when `git merge-base --is-ancestor <merge-source> <base>` confirms the merge actually advanced the base branch. If it did not (self-merge no-op, divergence, etc.), the command:

- Preserves your `.merge-source`, `.worktree-base`, and `.worktree-hold` markers.
- Exits with an error naming the recovery: re-run `dydo worktree merge` from the main repo (or `cd <main-root>` first).

You will see a message of the form:

```
Cannot finalize merge: <base> does not contain <merge-source>. The merge call did not
advance <base> (likely a self-merge from inside the source worktree, or <base> has
diverged). Markers preserved — re-run `dydo worktree merge` from the main repo (or
`cd <main-root>` first).
```

The only sanctioned recovery is to re-run `dydo worktree merge` after fixing the underlying cause (resolve divergence, restore a missing `.worktree-root`, etc.).

---

## Finalize output

`Merge finalized. Worktree <id> branch deleted.` only prints when the base branch advanced **and** the worktree branch was deleted. Two other outcomes are possible and each prints distinct text:

- **Other agents still hold the worktree.** `Merge applied to <base>. Worktree <id>: N agent(s) still referencing — directory and branch kept; the last cleanup will remove them.` This is success — the merge landed; the last referencing agent's release will remove the directory and branch.
- **Branch delete failed.** `Merge applied to <base>, but worktree branch <merge-source> could not be deleted.` followed by the exact recovery command (`git -C "<main-root>" branch -D -- <merge-source>`). The merge landed; only the branch ref is left over.

The previous contradictory pair (`Merge finalized` printed alongside `cannot delete branch ... used by worktree`) can no longer co-occur.

---

## Rules

- **Never use `git merge` directly.** `dydo worktree merge` handles branch cleanup, worktree removal, and marker management that raw git merge skips. `git merge --no-ff` as a recovery fallback is **not available** — it is now blocked by a dydo guard. If `dydo worktree merge` is itself broken in some new way, escalate to the human; do not bypass the guard.
- If conflicts require non-trivial decisions, escalate to the human.

---

## Marker reference

The markers `dydo worktree merge` reads and writes during the flow (see also [architecture.md → Worktree Dispatch](../understand/architecture.md#worktree-dispatch)):

| Marker | Written by | Read by | Cleared when |
|--------|------------|---------|--------------|
| `.worktree-root` | Worktree creation | `dydo worktree merge` (to rehome to main repo) | Workspace teardown |
| `.merge-source` | Reviewer→code-writer merge handoff | `dydo worktree merge` (which branch to merge) | Successful merge that advanced base |
| `.worktree-base` | Reviewer→code-writer merge handoff | `dydo worktree merge` (target branch) | Successful merge that advanced base |
| `.worktree-hold` | Reviewer→code-writer merge handoff | `dydo worktree cleanup` (prevents premature teardown) | Successful merge that advanced base |
| `.needs-merge` | Reviewer | Merger agent / orchestrator | Merge dispatched |

If the merge does not advance base, `.merge-source`, `.worktree-base`, and `.worktree-hold` are **preserved** so the next `dydo worktree merge` invocation can pick up where you left off.
