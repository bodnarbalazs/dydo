# Transition preflight

The already-invoked chief of staff prepares startup and shutdown while the user is present.
Read `dydo mode show` and record the observed value. Confirm the agreed work and stop line before
activation. The transition commands are:

```text
Start: dydo mode set overnight on
End:   dydo mode set overnight off
```

## At either transition

Run the applicable command only as the chief of staff readying overnight mode or ending it;
otherwise stop. On Claude, Codex, T3 and direct guard invocation, the guard stops an attempt
with that reminder unless a pass-through marker is already armed. If the reminder applies to
you, retry the same command once. Markers are global and have no expiry: an earlier caller may
have armed the same command, so your next attempt may pass immediately. The guard keys the
warning by the full command with whitespace runs normalized. A different command,
including the opposite transition, receives its own warning. The retry passes the warning;
ordinary hard blocks still apply on every attempt. This nudge prevents accidents. It does not
authenticate your role or prove human approval.

Wait for `dydo mode set` to return its result. Only then run `dydo mode show` in a separate later
tool call, never in parallel with the setter. Require the requested value before announcing
activation or shutdown. A warning, retry, successful setter exit, schedule or message establishes
no transition by itself. Capture the action, result and fresh readback
separately. If the value already matched before execution, report it as already on/off rather
than claiming a new transition.

On any unexpected refusal, execution failure, missing readback or mismatched value, report the
transition incomplete with the actual result and current observed state; if the readback failed,
say the state is unknown. Preserve the existing state and evidence. Return the blocker to the
responsible captain or user with the exact action needed. Use `wizard` for terminal help when
needed. Resolve the blocker before proceeding; every recovery still needs fresh readback.
