---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Validate reports a Claude project without the guard (unreleased)

## Summary

`dydo validate` now warns when a project records the Claude integration but its committed
`.claude/settings.json` does not run the dydo guard
([DYD-501](https://linear.app/bodnar-balazs/issue/DYD-501)). A project could have its Codex hook
wired while the Claude settings were missing or had lost the hook, and nothing reported it, so
every guard nudge silently skipped Claude agents there.

## Changed

- With `integrations.claude: true`, validate checks `.claude/settings.json` for a `PreToolUse` hook
  whose `command` is exactly `dydo guard --host claude`, whatever its matcher
  ([CLI Commands](../../../../reference/dydo-commands.md#dydo-validate)). A missing file, a file
  without that hook, a guard only in `settings.local.json` or a bare `dydo guard` gets one warning
  naming the fix, `dydo init claude --join`. A file that cannot be read gets a warning to make it
  readable first. A file that is not valid JSON or not an object, or whose `hooks` is not an object
  or `hooks.PreToolUse` not an array, gets a warning to fix the JSON first, since `--join` refuses
  such a file. The warning alone keeps exit `0`.
- The guard's daily validation shows the same warning and still allows the tool call.
- Init and validate read the hook command and the `--host` value from one source,
  `Services/ClaudeGuardWiring.cs`; what init writes is unchanged.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Services/ClaudeGuardWiring.cs`, `Services/ValidationService.cs`, `Commands/InitCommand.cs`,
`Commands/GuardCommand.ModeSet.cs`, `DynaDocs.Tests/Services/ValidationServiceTests.cs`,
`DynaDocs.Tests/Commands/ValidateCommandTests.cs`, `DynaDocs.Tests/Integration/ClaudeGuardWiringTests.cs`,
`DynaDocs.Tests/Features/claude-guard-wiring.feature`, `DynaDocs.Tests/Steps/ClaudeGuardWiringSteps.cs`,
`DynaDocs.Tests/coverage/test-associations.json`, `dydo/reference/dydo-commands.md`,
`Scaffold/dydo/reference/dydo-commands.md`, and this entry.

## Related

- [dydo 3.6.0 - PR-ready check allows lines without gh](./dyd-515-pr-ready-without-gh.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
