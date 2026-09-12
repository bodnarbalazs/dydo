---
area: guides
type: guide
---

# Adding a dydo Command

A new command or required option changes a closed set of code, test, help, and reference surfaces.
`CommandDocConsistencyTests` discovers the System.CommandLine tree and keeps those surfaces aligned.

## Required surfaces

| Surface | Required change |
|---|---|
| `Program.cs` | Register the top-level command. |
| `Commands/<Name>Command.cs` | Define the command and handler behavior. |
| `Commands/HelpCommand.cs` | Include the command path in agent-facing help when appropriate. |
| `DynaDocs.Tests/Commands/CommandSmokeTests.cs` | Exercise the command factory. |
| `dydo/reference/dydo-commands.md` | Document each option and show required flags in examples. |
| `Templates/dydo-commands.template.md` | Keep the framework source aligned with the installed reference. |

Update focused behavior tests as required by the command's risk. The consistency suite checks command
discovery, help presence, option coverage, required example flags, template/reference parity, and factory
smoke coverage.

## Product-boundary check

Before adding a command, confirm the capability belongs inside dydo. Local documentation, validation,
native skill authoring, guard, configuration, and utility operations fit the product boundary.
Linear work management does not: do not add commands that create, update, poll, cache, provision, or
mirror Linear objects.

Transition-only commands must be labeled as historical migration compatibility in active docs and must
not be presented as the current work model.

## Generated and installed copies

A role is a plain `SKILL.md` folder under `.claude/skills/` and `.agents/skills/`; edit it directly.
Framework document templates under `Templates/` feed `dydo init` scaffolding; keep
`Templates/dydo-commands.template.md` and `dydo/reference/dydo-commands.md` aligned by hand.

Do not hand-edit the protected `dydo/index.md`, `dydo/files-off-limits.md` or `dydo.json`; the human
owns them and the guard blocks direct writes.

## Verification

Run the command's focused tests through the worktree-isolated runner, then the coverage gate:

```bash
py DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~CommandDocConsistencyTests
py DynaDocs.Tests/coverage/gap_check.py
```

Finish with `dydo check` and `git diff --check`.

## Related

- [Testing Strategy](./testing-strategy.md)
- [CLI Commands](../reference/dydo-commands.md)
- [Architecture](../understand/architecture.md)
