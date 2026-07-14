---
title: Guard never sees codex shell commands - PreToolUse matcher lists Claude tool names, codex uses shell_command (unguarded shell for every codex agent)
id: 295
area: backend
type: issue
severity: critical
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Guard never sees codex shell commands - PreToolUse matcher lists Claude tool names, codex uses shell_command (unguarded shell for every codex agent)

Empirically proven: a codex worker cat'd an off-limits agent state file and got the contents. The hook fires but the matcher omits codex's shell tool names, so no shell command from any codex agent has ever been guarded - only apply_patch edits were.

## Description

## Observed (empirically proven, codex-cli 0.144.1)

The `dydo guard` PreToolUse hook pipeline runs under codex (interactive AND `codex exec`), but it **never guards codex shell command execution**. An unattended `codex exec` worker in the DynaDocs repo ran `cat dydo/agents/Brian/state.md` — a universally off-limits path — and got the **full file contents, exit 0, no BLOCK**. The identical wrapped command through a properly-hooked (Claude) shell is hard-blocked (`BLOCKED: Command references off-limits path`).

## Root cause

`.codex/hooks.json` PreToolUse matcher lists **Claude Code tool names**:
`Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit|AskUserQuestion|apply_patch`

Codex names its shell tool `shell_command` (code-mode wrapper: `exec`; also `local_shell`, `unified_exec`). **None of these are in the matcher.** Proven via `RUST_LOG=codex=debug`:
```
hook: PreToolUse
event.name="codex.tool_decision" tool_name=shell_command decision=approved source=Config
hook: PreToolUse Completed
```
The hook fires but the tool name doesn't match, so `dydo guard` is never invoked for the call; the sandbox/approval policy (`source=Config`) decides alone. Only `apply_patch` (file edits) is matched and genuinely guarded.

## Impact — HIGH, and it affects TODAY's swarm, not just the future design

- Every codex agent (interactive dispatch too, same matcher) can run **arbitrary shell UNGUARDED**: read off-limits secrets/state files, `git stash` (guard-blocked for a reason — global stack), `rm`, destructive git, human-only commands — none of the universal off-limits / dangerous-bash / git-safety layers bind on the shell lane. Only their `apply_patch` writes were ever guarded.
- This silently invalidates the assumption, relied on all through the swarm, that "the guard will stop a codex worker from doing something destructive." It stops their file edits, not their shell.
- For the planned ephemeral-worker factory it is a hard blocker: unattended workers MUST be shell-guarded.

## Fix (two parts)

1. **Matcher:** add codex shell tool names to the PreToolUse matcher in `.codex/hooks.json`:
   `...|apply_patch|shell_command|exec|local_shell|unified_exec`
   Then codex shell calls route to `dydo guard` and the universal layers bind. NOTE: after editing `.codex/hooks.json`, its trust hash in `~/.codex/config.toml [hooks.state]` must be re-pinned or the whole hook is silently skipped as untrusted — `Services/DispatchPreflight.cs` already self-repairs this, confirm it covers this path.
2. **Worker-lane routing:** the codex hook payload carries NO `agent_id`/`agent_type`, so guarded calls currently land in the Tier-1 stage-0 "stranger" lane (`GuardCommand.cs:443`), which identity-blocks writes/searches — the OPPOSITE of the intended "ephemeral workers don't claim" posture. To get the anonymous-worker lane (`HandleWorkerCall`, `GuardCommand.cs:664` — applies off-limits + dangerous-bash, skips identity/RBAC/must-reads), an `agent_type` marker must be injected into the codex hook payload at dispatch/launch time. (Open item already noted in `dydo/project/backlog/codex-mcp-delegation-experiment.md`.)

## Caveats to design around
- Windows sandbox virtualizes cwd (`…/.codex/.sandbox/cwd/<hash>`), which can break path-keyed hook trust — a separate silent-skip path (see the backlog record).
- `--ignore-user-config` drops the trust table → untrusted hooks silently skipped. `--dangerously-bypass-hook-trust` forces hooks on.

## Acceptance
- A codex exec (and interactive) worker attempting an off-limits shell read/command in the repo is BLOCKED, same as a Claude agent.
- A dangerous-bash / git-stash / destructive-git shell command from a codex worker is blocked.
- Unattended workers route through the anonymous-worker lane (off-limits + dangerous-bash enforced; no claim/must-read demanded).
- Trust re-pins automatically after the matcher edit (no silent skip).
- A regression test pins the matcher includes the codex shell tool names.

Verified files: `.codex/hooks.json`, `Commands/GuardCommand.cs` (443, 529, 664, 1159-1189, 1318), `dydo/files-off-limits.md`, `dydo/project/backlog/codex-mcp-delegation-experiment.md`, `Services/DispatchPreflight.cs`.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)