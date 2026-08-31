---
area: guides
type: guide
---

# Working-Tree Contract

How parallel agents open, claim, isolate and clean up branches and worktrees so concurrent work never
tangles.

Assignment is the **claim**: public, exclusive, released by hand once the work lands. One writer per
worktree; commits touch owned paths only.

## 1. Open the feature

Once per Project, at plan approval: the `manager` — or the human when the Project has none — creates
`feature/<project-slug>` from an up-to-date `main`, writes the Project map into the Project
description, and confirms every Issue carries its outcome, owned paths, blockers, exact gates and
base branch. Issues are pickable only when all three are done.

## 2. Claim the Issue and open the tree

Assign the Issue to yourself and move it to In Progress; nothing else claims work. Branch off the
Issue's base branch as `DYD-123-<slug>` (an example key — use the Issue's own): the key in the name
is what lets Linear's GitHub integration attach the branch and the PR. When the host isolates
sessions it hands you a worktree; otherwise make one beside the repository:

```bash
git worktree add -b DYD-123-<slug> ../<repo>.worktrees/DYD-123-<slug> <base-branch>
git -C ../<repo>.worktrees/DYD-123-<slug> rev-parse HEAD   # the base SHA
```

Post branch, base SHA and worktree path on the Issue **before the first edit**. Until that comment
exists the claim is invisible, and a second writer can land on the same files.

## 3. Check the environment

The `implementer` runs this before its first edit and before spawning any worker:

```bash
git rev-parse --abbrev-ref HEAD                          # DYD-123-<slug>
git rev-parse --show-toplevel                            # the worktree, not the main checkout
git merge-base --is-ancestor <base-sha> HEAD && echo ok  # the posted base is in this history
git status --porcelain                                   # empty
```

Owned paths are the fifth check and the only one Git cannot answer: take them from the Issue, and
name every file in the Issue-resolution plan you write as step one (the `planner`'s `issue`
resource). A failed check is a comment on the Issue, never a workaround.

## 4. Land it, then release the tree

Stage the paths you own by name; a whole-tree `git add` sweeps in someone else's half-finished work.
The PR targets the feature branch (`gh pr create --base feature/<project-slug>`) and its body carries
the review block. Merges into the feature branch are serial and `--no-ff`, one Issue at a time, each
followed by a merge review; the feature branch reaches `main` through the human's hands.

Once the merge has landed, whoever made the worktree removes it:

```bash
git worktree remove ../<repo>.worktrees/DYD-123-<slug>
git branch -d DYD-123-<slug>
```

On Windows a worktree that has been built in can refuse removal with `Filename too long`: delete the
folder with `rm -rf` and run `git worktree prune` instead. Whatever survives is swept by the
`chief-of-staff` on its board-hygiene pass, against `git worktree list` and `git branch --merged`.

## Atomic Issues

An Issue with no Project has no feature branch: it branches from `main`, its PR targets `main`, and
its `implementer` merges it after Issue review and merge review. Every other step is unchanged.

## Related

- [dydo Glossary](../reference/dydo-glossary.md) — Locked delivery vocabulary
