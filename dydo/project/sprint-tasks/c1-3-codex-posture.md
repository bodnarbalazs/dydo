---
title: c1-3 Configured Codex Launch Posture
blocked-by:
due:
needs-human: false
priority: High
sprint: c1-codex-adoption
status: ready
work-type: feature
area: backend
type: context
---

# c1-3 Configured Codex Launch Posture

Issue 0253: the launcher emits bare `codex "<prompt>"` (`TerminalLauncher.GetCodexCommand`,
TerminalLauncher.cs:166-170), so codex defaults to its most restrictive interactive mode and the
human hand-approves every action. Posture fixed in co-think (balazs 2026-07-09):
**`--sandbox workspace-write --ask-for-approval on-request`** as the config-surfaced default —
the sandbox is the enforcement boundary, prompts only to exceed it; explicitly NOT the
dangerous-bypass flag. The dydo guard hook remains project-boundary defense-in-depth.

## Behavior

- Codex launch (and resume — both paths through the launcher) carries the configured posture
  flags. Verified flag surface (official codex CLI reference, 2026-07-09; re-verify at
  implementation like the 0231 fix):
  `--ask-for-approval {untrusted|on-request|never}` (`on-failure` DEPRECATED — never emit),
  `--sandbox {read-only|workspace-write|danger-full-access}`.
- Config: `dispatch.codex` section (home: `Models/DispatchConfig.cs`, the launch-behavior bag) —
  `sandbox` and `approvalPolicy` string enums, validated against the accepted values; shipped
  defaults `workspace-write` / `on-request` (`Services/ConfigFactory.cs`). Absent section =
  shipped defaults, not bare launch.
- **The dangerous-bypass flag (`--dangerously-bypass-approvals-and-sandbox` / `--yolo`) is not a
  config value and is never emitted.** Unknown/invalid posture values fail config validation
  with the accepted list.
- Windows sandbox prerequisite (the smoke's missing `codex-windows-sandbox-setup.exe`):
  investigate what `workspace-write` requires on Windows, document the setup in
  `dydo/reference/configuration.md` (and wherever codex init is documented). The dispatch-time
  *check* is c1-4's; this slice supplies the documented setup instruction c1-4's error points at.

## Files

- `Services/TerminalLauncher.cs` — `GetCodexCommand` (166-170), `GetBareLaunchCommand` (120-124),
  Linux table strings (217-234), resume path (`ResumeArgumentToken`, 143-144): posture flags from
  config on every codex command line.
- `Services/WindowsTerminalLauncher.cs` — argument assembly (GetArguments 22-89) carries the
  flags through quoting intact.
- `Models/DispatchConfig.cs` — `codex { sandbox, approvalPolicy }` + validation.
- `Services/ConfigFactory.cs` — shipped defaults (file then handed to c1-6).
- Tests: `DynaDocs.Tests/Services/TerminalLauncherTests.cs` (+ Windows launcher tests) — assert
  the EXACT emitted command line per platform, defaults, config override, invalid-value
  rejection, and the never-emit-bypass invariant.
- Docs: `dydo/reference/configuration.md` posture keys + Windows sandbox setup note (+ template
  if clone-synced — grep before editing). No new command/flag → 6-surface rule not triggered;
  `CommandDocConsistencyTests` should stay green.

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

Parallel-safe with c1-1/c1-4/c1-5. **Blocks c1-6** (shared `Services/ConfigFactory.cs` and
`configuration.md`).

## Success criteria

A dispatched codex session runs shell commands and dydo CLI calls inside the workspace without
per-action human approval, under sandbox enforcement — no yolo flag anywhere in the codebase.
Issue 0253 resolved. Suite green.
