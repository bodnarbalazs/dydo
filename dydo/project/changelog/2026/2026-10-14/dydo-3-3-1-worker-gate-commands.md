---
area: general
type: changelog
date: 2026-10-14
---

# dydo 3.3.1 - Sub-agents run the dydo gates

## Summary

Patch release that lets sub-agents run the `dydo` gate and read-only commands their work needs.
The guard blocked every `dydo` shell command from a call carrying `agent_id`, a rule from the
dydo 2.0 worker model. In dydo 3, Issue Captains and their crew are native sub-agents, and
`dydo check` and `dydo fix` are documentation gates they must run themselves.

## Changed

- The guard's worker lane allows `dydo check`, `fix`, `gap-check`, `graph`, `validate`, `help`,
  `version` and the root `--help`/`--version` flags from sub-agents.
- `init`, `index`, `guard`, `map`, `completions`, `_complete`, and any unknown or retired subcommand stay blocked
  for sub-agents. The top-level session is unaffected.
- The guard checks every `dydo` invocation in a compound command. It joins backslash and
  PowerShell-backtick line continuations, splits on `;`, `&`, `|` and newlines, and checks every
  `dydo` word followed by arguments wherever it stands: bare, quoted or path-qualified, behind a
  wrapper such as `env` or `cmd /c`, and nested in `$( )`, backticks, `<( )` or PowerShell `( )`.
  Flags before the subcommand are skipped. `dydo check && dydo init` and `dydo check $(dydo init)`
  are blocked, and so is plain text such as `echo dydo sync`.
- The block message names the allowed commands and the one it refused.
- Align the runtime and npm package versions to 3.3.1, point NuGet release notes to this entry
  and permit publication only on the exact v3.3.1 tag.

## Files Changed

`Commands/GuardCommand.cs`, `DynaDocs.Tests/Integration/GuardWorkerLaneTests.cs`,
`dydo/understand/guard-system.md`, `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, `npm/test/install.test.cjs`, this entry and
`dydo/project/changelog/_changelog.md`.

## Release Status

This entry records preparation, not publication. After human merge, push the v3.3.1 tag and confirm
the NuGet and npm 3.3.1 packages before reporting the release available.

## Related

- [Guard System](../../../../understand/guard-system.md)
- [dydo 3.3.0 - Working specs and communication](./dydo-3-3-0-working-specs-and-communication-skills.md)
