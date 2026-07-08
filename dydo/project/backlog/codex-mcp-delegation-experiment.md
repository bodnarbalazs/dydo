---
area: general
type: backlog
status: open
created: 2026-07-08
created-by: Adele
origin: balazs — Codex-via-MCP capability sighting; "having this as an experiment to be checked later is a good idea"
related: [cross-vendor-agent-integration]
related-decisions: [037]
---

# Codex-via-MCP delegation — one gated experiment

## The capability (verified 2026-07-08)

Codex CLI ships a native MCP server mode (`codex mcp-server`); an MCP client (Claude Code) that
registers it gains `codex` / `codex-reply` tools — a Claude manager session can hand a coding
task to a live Codex agent mid-turn and get the result back into its own context. Community
wrappers exist (tuannvm/codex-mcp-server, kky42/codex-as-mcp, mkXultra/ai-cli-mcp). NOT the same
as Claude subagents running Codex models — the subagent `model` field remains Claude-only.

## Why it matters / why it's gated

- **For:** attacks coordination latency directly — step-level delegation, no session spin-up, no
  msg/wait round-trips (the 2026-07-08 adoption sprint ran ~6h wall-clock, mostly coordination).
  Dispatch is structurally task-boundary; MCP is the only step-boundary mechanism.
- **Against:** MCP-spawned codex edits are invisible to every dydo enforcement layer (no
  identity, no guard hooks on the inner process, no board presence) — an unattributed editor on
  a shared tree. Technically triggers DR 037's revisit-when ("cross-vendor subagents inside
  workflows"), but reopening pre-data was declined.
- **Resolution shape: isolation instead of surveillance** — confine MCP-Codex to a
  worktree-isolated slice (disposable checkout); the only path into the shared tree is a
  Claude-reviewed merge of the whole diff. Guard absence stops mattering when the blast radius
  is a throwaway directory and the border is gated. Open sub-question: a codex spawned from the
  repo root may inherit the project's .codex hooks config — verify with one probe.

## The experiment (run AFTER v2.0.6 dispatch smoke + first measured Codex sprint)

One run-sprint where a single worktree-isolated slice is implemented by MCP-Codex: the manager
passes the slice brief via the codex tool, a Claude reviewer gates the merge. Measure wall-clock
and review rounds against two baselines: (a) a comparable Claude-workflow slice, (b) a
dispatched-Codex task. Three data points → decide rail vs curiosity; if rail, design the guard
answer (hooks-inheritance probe first) and reopen DR 037 §1 with evidence.
