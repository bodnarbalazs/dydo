---
area: general
type: changelog
date: 2026-10-18
---

# dydo 3.4.1 - Guard refusals as a deny decision

## Summary

Patch release closing a fail-open: on Windows, Codex let every tool call the guard refused run anyway.

## Fixed

- Codex 0.163 runs a command hook through its session shell,
  `powershell|pwsh -NoProfile -Command "dydo guard"`. Windows PowerShell 5.1 and pwsh 7 both turn
  a native exit 2 into exit 1 under `-Command`, and Codex blocks only on exit 2: exit 1 is a failed
  hook, so the refused tool ran. Every guard refusal under Codex on Windows failed open (LC-1021).
- A `dydo guard` PreToolUse refusal now exits 0 and writes exactly one object to stdout,
  `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}`.
  Codex and Claude Code both block on it. The reason carries everything the call wrote to stderr:
  the `BLOCKED:` line, its follow-up lines and any `WARNING:` lines. That text still goes to stderr.
  Non-ASCII is escaped, so PowerShell re-encoding stdout cannot break the object. Every refusal
  goes through this one path, including a payload with no `session_id` and a guard that cannot
  initialize.
- An allowed call still exits 0 with empty stdout. The CLI-argument mode (`--action`, `--path`,
  `--command`), a payload naming another `hook_event_name`, and the `--stop` hook keep their exit
  codes.
- Align runtime and npm versions to 3.4.1 and permit publication only on the exact v3.4.1 tag.

## Files Changed

`Commands/GuardCommand.cs`, `Commands/GuardCommand.Refusal.cs`, `dydo/understand/guard-system.md`,
`dydo/reference/configuration.md`, `dydo/reference/dydo-commands.md`, `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/Integration/`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`,
`npm/test/install.test.cjs`, this entry and its changelog hub.

## Related

- [dydo 3.4.0 - New-file nudges and --root](../2026-10-17/dydo-3-4-0-new-file-nudges-and-root.md)
