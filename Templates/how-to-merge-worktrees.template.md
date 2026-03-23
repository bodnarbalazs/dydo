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

This merges the worktree branch into the base branch and cleans up the worktree.

If conflicts are detected, resolve them, commit, then finalize:

```bash
dydo worktree merge --finalize
```

After merge completes, dispatch a reviewer and release as normal.

---

## Rules

- **Never use `git merge` directly.** `dydo worktree merge` handles branch cleanup, worktree removal, and marker management that raw git merge skips.
- If conflicts require non-trivial decisions, escalate to the human.
