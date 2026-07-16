---
area: guides
type: guide
---

# Orchestration Pitfalls

Field-tested failure modes of multi-agent work on this repo — worktree isolation, workflow invocation, and the shared working tree. Read this before running a sprint or dispatching parallel work; every pitfall below cost a real session to discover. Each entry gives the symptom you'll see, the mechanism behind it, and the rule that avoids it.

---

## 1. Worktree isolation branches from a stale base

**Symptom:** An isolated agent reports the codebase's infrastructure as "missing" and raises its hand — infrastructure you know is committed.

**Mechanism:** A Workflow `agent({isolation: 'worktree'})` creates its worktree under `.claude/worktrees/` from a **stale base commit** — not the current branch HEAD, not even master's tip (observed: a worktree based on an old release-era commit, missing an entire campaign committed on the feature branch). An isolated agent cannot see committed work on an unmerged branch.

**Rule:** Worktree isolation is only safe for **parallel slices whose prerequisite work is already on the worktree's base** (i.e. merged). For a single slice, or any slice building on unmerged branch state, run in the main tree with no isolation — `run-sprint.js` gates isolation on `slices.length > 1` for exactly this reason. Related trap: saved-workflow `name` resolution serves a stale cached script within a session — after editing a workflow file, run the live file via `scriptPath`.

---

## 2. Workflow args silently collapse when stringified

**Symptom:** A multi-slice sprint runs as a single slice named `slice-1` whose brief is raw JSON. The workflow may still stamp `merged: true` for work that never reached the tree; often only the sprint-auditor's `fail` verdict exposes it.

**Mechanism:** Passing `args` to the Workflow tool as a JSON-encoded **string** (e.g. `args: "[{...}, {...}]"`) delivers one string to the script; run-sprint's `normalizeSlices` wraps the whole blob as one slice. Worse, passing a genuine JSON array is **not sufficient either** — the harness/permission pipeline has been observed re-stringifying args even when passed as a real array, defeating run-sprint's defensive `JSON.parse` in `normalizeSlices`. The collapse recurred even after that hardening landed.

**Rules:**

