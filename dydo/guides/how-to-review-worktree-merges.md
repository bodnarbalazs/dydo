---
area: guides
type: guide
must-read: true
---

# How to Review Worktree Merges

You are reviewing a merge task. A code-writer merged a worktree branch into the base branch.

---

## What to Check

1. **Commits landed** -- Run `git log --oneline -10` and verify the expected commits from the worktree branch are present.
2. **No unintended changes** -- The merge should only contain the commits from the worktree. Check `git diff` if in doubt.
3. **Tests pass** -- Run the test suite. Merge conflicts or bad resolutions often break tests silently.

## Zero-Change Merges

If the merge task resulted in **no actual file changes** (no new commits, no diff), this is a **FAIL**. It means the worktree work was never properly committed, the branch was empty, or something went wrong during the merge. Do not pass empty merges.
