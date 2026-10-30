# Approval preflight

The already-invoked chief of staff does this while the user is present, before muster work or
shutdown. Read `dydo mode show` and record the observed value. Identify the actual host and current
permission mode from the host session, then inspect the installed hook configuration read-only.

## Choose the route early

Claude Code is approval-capable only through the hook `dydo init` installed, invoking
`dydo guard --host claude`, in `default`, `acceptEdits`, `plan` or `auto` permission mode. Verify
that installed PreToolUse hook matches the shell tool being used. This is capability discovery
from the known host and configuration, not proof of a functioning approval UI. An inherited
`CLAUDECODE` environment variable does not identify the host.

Codex, an unknown host, a missing hook marker, `bypassPermissions`, `dontAsk` or uncertain
capability takes the user's own terminal route. Codex parsing an `ask` result is not enforced
approval. Show both exact commands now, before the user leaves:

```text
Start: dydo mode set overnight on
End:   dydo mode set overnight off
```

The user executes the applicable command in their own terminal at the transition. Permission or
intent alone is not execution. Use `wizard` if the user needs terminal help; return to the
calling procedure for an independent `dydo mode show` readback.

## At the transition

On the supported Claude route, an authorized first attempt receives the chief-of-staff nudge;
rerun the same command once to request actual user approval. A denied approval, refusal or
unexpected result stops this transition. Report the current readback and offer the exact
human-terminal command. Never retry until a restriction disappears, spoof a host marker, change
permission modes or weaken the guard. On the unsupported route, ask for user execution directly
instead of discovering the refusal after a farewell.

After either route, the caller verifies the requested value with fresh `dydo mode show` output
before announcing success. Capture the action, result and readback separately from the schedule
and messages. If the value already matches, report it as already on/off rather than claiming a
new transition. If readback fails or disagrees, activation/shutdown remains incomplete.

## When product support is needed

Preserve the refusal, actual host/version, installed hook command, permission mode and readback.
The bounded resolution is early terminal fallback; the guard's refusal is deliberate security.
If native Codex approval is needed, return a product/security decision to the ticket-captain:
retain terminal execution, or commission an adapter whose actual acceptance and rejection are
proved in that host before changing policy. Synthetic hook JSON is protocol evidence only.
No blanket approval, environment trick or marker change is a support solution.
