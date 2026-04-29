---
area: process
type: decision
status: accepted
date: 2026-04-29
---

# 020 — Worktree Usage Policy: Power Option, Not Default

Worktrees are reframed from "default for parallel code work" (Decision 011) to a **power option** orchestrators reach for only in narrow, sized cases, and only with explicit user agreement before dispatch.

This **supersedes Decision 011** for the question of *when* to dispatch with `--worktree`. The mechanics described in 011 (sequential merges, conflict escalation, `git stash` block) remain valid.

---

## Context

Decision 011 (2026-03-16) made `--worktree` the default for any parallel code-writer dispatch. The driving problem at the time: agents on a shared working tree killed each other's `dotnet test` processes, output DLL locks cascaded into mutual process-kills, and `git stash` corrupted neighbours. The cycle was unrecoverable for orchestrators dispatching more than two code-writers in parallel.

Two things have changed since:

1. **The dominant test-contention problem has been addressed at the test-runner layer.** Tests now run in an ephemeral worktree per invocation, so multiple agents on main can run them concurrently without colliding. The everyday justification for `dispatch --worktree` collapsed.
2. **The worktree subsystem has known stability issues** (#0108 dual-claim across main tree and worktree, #0111 stale active-queue marker not auto-cleared, #0112 orphan markers across multiple workspaces) that translate process churn into multi-step manual recovery under guard-lift. These reinforce the policy change but are not the primary driver — even with all three resolved, the everyday case for worktrees would still be gone.

Lived practice on 2026-04-29 already reflects this: every dispatch shipped that day (Henry, Iris, Charlie, Dexter, Jack — all touching `Services/` and tests) was deliberately no-worktree. The orchestrator template still pushed agents toward `--worktree` for any parallel work, leaving Brian's "no worktrees right now" direction load-bearing but undocumented. This decision closes that gap.

## Options Considered

### Option A: "Off until fixed" caveat band

Keep Decision 011's recommendations, prepend a banner tying them to #0108/#0111/#0112. Auto-relaxes when issues close.

**Rejected** — would re-promote worktrees to default once the bugs land, which doesn't match the underlying reality. `run_tests.py` removed the everyday demand independently of the bugs. Treating this as temporary would re-create the same drift between doc and practice.

### Option B: Disjointness-first decision tree

Reframe parallel work around file-disjointness as the primary contention tool, with worktrees as a heavier fallback when disjointness can't be achieved.

**Partially adopted** — file-disjointness is the right primary tool, but framing worktrees as a "fallback when disjointness fails" implies they fix disjointness problems they don't actually fix (two agents editing the same files in two worktrees still produce a merge conflict). The real trigger is *agent count*, not file overlap.

### Option C: Power option, sized, opt-in with user confirmation (CHOSEN)

Default is skip. Worktrees are reserved for narrow, sized cases. Orchestrator may *suggest* worktree use to the user, but never dispatches `--worktree` without the user agreeing.

## Decision

### Default is skip

Orchestrators do not dispatch with `--worktree` by default. Disjoint-file task slicing is the primary parallelism tool — that's good orchestration regardless of worktrees.

### Confirmation is required before any `--worktree` dispatch, except for inquisitors

Orchestrators may *suggest* worktree use when they see a real fit, but they may not dispatch `--worktree` without the user agreeing first. Phrasing example:

> "Sub-task X looks like it would benefit from a worktree because [reason — e.g. 12 parallel code-writers]. Want me to dispatch with `--worktree`? Default is no."

**Inquisitors are the exception.** Worktree is the default for inquisitor dispatches; no confirmation needed.

### Sizing rule

- ~98% of parallel work: 4–5 concurrent code-writers on disjoint files run fine on main. **No worktree.**
- ~2% of parallel work: 10+ concurrent agents (especially 15+) push build/process contention past what main can absorb. **Worktrees become necessary.**

This sizing rule applies whether the orchestrator is root or a sub-orchestrator. Hierarchy is not the trigger; the size of the dispatch fan-out is.

### Real remaining use cases (the only ones the template should name)

1. **Extremely large parallel fan-out (~10+, especially 15+ concurrent code-writers/test-writers).** Build contention is genuinely unavoidable at this scale.
2. **Inquisitors (default).** They spawn test-writers and benefit from clean separation from main. Worktree is the default for this role; no confirmation needed.

### Sub-orchestrators are not a use case on their own

A sub-orchestrator working on a separate domain can run on main like any other agent. Worktrees only enter the picture if *that sub-orchestrator's own dispatch fan-out* hits the sizing rule. "Domain isolation" alone does not justify a worktree.

### Nested worktrees: cut

Decision 011 left nested worktrees as a feature. They are removed from the orchestrator template guidance — only relevant when a parent already has 10+ agents and a sub-domain would push further. Power-on-power, amplifies failure modes.

### What stays from Decision 011

- Sequential merges, orchestrator-coordinated ordering, conflict escalation to human.
- `git stash` blocked in the bash guard.
- "Each worktree task ends with a merge" mechanics (`how-to-merge-worktrees`, `how-to-review-worktree-merges` templates remain authoritative for *how* to merge when one is in play).

## Consequences

- **Template change.** `Templates/mode-orchestrator.template.md` § Worktrees rewritten: default-skip, confirmation-required (except inquisitors), sized use cases only, nested section removed.
- **No code changes.** Worktree subsystem behavior, dispatch flags, and merge mechanics are unchanged.
- **Decision 011 superseded** for the *when-to-use* question. Its mechanics (sequential merges, `git stash` block) remain in force.
- **Open issues #0108 / #0111 / #0112** are unblocked from being resolved on their own merits — this decision does not depend on them, and they no longer block re-promotion of worktrees.
- **Re-evaluate frequency.** Worth revisiting once #0108/#0111/#0112 land and any further test-contention paradigms (beyond `run_tests.py`) shake out, to see whether the sizing thresholds shift.

---

## Affects

- [Templates/mode-orchestrator.template.md](../../../Templates/mode-orchestrator.template.md) — Worktrees section rewrite.
- [Decision 011 — Worktrees as Default for Parallel Development](./011-worktrees-as-default-for-parallel-work.md) — superseded for the *when-to-use* question.
