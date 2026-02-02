# dydo

DynaDocs CLI - A platform-agnostic AI orchestration and context-management framework.

100% local, 100% under your control.

## Installation

```bash
npm install -g dydo
```

## Quick Start

```bash
# Initialize in your project (creates dydo/ folder structure)
dydo init claude

# Validate documentation
dydo check

# Auto-fix issues
dydo fix
```

## Workflow Flags

Use these flags in your prompts to set the agent workflow:

| Flag | Workflow |
|------|----------|
| `--feature` | Interview → Plan → Code → Review |
| `--task` | Plan → Code → Review |
| `--quick` | Code only (simple changes) |
| `--think` | Co-thinker mode |
| `--review` | Reviewer mode |
| `--docs` | Docs-writer mode |
| `--test` | Tester mode |

## Key Commands

| Command | Description |
|---------|-------------|
| `dydo init <integration>` | Initialize project (`claude`, `none`) |
| `dydo check` | Validate documentation |
| `dydo fix` | Auto-fix issues |
| `dydo agent claim auto` | Claim an agent identity |
| `dydo agent role <role>` | Set role and permissions |
| `dydo whoami` | Show current agent identity |

## Documentation & Details

For full documentation, architecture diagrams, and detailed command reference:

**[github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)**

## Alternative Installation

If you have .NET installed:

```bash
dotnet tool install -g DynaDocs
```

## License

MIT