- **Always verify the returned ledger:** slice names must match what you passed. A lone default-named `slice-1` means the collapse happened.
- **Prefer single-slice, plain-prose waves.** Single-slice sprints are immune: pass the brief as a plain prose string (not JSON) — it wraps as one slice and runs in the main tree by design. When slices are dependency-ordered anyway, sequence one slice per wave.
- **When collapsed work lands in-tree uncommitted,** fix rounds must run as in-tree agents (worktrees can't see uncommitted changes), and commit only after re-review.

Two sibling anomalies live in the same structured-result layer (details in [Notion board follow-ups §D](../project/backlog/notion-board-followups.md)):

- A boolean field (`raiseHand: false`) was silently **dropped** from a worker's structured payload, failing validation; the worker emitted a fake escalation just to deliver its report. Don't trust absent boolean fields in worker payloads, and treat unexplained escalations as possible delivery workarounds.
- The StructuredOutput retry cap (×5) **killed a workflow after its worker had already applied all in-tree work**. A dead workflow ≠ lost work: read the run's `journal.jsonl`, salvage the in-tree state, and re-review it instead of re-running.

Net pattern: workers do the code fine; failures concentrate in structured-result serialization and delivery. The proven playbook is single-slice plain-prose waves plus journal salvage.

---

## 3. Disjoint files still collide through global gates

**Symptom:** Agent B's test gate goes red on code agent B never touched, because agent A's slice is mid-flight.

**Mechanism:** `DynaDocs.Tests/coverage/run_tests.py` copies **all** dirty and untracked files into its throwaway test worktree, so any gate run sees the *union* of every agent's in-flight edits. Several gates are global, not file-scoped: `CommandDocConsistencyTests` (help text / `dydo/reference/*` / templates must match the command tree), `dydo check` over untracked scaffolding, and coverage-tier totals. So "different files = safe to parallelize" is false here — when agent A adds a command mid-flight (command present, docs not yet), agent B's gate run reddens on A's incomplete state.

**Rule:** Sequence tree-shared work explicitly — one agent commits their complete slice, pings, *then* the next dispatches. Don't run your final gate while another agent's slice is mid-flight. Agent-tool worktree isolation is not a fix for dependent slices (see pitfall 1: the worktree branches from a stale base, missing a just-landed dependency commit).

---

## 4. Shared-tree commit race: workers never self-commit

**Symptom:** A worker's commit contains another agent's half-finished work; or a worker's own files get rewritten under it mid-session.

**Mechanism:** dydo agents work concurrently in the **same main working tree**, not per-agent worktrees. At any moment the uncommitted tree can commingle several agents' in-flight changes. A worker running `git commit` — especially `git add -A && commit` — races peers and sweeps their incomplete work into the commit. Green tests do not make it safe: the test runner copies the commingled dirty tree, so the suite can pass while committing it is still wrong. Even staging **explicit paths** is not a safe escape hatch — a peer's in-flight refactor can be modifying the *same files* you edited (observed: a concurrent refactor touching a worker's two files at session start, and a peer rewriting the worker's test file live, under its edits).

**Rule:** As a worker: finish your edits, prove the suite and gate green, report structured results, then **hold** on your general wait. The Tier-1 orchestrator sequences a careful landing — staging only your paths, in dependency order across agents — and sends exact steps; follow them precisely and never improvise a commit. Any "rebase and land your slice" plan that assumes isolated worktrees is wrong here. If you find the tree racing (peer edits in your files), report and hold — verifying or re-applying in a racing tree is futile.

---

## 5. Surgical staging: committing only your hunks

When a landing *is* sequenced and a file you touched also carries a peer's uncommitted hunks, a whole-file `git add` sweeps their work into your commit. Pitfall 4 is the hazard; this is the commit-time remedy (verified in practice, landed clean):

1. Per touched file: `git diff --numstat -- FILE` and count `^@@` hunks. If hunk count and added lines match your edit exactly → single authorship → whole-file `git add` is fine.
2. Polluted file (extra hunks) → stage only your hunk(s): `git diff -- FILE > f.diff`, `grep -nE '^@@' f.diff` to find hunk boundaries, `sed -n '1,4p;<hunkStart>,9999p' f.diff > f.patch` (diff header lines 1–4 plus your hunk), then `git apply --cached --recount f.patch` (`--recount` absorbs the offsets broken by the omitted hunks).
3. `git add` new files and all single-authorship files normally.
4. **Verify before commit:** `git diff --cached -- <each polluted file>` must show only your change. Polluted files staying `M` after commit (the foreign remainder, unstaged) is correct — not a miss.
5. Hand the orchestrator a **shared-tree caveat** note listing which files carry foreign hunks and exactly which hunk is yours — the single most useful artifact of a sequenced landing.

Guard gotchas while doing this in Bash: the guard blocks write-commands (redirects) containing `$` (including sed's end-of-file `$` — use a big literal like `9999`), `$(...)`/backtick substitution, embedded newlines (join with `;` on one line), and `cd &&` chaining. Use literal absolute paths. Commit multi-line messages containing backticks via `git commit -F <file>` (write the message file first), never `-m`.

---

## 6. Junction-shared dirs write through worktree isolation

**Symptom:** From a dydo-launched worktree terminal, edits under certain `dydo/` dirs appear live in the main tree — a cross-dir move half-applies immediately and then conflicts on merge-back.

**Mechanism:** `WorktreeCommand.JunctionSubpaths` (`Commands/WorktreeCommand.cs`) junctions `dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions`, `dydo/project/backlog`, and `dydo/project/future-features` into dydo-launched worktree terminals — writes there go through the junction into the main tree. This applies **only** to dydo's own worktree launch path; Claude-native Workflow worktrees have no junctions (their hazards are pitfalls 1 and the shared `_index.md`/link surfaces instead).

**Status (2026-07-08):** this machinery is suspected dead code post-2.0 (`run-sprint` uses Claude-native worktrees; `dydo worktree` no longer has a `create` subcommand) — under investigation in the [DR 034](../project/decisions/034-pm-record-taxonomy.md) sprint. Don't cite junction write-through as the sole reason to sequence doc-move slices without checking the machinery still exists; the stale-base and shared-index arguments (pitfalls 1 and 3) carry that recommendation on their own.

---

## Related

- [Coding Standards](./coding-standards.md) — Workflow discipline for Tier-2 workers
- [Writing Good Briefs](./writing-good-briefs.md) — Briefing dispatched agents
- [Testing Strategy](./testing-strategy.md) — The gates agents collide through
- [Decision 024](../project/decisions/024-dydo-2-native-pivot.md) — dydo 2.0 native pivot
- [Decision 026](../project/decisions/026-tier1-managers-doctrine.md) — Tier-1 managers doctrine
