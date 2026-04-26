---
id: 112
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-04-26
---

# Worktree marker shape is crash-fragile; orphan markers across multiple workspaces require multi-step manual recovery with guard-lift

## Description

The worktree dispatch and merge flow uses a set of filesystem markers — `.needs-merge`, `.merge-source`, `.worktree-base`, `.worktree-path`, `.worktree-root`, `.worktree-hold` — distributed across the workspaces of all agents involved in a worktree session (the dispatched code-writer, the dispatched reviewer, sometimes a test-writer in between, and the merge code-writer at the end). These markers are the connective tissue of the merge handoff: they tell each agent what to do next and they gate release on certain agents.

When an agent's process dies abruptly (terminal closed, OS killed it, dydo update propagation killed all in-flight processes — all observed in a single 2026-04-26 downstream session), the markers it owned remain on disk. Other agents downstream see "this work is in progress" indefinitely. There is no first-class command to clean orphan markers — recovery requires:

1. Identifying every workspace that holds a marker for the orphaned chain (5+ agent workspaces in the post-mortem's worst case).
2. Asking the human for a `dydo guard lift` because most agent roles cannot edit each others' workspaces.
3. Running a sequence of `rm` commands to clear markers across all affected workspaces.
4. Optionally pruning leftover worktree directories with `dydo worktree prune`.

The downstream post-mortem (`dydo/agents/Brian/incoming-post-mortem-LC-2026-04-26.md`) reports this as a "5-10 minute scrub across 13 agent workspaces" after a single terminal-process death. In a multi-agent burndown session, this can recur multiple times per day.

The blast radius is amplified by the same junction-shared `dydo/agents/` path that surfaces in #0108 — markers in one agent's workspace are visible across worktrees, and stale state in one place can confuse the merge flow elsewhere.

## Reproduction

1. Dispatch a worktree-based code-writer (orchestrator → code-writer in worktree).
2. Allow the chain to proceed: code-writer dispatches test-writer or reviewer, etc. Markers accumulate (`.merge-source`, `.worktree-base`, `.worktree-path`, `.worktree-root` in the involved agents' workspaces; `.needs-merge` once the reviewer signals merge-ready).
3. Kill the active terminal process out-of-band (any of: terminal close, `kill -9`, OS context termination, dydo binary update mid-session).
4. Inspect each involved agent's workspace: markers persist.
5. Attempt new worktree work touching any of the same files / branches: gets confused by leftover state.
6. Recovery: human guard lift; manual `rm` per workspace.

The post-mortem documents three sessions where this fired and the recovery cost.

## Likely root cause

Two interacting issues:

1. **Markers are written eagerly but cleaned lazily.** The handoff chain assumes the next agent will arrive and clear the marker as part of acknowledging it. There's no time-bound or PID-bound on the marker — an orphaned marker is indistinguishable from a marker waiting to be picked up.
2. **No first-class GC.** `dydo worktree prune` cleans worktree directories but doesn't sweep marker files in agent workspaces. There's no equivalent for the agent-side markers.

Together, an abrupt process death turns a marker file from "active handoff state" into "permanent confused state" with no automatic recovery.

## Suggested fix

Recommended composite fix:

1. **Add agent/PID metadata to every marker.** Each marker should record the writing agent's session ID and PID. Readers can detect staleness (PID dead + session not active) without external knowledge.

2. **Introduce a first-class GC command:** `dydo workspace gc` (or `dydo agent reset <name>` for narrower scope). Walks all agent workspaces, identifies markers owned by released or dead-PID agents, archives them to `archive/orphan-markers/<timestamp>/`, and reports the count.

3. **Run the GC implicitly on:**
   - `dydo agent claim <name>` (so a fresh claim of an agent doesn't inherit stale state),
   - `dydo worktree prune` (so worktree teardown sweeps the agent-side markers too),
   - Optionally on `dydo agent list` (cheap pass; surfaces stale markers as warnings).

4. **Document the marker shape and lifecycle** in `dydo/understand/architecture.md#worktree-dispatch` — list every marker, what writes/reads/clears it, and how recovery works. Currently this is implicit knowledge.

5. **Regression test:** simulate a process death mid-handoff (mark file written, process killed before next agent reads), then run `dydo workspace gc` (or `dydo agent claim <name>`), and assert all stale markers are cleared and the agent can proceed.

## Impact

- **Recurring orchestrator overhead.** Each terminal-death event in a worktree session triggers a multi-step manual recovery requiring human guard-lift.
- **Cross-workspace blast radius.** A single orphan can confuse 5-13 agents' workspaces (post-mortem evidence).
- **Compounds with #0108, #0110, #0111.** All four issues touch worktree-related state. A robust marker GC is part of the broader fix surface for "worktree work survives process death".
- **Increases the cost of agent process churn.** Currently, terminal flakiness translates directly into multi-minute recovery costs. With a GC, terminal flakiness is invisible.

## Related context

- `dydo/agents/Brian/incoming-post-mortem-LC-2026-04-26.md` — the post-mortem's "Other related observations" section explicitly calls out marker fragility as a 5-10 minute recovery cost across 13 workspaces.
- `dydo/understand/architecture.md#worktree-dispatch` — current (implicit, sparse) marker documentation.
- `Commands/WorktreeCommand.cs` — worktree marker writers/readers; primary fix surface.
- `Commands/AgentCommand.cs` — `agent claim` is a sensible point to run implicit GC.
- Issues #0108 (dual-claim), #0110 (uncommitted-cleanup), #0111 (stale active-queue) — all sibling failure modes of "the worktree subsystem doesn't recover from process death".
- The marker list per architecture: `.needs-merge`, `.merge-source`, `.worktree-base`, `.worktree-path`, `.worktree-root`, `.worktree-hold`.

## Resolution

(Filled when resolved)
