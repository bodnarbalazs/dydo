# Guard tool calls

## Sub-features

`Commands/GuardCommand.cs` consumes actual host hook JSON. The initial journey exercises one
destructive-command refusal; it does not claim every path tier, shell dialect or nudge.

## How to get to it (user POV)

After `dydo init claude`, the host runs the installed PreToolUse hook before a shell action.
The driver reads that installed marker and sends a synthetic hook event to the real guard.

## Driving it with control-dydo

`drive --run RUN --journey guard` submits `rm -rf ./` as stdin data, expects a deny decision,
and checks the fixture sentinel survives. Capture the guard receipt and `guard-side-effects.json`.
The requested shell command is never launched.

## Gotchas

An exit of 0 alone does not mean allow: PreToolUse decisions are in JSON. This protocol drive
does not prove a real host honors the result. DYD-406's separate candidate must be integrated
and exercised before claiming its shell coverage; do not copy its unmerged implementation here.
