---
id: 156
area: project
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-01
---

# Live workaround for #0149 deadlock is misexplained — works via #0155 bash-chain bypass, not the backgrounded wait

## Description

## Description

Issue [#0149](./0149-wait-rearm-gap-deadlocks-agent-when-3-plus-messages-stack-faster-than-agent-can-process.md) and Noah's onboarding messages describe the live workaround as:

> "The parenthesized backgrounded wait keeps a fresh wait alive in shell-bg long enough for the PreToolUse guard to see Listening true. Your command runs in that window."

This explanation is wrong. The workaround works because of the bash-chain bypass tracked in #0155, not because the backgrounded wait stays alive long enough.

## Trace

For `dydo wait --cancel && (dydo wait &) && sleep 3 && dydo inbox show`:

1. The PreToolUse hook fires *once* on the outer bash command.
2. `IsDydoCommand` matches the leading `dydo` → routed to `HandleDydoBashCommand`.
3. `IsDydoWaitAnyForm` matches the leading `dydo wait --cancel` → `CheckPendingState` skipped.
4. `HandleDydoBashCommand` returns `ExitCodes.Success`. **No further hook checks fire on the inner chain.**
5. The shell runs the entire chain. By the time `dydo inbox show` runs, the spawned background wait has typically already fired and exited (because the unread is in the set and the wait has no initial sleep). The inner `dydo inbox show` runs anyway because the hook is not invoked for it — bash gets one allow/deny per tool call, not per chained sub-command.

Verifying corollary: `dydo wait --cancel && dydo inbox show` (no spawned wait, no sleep) works identically.

## Why this matters

- If issue #0155 (the bash-chain bypass) is fixed, the documented workaround stops working — and the docs/comments around it remain misleading until updated.
- If issue #0149 (the wait re-arm deadlock root cause) is fixed, the workaround becomes unnecessary and should be removed from the docs entirely.
- Until both are addressed, agents inheriting the workaround pattern will form an incorrect mental model of how the guard works, which compounds when they later read `Commands/WaitCommand.cs:104-108` (see issue for that drift in the related list).

## Suggested fix

When resolving #0149 and/or #0155, also revise:
- The "Description" / "Resolution" sections of issue #0149 to either remove the workaround (if #0149 is fixed and it's no longer needed) or accurately attribute the mechanism to #0155.
- Noah's onboarding message template (wherever the workaround is documented for new agents) — same revision.
- The inline comment in `Commands/WaitCommand.cs:104-108` (also tracked in its own low-severity drift issue).

## Related

- Issue [#0149](./0149-wait-rearm-gap-deadlocks-agent-when-3-plus-messages-stack-faster-than-agent-can-process.md) — root cause; the issue itself contains the misleading explanation.
- Issue #0155 — the bash-chain bypass that actually makes the workaround function.
- Inquisition: [wait-rearm-flood-deadlock](../inquisitions/wait-rearm-flood-deadlock.md) (Finding #4) — the trace that established this.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)