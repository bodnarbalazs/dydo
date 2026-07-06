---
title: RBAC, off-limits, and dangerous-pattern checks bypassed for any bash chain containing a dydo subcommand
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: open
work-type: 
id: 155
type: issue
found-by: inquisition
date: 2026-05-01
---

# RBAC, off-limits, and dangerous-pattern checks bypassed for any bash chain containing a dydo subcommand
`GuardCommand.HandleBashCommand` routes any bash chain containing a `dydo` subcommand to `HandleDydoBashCommand`, which never runs `BashCommandAnalyzer.Analyze`, dangerous-pattern checks, or off-limits checks on the surrounding chain. Because the PreToolUse hook fires once per tool call, every other `&&`/`;`/`||` segment in the chain executes unchecked once the dydo segment is allowed.
## Description
`Commands/GuardCommand.HandleBashCommand` (lines 486-528) routes any bash command containing a `dydo` subcommand at a chain-segment position to `HandleDydoBashCommand` *before* dangerous-pattern checks, nudges, and full `BashCommandAnalyzer` analysis. `HandleDydoBashCommand` (lines 627-678) does not call `BashCommandAnalyzer.Analyze`, `CheckDangerousPatterns`, `CheckBashFileOperation`, or off-limits checks on the inner part of the chain. After at most a `CheckPendingState` (which is itself skipped for `IsDydoDispatchCommand` and `IsDydoWaitAnyForm`), it returns `ExitCodes.Success`.
The PreToolUse hook fires *once* per bash tool call, so any chained `&&`/`;`/`||` sub-commands run unchecked once the outer hook returns Success. RBAC, off-limits patterns, and the hardcoded dangerous-pattern detector are all silently bypassed.
The routing trigger is structural: `DydoCommandRegex` (`(?:^|[;&|]\s*)(?:\./)?dydo\s`) matches `dydo` anywhere in the chain, not just at the start. So even a chain like `rm -rf / && dydo whoami` routes through `HandleDydoBashCommand` and skips dangerous-pattern detection.
## Reproduction
Concrete bypass examples — none caught by guard once the chain contains a `dydo` subcommand:
- `dydo wait --cancel && rm -rf Commands/` — RBAC on `Commands/**` (read-only for most roles) is bypassed.
- `dydo wait --cancel && cat dydo/_system/secrets/x` — off-limits patterns from `OffLimitsService.IsPathOffLimits` are not consulted for inner ops.
- `dydo dispatch ... && rm -rf .git` — dangerous-pattern detector never runs.
- `dydo whoami && cat $env:USERPROFILE/.ssh/id_ed25519` — assuming general wait alive (normal post-claim state), `CheckPendingState` passes and the inner command runs unchecked.
- `rm -rf / && dydo whoami` — even with `dydo` last in the chain, the routing fires and bypasses the dangerous-pattern check.
A benign instance was exploited live during inquisition #0149: `dydo wait --cancel && sed -i '...' dydo/agents/Charlie/state.md` to drain a stuck unread. The same construct could write or delete files outside the agent's role-writable paths.
## Suggested fix paths
(Investigator should evaluate.)
- **A. Run `BashCommandAnalyzer.Analyze` on the full string in `HandleDydoBashCommand` after the dydo-specific checks**, exactly as `HandleNonDydoBash` does. Dydo-prefix routing should *add* behaviour (session storage, claim handling, pending-state bypass for waits/dispatch), not *remove* the security checks. This preserves chain-using workflows (e.g., the live #0149 workaround) while restoring RBAC/off-limits/danger-pattern coverage on the inner ops.
- **B. Reject `&&`/`;`/`||`/`|`/`$(...)`/`` `...` `` in commands routed to `HandleDydoBashCommand`.** More aggressive — would break the documented #0149 workaround chain. Acceptable only if #0149's root cause (see related) is fixed first so the workaround becomes unnecessary.
A is preferred because it restores defence-in-depth without changing user-visible behaviour for safe chains.
## Related
- `Commands/GuardCommand.cs:486-528` — `HandleBashCommand` routing.
- `Commands/GuardCommand.cs:627-678` — `HandleDydoBashCommand` (no inner-chain analysis).
- `Commands/GuardCommand.cs:1388-1389` — `DydoCommandRegex` (matches at any chain segment).
- `Commands/GuardCommand.cs:708-748` — `HandleNonDydoBash` (the contrast: runs full analysis).
- Inquisition: [wait-rearm-flood-deadlock](../inquisitions/wait-rearm-flood-deadlock.md) — surfaced this bypass during #0149 reproduction work.
- Issue [#0149](./0149-wait-rearm-gap-deadlocks-agent-when-3-plus-messages-stack-faster-than-agent-can-process.md) — the live workaround documented there exploits this bypass.