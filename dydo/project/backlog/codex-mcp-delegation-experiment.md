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

## Exploration findings (2026-07-08, Noah co-thinker round with balazs)

Hands-on probes (throwaway dirs only, live tree untouched). The hooks-inheritance sub-question
is now **answered definitively**:

### Hooks inheritance: yes, but trust-gated — and the guard CAN see MCP-Codex

- `codex exec` **does** load and fire a project `.codex/hooks.json` PreToolUse hook, discovered
  from the **`--cd` workdir, not the process cwd** (probe: launched from the repo root with
  `--cd <scratch>`; only the scratch dir's hook fired, the repo's trusted guard hook never
  loaded).
- **Trust is the entire gate, keyed by absolute hooks.json path** in the user-level
  `~/.codex/config.toml` `[hooks.state]` (SHA256-pinned, per-hook enable/disable). Untrusted
  path → hooks **silently skipped** (no warning, no marker). With
  `--dangerously-bypass-hook-trust` → hooks fire, with loud per-run warnings. Note the flag's
  polarity: it **forces hooks ON** in untrusted dirs — the opposite of the wrapper bypass flags
  we worried about.
- **Payload is Claude-compatible**: `session_id`, `turn_id`, `transcript_path` (points into the
  vendor's sessions dir, so host inference works), `cwd`, `hook_event_name`, a model field,
  `permission_mode`, `tool_name` (`apply_patch` for edits), `tool_input`, `tool_use_id`. dydo's
  `HookInput` parses it as-is. **No `agent_id`/`agent_type`** → the guard routes a codex hook
  call into the Tier-1 lane with an unknown session id → stage-0 stranger, writes blocked.
- Consequence: **"isolation instead of surveillance" is no longer the only shape** — guard
  surveillance of MCP-Codex is achievable. A worktree slice is guarded iff the worktree carries
  a hooks.json AND (trust pre-seeded for that path, or the spawner passes the bypass-trust
  flag). Both are automatable by whatever creates the worktree.
- Wrinkle: the Windows **sandbox virtualizes cwd to a different path**
  (`…CodexSandboxOffline\.codex\.sandbox\cwd\…`), which breaks path-keyed trust even for a
  fully trusted repo. Sandbox posture and hook trust interact; needs one more probe.

### Identity: codex cannot claim — but it can impersonate (worse)

A live interactive codex session in the repo root tripped on `dydo agent claim auto`, then
`dydo agent role … --task …` **succeeded by silently binding to the already-claimed agent
Noah** via the CLI session-context fallback (`GuardCommand.cs:311`), clobbering the live
session's task binding and creating a stray task file. The "unattributed editor" objection
above is actually a **misattributed editor**. Filed as
[issue 0250](../issues/0250-session-context-fallback-lets-hookless-processes-impersonate-the-active-agent.md)
(high; balazs: test against this before v2.0.6 fully lands). Design direction that fell out:
MCP-Codex should ride a **worker-lane-style anonymous path** (like Tier-2 subagent calls) —
attributable via a payload marker, no claim, no state mutation; identity ceremony for inner
agents is explicitly not wanted.

### Experiment-design revisions implied

1. The MCP-Codex slice's worktree setup must **inject `.codex/hooks.json` + resolve trust**
   (seeded trust entry or bypass-trust flag at spawn) — the guard answer is now a precondition
   we know how to meet, not an open question.
2. Until issue 0250 is fixed, the experiment must assume the inner codex can mutate dydo agent
   state as the ambient agent — keep dydo CLI out of the inner agent's prompt, or fix 0250
   first.
3. `codex exec` runs `approval: never`; the "approvals surfaced back through MCP" question only
   exists for the native `codex mcp-server` path — probe there.

### Still open (next probes)

- Native `codex mcp-server` registration ergonomics vs wrappers: what `--cd`/sandbox/trust
  posture each passes to the inner exec; whether `-c` overrides at server registration can pin
  hook/sandbox posture fleet-wide. (Registration is the human's hand.)
- Sandbox path-virtualization × hook trust interplay.
- Cost/limit surface: exec turns bill the signed-in account (per-turn token counts are printed;
  probe runs were ~8.7k tokens each); visibility/limits for MCP-spawned turns unverified.
