# DynaDocs (dydo)

A CLI tool for documentation validation and AI agent orchestration.

DynaDocs helps you maintain AI-traversable documentation with consistent structure, validated links, and coordinated multi-agent workflows.

## Installation

### npm (recommended for non-.NET users)

```bash
npm install -g dydo
```

### .NET Global Tool

```bash
dotnet tool install -g DynaDocs
```

### Direct Download

Download the binary for your platform from [GitHub Releases](https://github.com/bodnarbalazs/dydo/releases).

## Quick Start

```bash
# Initialize a new project with Claude Code integration
dydo init claude

# Validate your documentation
dydo check

# Auto-fix issues (naming, links, missing hub files)
dydo fix

# Claim an agent identity
dydo agent claim auto

# Set your role
dydo agent role code-writer --task my-feature
```

## What It Does

### Documentation Validation
- **Naming conventions** - Enforces kebab-case for files and folders
- **Link validation** - Ensures all internal links resolve correctly
- **Frontmatter** - Requires YAML frontmatter with area/type metadata
- **Hub files** - Every folder needs an `_index.md` with links to contents
- **Orphan detection** - Finds docs not reachable from the index

### Agent Orchestration
- **Multi-agent workflows** - Coordinate multiple AI agents on a project
- **Role-based permissions** - code-writer, reviewer, planner, tester, etc.
- **Self-review prevention** - Ensures different agents review code
- **Guard hooks** - Integrates with Claude Code to enforce permissions

## Documentation Structure

```
project/
├── dydo.json              # Configuration
├── CLAUDE.md              # AI entry point
└── dydo/
    ├── index.md           # Documentation root
    ├── understand/        # Domain concepts, architecture
    ├── guides/            # How-to guides
    ├── reference/         # API docs, specs
    ├── project/           # Decisions, pitfalls, changelog
    │   └── tasks/         # Cross-agent task handoff
    └── agents/            # Agent workspaces (gitignored)
```

## Commands

| Command | Description |
|---------|-------------|
| `dydo init <integration>` | Initialize project (claude, none) |
| `dydo check [path]` | Validate documentation |
| `dydo fix [path]` | Auto-fix issues |
| `dydo agent claim <name\|auto>` | Claim an agent identity |
| `dydo agent role <role>` | Set agent role |
| `dydo agent list` | List all agents |
| `dydo dispatch --role <role> --task <name>` | Hand off work |
| `dydo guard` | Check permissions (for hooks) |

## Agent Roles

| Role | Can Edit | Purpose |
|------|----------|---------|
| code-writer | src/**, tests/** | Implement features |
| reviewer | (read-only) | Review code |
| planner | tasks/**, agent workspace | Design implementation |
| tester | tests/**, pitfalls/** | Write tests, report bugs |
| docs-writer | dydo/** (except agents/) | Write documentation |
| co-thinker | decisions/**, agent workspace | Explore ideas |
| interviewer | agent workspace | Gather requirements |

## License

MIT
