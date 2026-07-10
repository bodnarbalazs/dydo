---
title: Codex worker-role read-only capability not expressed - tools field is Claude-only; codex read-only roles need sandbox_mode mapping
id: 272
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Grace
found-by-vendor: claude
found-by-model: claude
date: 2026-07-10
---

# Codex worker-role read-only capability not expressed - tools field is Claude-only; codex read-only roles need sandbox_mode mapping

Split off from 0271 (the parse-unblock). Root fact (verified 2026-07-10 against the codex config
reference, learn.chatgpt.com/docs/config-file/config-reference): codex's agent `tools` field is a
`ToolsToml` STRUCT of codex-defined toggles ONLY — `view_image` (bool) and `web_search`
(bool|object) — NOT a list of file/shell tool names. Codex agents get apply_patch/shell/read
intrinsically; a worker's read-only-vs-read-write capability is governed by `sandbox_mode`
(`read-only` | `workspace-write`) and approval policy, NOT by a `tools` list. This is a different
model from Claude, where dydo encodes read-only by compiling the agent with no Edit/Write tool
(DR-024 §2 / DR-028: `writablePaths` shapes the compiled tool profile).

## The gap 0271 leaves open

0271's fix drops the invalid `tools = "read, grep, ..."` string so codex stops rejecting the role
files (the parse blocker). But dropping it means read-only roles (reviewer, inquisitor,
sprint-auditor, docs-writer-as-read-only, etc.) and read-write roles (code-writer) become
capability-identical in their `.codex/agents/*.toml` — both inherit the session's sandbox. So a
codex-spawned read-only reviewer subagent is NOT file-write-restricted the way its Claude twin is.
Not a security hole (the dydo guard hook still blocks off-limits writes globally, defense-in-depth,
and 0270's preflight enforces guard presence), but a capability-fidelity gap: the read-only
contract that holds on the Claude rail does not hold on the codex rail.

## Fix direction

Map dydo's read-only role determination (`IsReadOnlyRole`, SyncCommand.cs) to codex's mechanism:
emit `sandbox_mode = "read-only"` in the read-only roles' `.codex/agents/*.toml`; read-write roles
inherit or emit `workspace-write`. VERIFY LIVE whether a codex-spawned subagent actually honors a
stricter agent-file `sandbox_mode` than its session (codex may only widen, not narrow, or ignore
per-agent sandbox for spawned agents) — if it does not take, the fidelity has to come from how the
workflow dispatches the subagent, not the role file. This live-behavior question is why it is split
from 0271's mechanical parse-fix.

## Sequencing

NOT a 2.0.8 blocker (balazs directive 2026-07-10: 2.0.8 = codex-blocking bugs only; 0271's
parse-unblock is the blocker, this fidelity refinement is not). Natural **codex dogfood task**
post-2.0.8, or a P1 companion to the DR-037 codex-guard-adapter work.

## Related

- 0271 (parse-unblock, the 2.0.8 blocker this splits from).
- DR-024 §2 / DR-028 (tool-profile-from-writablePaths, the Claude-side mechanism).
- DR-037 (codex adapter follow-up).

## Resolution

(Filled when resolved)
