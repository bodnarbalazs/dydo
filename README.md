# DynaDocs (dydo)

Documentation-driven context and agent orchestration for AI coding assistants.

100% local, 100% under your control.

## Stop Doing Agent Work Yourself

Your time is the most precious resource in the equation. You should focus on your comparative advantage: deciding **what** should be done and **why** — articulating intent, making value choices, choosing direction. Everything that *can* be done by an agent *should* be.

Agents write code. Agents review code. Agents write tests. Agents write documentation. Agents coordinate other agents. The human is the last step, not the first reviewer. If it can be done by an agent, why waste your time on it?

DynaDocs makes this possible. It gives AI coding agents persistent memory (through documentation), enforced identity and permissions (through a guard hook), and multi-agent coordination (through dispatch, messaging, and orchestration). You describe what you want. Agents figure out the rest.

![Simple workflow: three agents collaborating on a task](https://raw.githubusercontent.com/bodnarbalazs/dydo/master/dydo/_assets/dydo_diagram_simple_workflow.svg)

### The amnesia problem

AI agents forget everything between sessions. DynaDocs solves this by making your project documentation the persistent memory. Think of it like Groundhog Day: the AI wakes up fresh, but you've left it a note explaining everything it needs to know. It reads the note, onboards itself, and gets to work.

### What you get

- **Documentation as memory** — Your docs are the source of truth; AI re-reads them each session
- **Self-onboarding** — Agents follow a documentation funnel, no manual context-setting
- **Role-based permissions** — Reviewer can't edit code, code-writer can't touch docs — enforced, not suggested
- **No self-review** — The agent that wrote the code cannot review it
- **Multi-agent orchestration** — Orchestrators coordinate swarms of agents across parallel tasks
- **Dispatch and messaging** — Agents hand off work, communicate results, and wait for responses
- **Worktree isolation** — Parallel agents work on separate git branches without conflicts
- **Issue tracking** — Lightweight issue management tied to inquisitions and reviews
- **Team support** — Each team member gets their own pool of agents
- **Platform-agnostic** — Works across AI tools (Claude Code, Cursor, etc.) and operating systems
- **Your process, your rules** — Modify templates, roles, and workflows to match how you work

---

## Installation

```bash
# npm (recommended)
npm install -g dydo

# if you have .NET installed
dotnet tool install -g dydo
```

**Note:** The setup will prompt you to set the `DYDO_HUMAN` environment variable. Agents use this to know which human they belong to.

## Quick Start

### 1. Set up dydo in your project

Run from your project's root directory:

```bash
# If you use Claude Code
dydo init claude

# If you use something else
dydo init none
```

This creates the `dydo/` folder structure, templates, and configures Claude Code hooks automatically.

### 2. Link your AI entry point

Add this to your `CLAUDE.md` (or equivalent for other AI tools):

```markdown
This project uses an agent orchestration framework (dydo).
Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
```

### 3. For non-Claude Code users

If you're using a different AI tool, wire up a hook that calls `dydo guard` before file edits:

```bash
# CLI mode (simpler)
dydo guard --action edit --path src/file.cs

# Or pipe JSON via stdin (for tools that send structured data)
echo '{"tool_name":"Edit","tool_input":{"file_path":"src/file.cs"}}' | dydo guard
```

Exit code `0` = allowed, `2` = blocked (reason in stderr).

### 4. Validate your documentation

```bash
dydo check    # Find issues
dydo fix      # Auto-fix what's possible
```

**Tip:** [Obsidian](https://obsidian.md) makes navigating the docs easier, but it converts links when you move files. Run `dydo fix` afterward.

### 5. Customize the templates

Edit templates in `dydo/_system/templates/` to fit your project. Fill out the `about.md` and modify the `coding-standards.md` to your taste.

You're ready to go. For best results, keep docs up to date and accurate to match your intent.

---

## How It Works

**Example prompt:** `Hey Adele, help me fix this bug in the auth service`

1. The agent reads `CLAUDE.md`, gets redirected to `dydo/index.md`
2. From `index.md`, it navigates to its workspace: `dydo/agents/Adele/workflow.md`
3. It claims its identity: `dydo agent claim Adele`
4. It reads the prompt, infers the appropriate role, and sets it: `dydo agent role code-writer --task auth-fix`
5. On every file operation, the `dydo guard` hook enforces permissions based on the current role
6. When done, it dispatches to a *different* agent for review — fresh eyes, enforced

The AI onboards itself by following the documentation funnel — you don't have to re-explain what's already documented. Permissions aren't suggestions; the hook blocks unauthorized edits.

For orchestrated work, the prompt includes `--inbox`, telling the agent to check its inbox for dispatched work items.

---

## Multi-Agent Orchestration

For complex work, an orchestrator agent coordinates multiple agents working in parallel:

![Multi-agent orchestration with worktrees, inquisitors, and judges](https://raw.githubusercontent.com/bodnarbalazs/dydo/master/dydo/_assets/dydo_diagram_complex_workflow.svg)

Key capabilities:

- **Dispatch chains** — orchestrator dispatches code-writer, code-writer dispatches reviewer, reviewer reports back
- **Worktree isolation** — `dispatch --worktree` gives each agent an isolated git branch
- **Dispatch queues** — `--queue` serializes terminal launches to avoid resource contention
- **Inquisition** — adversarial QA agents audit code quality and documentation coverage
- **Dispute resolution** — judge agents arbitrate when agents disagree

---

## Agent Roles

Nine roles, each with enforced permissions:

| Role | Purpose | Can Edit |
|------|---------|----------|
| **code-writer** | Implements features and fixes bugs | Source code, tests |
| **reviewer** | Reviews code for quality and correctness | Own workspace (read-only access to code) |
| **co-thinker** | Collaborates on design decisions and architecture | Decisions, own workspace |
| **planner** | Creates implementation plans and task breakdowns | Tasks, own workspace |
| **docs-writer** | Creates and maintains documentation | Documentation tree |
| **test-writer** | Writes and maintains test suites | Tests, pitfalls |
| **orchestrator** | Coordinates multi-agent workflows | Tasks, decisions, own workspace |
| **inquisitor** | Conducts adversarial QA and knowledge audits | Inquisition reports |
| **judge** | Arbitrates disputes between agents | Issues, own workspace |

Roles are data-driven — defined in `.role.json` files. Projects can add custom roles with `dydo roles create <name>`.

---

## Folder Structure

![DynaDocs folder structure](https://raw.githubusercontent.com/bodnarbalazs/dydo/master/dydo/_assets/dydo-diagram.svg)

```
project/
├── dydo.json                    # Configuration
├── CLAUDE.md                    # AI entry point
└── dydo/
    ├── index.md                 # Documentation root
    ├── understand/              # Domain concepts, architecture
    ├── guides/                  # How-to guides
    ├── reference/               # API docs, specs
    ├── project/                 # Decisions, issues, changelog
    ├── _system/templates/       # Customizable templates
    ├── _system/roles/           # Role definitions (.role.json)
    ├── _assets/                 # Images, diagrams
    └── agents/                  # Agent workspaces (gitignored)
```

---

## For Teams

Each team member gets their own pool of agents — no conflicts. Join an existing project with:

```bash
dydo init claude --join
```

---

## Self-Documentation

DynaDocs documents itself using its own system. Agents can learn about dydo by reading the `dydo/` folder in the [dydo GitHub repo](https://github.com/bodnarbalazs/dydo) — it's a living example of documentation-driven orchestration in action.

---

## Get Started

```bash
npm install -g dydo
cd your-project
dydo init claude
```

Then tell your AI to read `dydo/index.md`. That's it.

---

## Command Reference

**Note:** Agents call most of these commands themselves.

### Setup
| Command | Description |
|---------|-------------|
| `dydo init <integration>` | Initialize project (`claude`, `none`) |
| `dydo init <integration> --join` | Join existing project as new team member |
| `dydo whoami` | Show current agent identity |

### Documentation
| Command | Description |
|---------|-------------|
| `dydo check [path]` | Validate documentation |
| `dydo fix [path]` | Auto-fix issues |
| `dydo index [path]` | Regenerate index.md from structure |
| `dydo graph <file>` | Show graph connections for a file |
| `dydo graph stats [--top N]` | Show top docs by incoming links |

### Agent Lifecycle
| Command | Description |
|---------|-------------|
| `dydo agent claim <name\|auto>` | Claim an agent identity |
| `dydo agent release` | Release current agent |
| `dydo agent status [name]` | Show agent status |
| `dydo agent list [--free] [--all]` | List agents (default: current human's) |
| `dydo agent tree` | Show dispatch hierarchy of active agents |
| `dydo agent role <role> [--task X]` | Set role and permissions |

### Agent Management
| Command | Description |
|---------|-------------|
| `dydo agent new <name> <human>` | Create new agent |
| `dydo agent rename <old> <new>` | Rename an agent |
| `dydo agent remove <name>` | Remove agent from pool |
| `dydo agent reassign <name> <human>` | Reassign to different human |

### Workflow
| Command | Description |
|---------|-------------|
| `dydo dispatch --wait/--no-wait --role <role> --task <name>` | Hand off work to another agent |
| `dydo dispatch --worktree ...` | Dispatch into an isolated git worktree |
| `dydo dispatch --queue <name> ...` | Serialize launches via named queue |
| `dydo inbox list` | List agents with inbox items |
| `dydo inbox show` | Show current agent's inbox |
| `dydo inbox clear --all` | Archive processed items |

### Messaging
| Command | Description |
|---------|-------------|
| `dydo msg --to <agent> --body "..."` | Send message to another agent |
| `dydo msg --to <agent> --subject <task> --body "..."` | Message with task context |
| `dydo wait --task <name>` | Wait for task-specific message |
| `dydo wait --cancel` | Cancel active waits |

### Tasks
| Command | Description |
|---------|-------------|
| `dydo task create <name>` | Create a new task |
| `dydo task ready-for-review <name> --summary "..."` | Mark task ready for review |
| `dydo task approve <name>` / `--all` | Approve task(s) (human only) |
| `dydo task reject <name>` | Reject task (human only) |
| `dydo task list` | List tasks |
| `dydo task compact` | Compact audit snapshots |
| `dydo review complete <task>` | Complete a code review |

### Issues
| Command | Description |
|---------|-------------|
| `dydo issue create --title "..." --area <area> --severity <level>` | Create an issue |
| `dydo issue list [--area <area>] [--all]` | List issues |
| `dydo issue resolve <id> --summary "..."` | Resolve an issue |

### Inquisition
| Command | Description |
|---------|-------------|
| `dydo inquisition coverage` | Show inquisition coverage across areas |

### Roles
| Command | Description |
|---------|-------------|
| `dydo roles list` | List all role definitions |
| `dydo roles create <name>` | Scaffold a custom role |
| `dydo roles reset` | Regenerate base role files |
| `dydo validate` | Validate configuration and roles |

### Workspace
| Command | Description |
|---------|-------------|
| `dydo guard` | Check permissions (for hooks) |
| `dydo guard lift <agent>` | Temporarily lift guard restrictions |
| `dydo guard restore <agent>` | Restore guard restrictions |
| `dydo clean <agent>` | Clean agent workspace |
| `dydo workspace init` | Initialize agent workspaces |
| `dydo workspace check` | Verify workflow before session end |

### Audit
| Command | Description |
|---------|-------------|
| `dydo audit` | Generate activity replay visualization |
| `dydo audit --list` | List available sessions |
| `dydo audit --session <id>` | Show details for a session |
| `dydo audit compact [year]` | Compact audit snapshots |

### Template
| Command | Description |
|---------|-------------|
| `dydo template update` | Update framework templates and docs |
| `dydo template update --diff` | Preview changes without writing |

### Utility
| Command | Description |
|---------|-------------|
| `dydo completions <shell>` | Generate shell completions |
| `dydo version` | Display version |

---

### Limitations
Currently it's tested to work with Claude Code (hooks setup), but the principle should be the same for all coding agents.

## License

AGPL-3.0 — [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)

**Free to use, always.** You can use dydo as a tool on any project, including commercial ones. The AGPL obligations apply only if you modify or embed dydo's source code in your own software — for example, shipping dydo as part of a product you distribute or offer as a service.

For commercial licensing without AGPL obligations, [open a GitHub issue](https://github.com/bodnarbalazs/dydo/issues).
