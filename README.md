# DynaDocs (dydo)

A platform-agnostic AI orchestration and context-management framework in a cli tool.

## The problem

AI code editors and generators need persistence.
Without it each session starts with fresh context and the agent has to perform it's task with the additional context given during the prompt about the project itself and not the task and the arbitrary context it may gather during the exploration of the project.

Claude Code and Cursor don't have memory built in, and for tools which do like Windsurf and Antigravity which do have it in some form you don't control it.

## The solution

DynaDocs, short for Dynamic Documentation takes the approach of combining an agent-friendly documentation format with a CLI tool for deterministic rule enforcement and framework management.

## How does it work?

You direct the agent you're working with to the index.md file under dydo (through CLAUDE.md or equivalent), then from the index.md it goes through an onboarding process where it learns about the framework it's operating in and how to use it.

Based on it's prompt it may be given an identity or a task and it self-assigns to next onboarding file to learn more about how to perform that task.

### Example:

Prompt: Hey Adele, could you help me plan and implement the authentication? --feature

0. The agent gets redirected to index.md from the CLAUDE.md or it's equivalent.
1. In index.md it gets redirected to it's tailored onboarding file, a workflow.md under it's sandbox folder
2. In the workflow.md it learns it has to run 'dydo claim Adele' to claim it's identity, the agent-state.md gets updated in the dydo/agents/Adele folder, it will include the terminal's process id and the agent mode

On each agent action the command 'dydo guard' gets executed by a hook (which is set up automatically). This command will enforce the agent's permissions by looking up the registered process id in the agent-state.md files.

Note: Agents are made to comply by the dydo guard, they won't be able to make edits to the code unless they have claimed their identity AND it's set to code-writer mode.

Back to our example:

3. Adele claims it's identity and learns that the --feature flag means this workflow: interview → plan → code → review, so it sets itself to interview mode by running 'dydo agent role interviewer --task <task-name>'

Note: If the --feature flag is not included the agent will try to guess which workflow does the prompt suggest, it is instructed to ask if unsure. If the agent is not named by the prompt it will run 'dydo claim auto' which will give it an agent identity which belongs is assigned to the user (based on environment variable) and is free (not assigned elsewhere).

4. Adele starts to explore the documentation by reading the About.md and Architecture.md to get a birds-eye view of the project. Then it will search for certain terms like auth, security, it will read relevant files, and it will determine whether the links in those files point to relevant docs and if so read those too. Maybe it will use 'dydo graph <file>' to list the the files which point to the given file as well. It will then explore the codebase.

5. Adele now has enough relevant context to ask some smart questions and fill in the gaps, so the user's intent with the given feature is accurately captured. After the questions Adele will document some important decisions under dydo/project/decisions like that we've decided to go with JWT based authentication, and list the reasons for the decision, this is the kind of information which would be useful context in the future, but is usually lost.




























------

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
