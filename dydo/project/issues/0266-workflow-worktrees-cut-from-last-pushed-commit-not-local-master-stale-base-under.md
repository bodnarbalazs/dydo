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

**B. `merged: true` ≠ committed to HEAD for SINGLE-slice runs (observed twice).** A single-slice
run-sprint runs the slice **in-branch in the main working tree** (no isolated worktree branch to
merge), so it leaves the result as **uncommitted working-tree diffs** and reports `merged: true`
meaning "applied to the tree" — the orchestrator must commit it. Multi-slice parallel runs, by
contrast, create real per-slice branches and DO land actual merge commits (wave 3's c1-5/c1-6
merge commits are on HEAD). Two single-slice losses this sprint:
- **f1b** (corrected security fix): reported `merged: true` + wave-audit PASS, but HEAD still
  carried the first-generation fix with the live bypass; the fix sat as working-tree diffs. A
  re-audit that read the **working tree** reported PASS, so it was invisible until a
  release-candidate `git show HEAD:` check.
- **0270** (hook-trust schema fix): same shape — `merged: true`, but HEAD unchanged, fix
  working-tree-only. **Caught by my own verify-the-landing step this time**, BEFORE any re-audit
  or report.

**Corrected root cause (supersedes the earlier concurrent-race hypothesis).** The f1b loss was
first blamed on a race with concurrent manual master commits. The 0270 loss happened with the
orchestrator **fully hands-off master — zero concurrent commits** — and was lost identically. So
the race was a red herring: the real invariant is that **single-slice / in-branch run-sprint
runs do not commit; they hand an uncommitted tree back to the orchestrator, and `merged: true`
does not mean "on HEAD."** (c1-2 and c1-7, also single-slice, were committed by the orchestrator
by hand — which is why they landed; f1b was the one I trusted without committing.)

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
- **For B (`merged: true` ≠ committed):** the always-applicable rule is **verify-the-landing +
  commit-if-needed**. After every `merged: true`: (1) `git show HEAD:<a-changed-file>` / `git log`
  for the slice's signature — if it is NOT on HEAD (single-slice in-branch case), the tree holds
  the result and **the orchestrator must commit it explicitly** (git add the fix files by path +
  commit + `dydo fix` for any issue moves). (2) Any audit/re-audit MUST read committed HEAD, never
  the working tree — a working-tree read hides an uncommitted loss. (3) Still avoid manual master
  commits *during* a multi-slice run (that race can corrupt real merge commits), but do NOT rely
  on serialize alone — the single-slice non-commit happens with zero concurrency. The verify step
  is the load-bearing one; it caught the 0270 loss before it could reach a report.

## Fix directions

- Determine the actual base-selection rule (harness behavior, not dydo code) and document it in
  guides/orchestration-pitfalls.md.
- If configurable, pin workflow worktree bases to local branch HEAD.
- Make run-sprint's merge-back **atomic and verified**: commit on the invoking branch and assert
  the commit is HEAD before reporting `merged: true`; fail loudly if the working tree is left
  dirty. Ideally merge-back should not depend on a quiescent shared main tree at all.
- Until then, the STEP-0 mitigation (A) and the serialize + verify-landing rules (B) belong in the
  run-sprint skill/brief template so every orchestrator inherits them.

## balazs directive (2026-07-10): this is a SYSTEM shortcoming, fix it at the source

balazs, on being shown the merge/commit workaround: "if this merge/commit detail comes from the
workflow/docs it has to be patched, it's a shortcoming of the system." The verify-the-landing +
commit-if-needed rule and the STEP-0 base merge must NOT stay as per-brief tribal knowledge that
each orchestrator re-discovers (Grace hit manifestation B twice before pinning it). Required
systemic fixes, elevated from the "until then" note:
1. **run-sprint skill**: encode STEP-0 base-merge into the slice-brief template it emits, and make
   the workflow's own post-merge contract verify-the-landing (assert the slice signature is on the
   invoking branch HEAD, commit-if-in-branch, fail loudly if the tree is left dirty) — so
   `merged: true` cannot mean "uncommitted tree."
2. **orchestrator methodology / skill**: state verify-the-landing + audits-read-HEAD-not-tree as
   doctrine, so it compiles into every project's orchestrator artifacts via `dydo sync`.
3. **guides/orchestration-pitfalls.md**: document the base-selection rule and the single-slice
   in-branch non-commit behavior (both were invisible until they bit).
This is a required fix (not optional hardening) and a natural 2.0.8 / P1 companion.

## Reproduction

With local master N commits ahead of origin, launch any run-sprint slice; inspect the worktree
HEAD — it sits at the last pushed commit.

## Resolution

(Filled when resolved)
