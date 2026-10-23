---
area: general
type: changelog
date: 2026-10-23
---

# dydo 3.5.0 - Dispatch rule, decision skill and overnight mode

## Summary

Minor release adding overnight mode (a machine mode flag, a `dydo mode` command and a guard nudge
on it), a decision skill that answers "what would balazs do", a dispatch channel rule, formal
verification skills, and a rule that wizards never carry decisions. It also removes the dead dydo 2
audit trail.

## Added

- Overnight mode (DYD-351): the `overnight-mode` skill runs the chief of staff's muster, the
  standing orders and per-Project night orders every admiral obeys, the night watch, and the
  morning brief that ends it. `dydo mode show` lists the machine's mode flags and
  `dydo mode set <flag> <value>` stores one in a per-user file (`dydo/mode.json` under the local
  application data), so every project and agent on the machine reads the same values. One flag
  ships: `overnight`, `on` or `off`, default `off`.
- A built-in guard nudge on `dydo mode set` (DYD-351). An agent's first try is blocked with a
  reminder that only the chief of staff sets the mode. On the rerun the guard answers an `ask`
  decision only on a Claude Code hook that carries the new `--host claude` marker, in the
  `default`, `acceptEdits`, `plan` or `auto` permission modes; everywhere else (Codex,
  `bypassPermissions`, `dontAsk`, an unmarked hook) it denies and the user runs the command in his
  own terminal. The nudge lives in code, so `dydo.json` cannot drop or weaken it. The guard reads
  the command with the rules of the shell that runs it (bash, PowerShell, `cmd /c`), so quotes,
  here-strings, `$(...)` substitutions and wrappers such as `env` or `sudo` are seen, while a
  commit message that only mentions `dydo mode set` is not. `dydo init claude --join` rewrites an
  older Claude Code hook to carry the marker. The nudge stops accidents, not a deliberate bypass.
- The `what-would-balazs-do` decision skill and its tribunal (DYD-350, DR 054): a trigger map
  decides a weighty call or prepares it as a Question, with a ballot and a judge brief. Control-flow
  section 6.1 sends a call above the officer's say through it when the call is DR-grade or
  irreversible, or when the human is away. Only a call the tribunal decides while he is away stays
  unwired, for him to ratify or overturn; every other call gets a prepared Question wired to its
  waiters.
- The `create-formal-verification-skill` and `maintain-formal-verification-skill` skills
  (DYD-344), adapted from the cursor/plugins pstack at 5dba0b8d (MIT), registered in the skill
  READMEs, the control-flow table, the glossary and the third-party notices.
- A proposal for an overnight chief of staff, recorded in PR #153 and now marked `promoted` into
  the shipped overnight mode above (DYD-351).

## Changed

- The wizard skill bans decisions (DYD-343): settle each in conversation before authoring. A
  wizard script asks the human only for operational confirmations (y/N or Enter to continue) and
  captured values (a secret, an identifier, a path).
- A dispatch channel rule and a two-vendor first review round, with the reviewer counts and
  human-in-the-loop threads aligned to the working-tree contract (DYD-346).
- `dydo guard` accepts `--host claude`, which `dydo init` writes into the Claude Code hook. It is
  a marker, not a diagnostic argument: the call still reads its payload from stdin.
- The decision-record hub states when to write a record (all three of hard to reverse, surprising
  without context, a real trade-off) and documents the `ratification: pending` frontmatter for a
  `proposed` record a tribunal decided while the human was away (DYD-350).
- Align runtime and npm versions to 3.5.0 and permit publication only on the exact v3.5.0 tag.

## Removed

- The dead dydo 2 audit trail (DYD-340, PR #152): `dydo/_system/audit`, the audit-large test
  fixture, `GetAuditPath`, the audit-system reference page and the `_system/audit/` scan
  exclusion. A stale `_system/audit/` entry in a project's `dydo.json` is left alone by `check`
  and `fix`.

## Files Changed

`Commands/` (`ModeCommand.cs`, `GuardCommand.ModeSet.cs`, `GuardCommand.cs`,
`GuardCommand.Refusal.cs`, `InitCommand.cs`, `HelpCommand.cs`), `Models/HookInput.cs`, `Program.cs`,
`Services/` (`ModeState.cs`, `ShellReader.cs`, `DydoInvocationScanner.cs`, `ConfigFactory.cs`,
`ConfigService.cs`, `IConfigService.cs`), `Scaffold/dydo/` (guides, reference, decisions hub),
`dydo/guides/`, `dydo/reference/`, `dydo/understand/control-flow.md`,
`dydo/project/decisions/054-what-would-balazs-do-tribunal.md`,
`dydo/project/decisions/_decisions.md`,
`dydo/project/future-features/overnight-chief-of-staff.md`, `dydo/_system/audit/` (removed),
`dydo.json`, `skills/` (overnight-mode, what-would-balazs-do, the two formal verification skills,
wizard, to-issue, wayfinder, admiral, issue-captain, chief-of-staff, READMEs), `THIRD-PARTY-NOTICES.md`,
`npm/THIRD-PARTY-NOTICES.md`, `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/` (mode command, mode state, invocation scanner
in `Services/DydoInvocationScannerTests.cs`, guard mode-set and worker lane, init, fix, check,
validate, config, doc scanner, off-limits, command smoke, the `machine-mode.feature` scenarios and
steps, canonical skill assertions, coverage associations, the removed audit-large fixtures), `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `npm/test/install.test.cjs`, this entry and its
changelog hub.

## Related

- [dydo 3.4.1 - Guard refusals as a deny decision](../2026-10-18/dydo-3-4-1-guard-deny-decision.md)
