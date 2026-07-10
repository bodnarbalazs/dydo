---
title: Workflow worktree/merge-back mechanism is unsafe under concurrent local-master mutation - stale-base reads AND lost merge-back writes
id: 266
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Grace
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Workflow worktrees cut from last-pushed commit, not local master - stale base under hold-commit accumulation

Every worktree cut during the C1 sprint (run-sprint workflow slices AND a direct worktree-isolated
subagent) was based at `d88102b3` ("2.0.6") — the last pushed commit — while local master had
accumulated 14+ unpushed commits (plan records, merged slices). Consequence: wave-1 workers
implemented against a base missing the sprint's own plan files and sibling seams (c1-4 shipped
blind seams; c1-1 could not flip its row); wave-3 workers hit their base-check tripwire and
stopped cleanly, costing a full round.

## Two manifestations of one root cause (amended 2026-07-10)

Both observed this sprint; both trace to the workflow worktree/merge-back mechanism interacting
badly with **local master that the orchestrator is concurrently mutating** under the
hold-commits-until-release posture.

**A. Stale-base READ (original report).** New worktrees are cut at the last *pushed* commit
(`d88102b3` "2.0.6"), not local HEAD, so slices start missing the unpushed accumulation
(sibling seams, the sprint's own plan files). Cost: wave-1 blind seams, a wasted wave-3 round.

**B. Lost merge-back WRITE (new, and worse).** The f1b corrected security fix ran through
run-sprint, which reported `merged: true` and a wave-audit PASS — but the merge-back commit
**never landed on master HEAD**. The slice content was left as **uncommitted working-tree diffs**
in the main tree (`Commands/ReadCommand.cs`, `GuardCommand.cs`, `ReadCommandTests.cs` all dirty;
HEAD still carried the first-generation fix with the live bypass). Timeline: f1b was launched
concurrently with a manual f2f3 merge + reconcile commits on master; when f1b reached its
merge-back phase the main tree's HEAD had moved, and the merge applied to the working tree/index
without a persisted commit to master. A downstream re-audit that read the **working tree** (which
had the fix) reported PASS, so the loss was invisible until a release-candidate check compared
`git show HEAD:` against the tree. Caught by Adele's RC check, not by any gate.

## Working hypothesis

Worktree bases resolve to remote-tracking state (last pushed), not local HEAD (manifestation A).
And run-sprint's merge-back writes into the **shared main working tree/index**, which is unsafe
when the orchestrator commits to master during the workflow's lifetime — the merge-back's commit
races or is silently left uncommitted (manifestation B). Both are the same unsafe assumption: the
mechanism treats local master as quiescent and remote-anchored when, under hold-commits, it is
neither.

## Mitigation in use (C1, works)

- **For A (stale base):** slice briefs carry STEP 0 — `git merge master --no-edit` in the worktree
  (fast-forwards the stale base onto local master), then a BASE CHECK naming concrete predecessor
  artifacts, with a STOP-and-raise-hand instruction if the check still fails.
- **For B (lost merge-back):** two rules, both now mandatory for the orchestrator. (1) **Do not
  manually commit to master while a run-sprint is in flight** — serialize: let each workflow fully
  complete before hand-committing. (2) **After every `merged: true`, verify the merge actually
  landed on HEAD** (`git show HEAD:<a-changed-file>` / `git log` for the slice's signature) BEFORE
  trusting it or running any audit — a re-audit must read committed HEAD, never the working tree.
  This verify-the-landing step is the process gap that let B reach the RC check.

## Fix directions

- Determine the actual base-selection rule (harness behavior, not dydo code) and document it in
  guides/orchestration-pitfalls.md.
- If configurable, pin workflow worktree bases to local branch HEAD.
- Make run-sprint's merge-back **atomic and verified**: commit on the invoking branch and assert
  the commit is HEAD before reporting `merged: true`; fail loudly if the working tree is left
  dirty. Ideally merge-back should not depend on a quiescent shared main tree at all.
- Until then, the STEP-0 mitigation (A) and the serialize + verify-landing rules (B) belong in the
  run-sprint skill/brief template so every orchestrator inherits them.

## Reproduction

With local master N commits ahead of origin, launch any run-sprint slice; inspect the worktree
HEAD — it sits at the last pushed commit.

## Resolution

(Filled when resolved)
