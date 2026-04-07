---
area: reference
type: reference
---

# DynaDocs (dydo)

Documentation-driven context and agent orchestration for AI coding assistants.

100% local, 100% under your control.

**Built for Claude Code.** DynaDocs uses Claude Code's `PreToolUse` hook system for guard enforcement. Support for other AI coding tools may come in the future, but right now this is designed for and tested with Claude Code.

## Stop Doing Agent Work Yourself

Your time is the most precious resource in the equation. You should focus on your comparative advantage: deciding **what** should be done and **why** — articulating intent, making value choices, choosing direction. Everything that *can* be done by an agent *should* be.

Agents write code. Agents review code. Agents write tests. Agents write documentation. Agents coordinate other agents. The human is the last step, not the first reviewer. If it can be done by an agent, why waste your time on it?

DynaDocs makes this possible. It gives AI coding agents persistent memory (through documentation), enforced identity and permissions (through a guard hook), and multi-agent coordination (through dispatch, messaging, and orchestration). You describe what you want. Agents figure out the rest.

### The context problem

AI coding tools have memory features — Claude Code has CLAUDE.md and persistent memory, others are adding similar capabilities. But this memory is unstructured, opaque, and not under your control. You can't organize it, version it, or enforce who reads what.

DynaDocs gives you explicit, structured control over project context. Your documentation is the versioned, human-readable source of truth. Agents onboard themselves each session by reading it. You decide what's documented, how it's organized, and what each role needs to know.

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
- **Custom nudges** — Project-specific guardrails: regex patterns that block or warn agents with custom messages
- **Your process, your rules** — Modify templates, roles, and workflows to match how you work

---

## Installation

```bash
# npm (recommended)
npm install -g dydo

# if you have .NET installed
dotnet tool install -g dydo
```

**Note:** Set the `DYDO_HUMAN` environment variable so agents know who they belong to:

```powershell
# Windows (PowerShell)
[Environment]::SetEnvironmentVariable("DYDO_HUMAN", "YourName", "User")
```

```bash
# macOS / Linux (add to ~/.bashrc or ~/.zshrc)
export DYDO_HUMAN="YourName"
```

### Terminal Compatibility

Multi-agent dispatch opens new terminal tabs/windows. Supported terminals:

- **Windows:** Windows Terminal (Windows 11)
- **macOS:** iTerm2 recommended

## Quick Start

### 1. Set up dydo in your project

Run from your project's root directory:

```bash
dydo init claude
```

This creates the `dydo/` folder structure, templates, and configures Claude Code hooks automatically.

### 2. Link your AI entry point

Add this to your `CLAUDE.md`:

```markdown
This project uses an agent orchestration framework (dydo).
Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
```

### 3. Validate your documentation

Dydo expects a certain format — relative links, frontmatter, consistent structure. Run these periodically:

```bash
dydo check    # Find issues
dydo fix      # Auto-fix what's possible
```

**Tip:** [Obsidian](https://obsidian.md) makes navigating the docs easier, but it converts links when you move files. Run `dydo fix` afterward. The fix command also generates missing hub files and folder meta files.

### 4. Configure paths and roles

Edit `dydo.json` to set your project's source and test paths — not every project uses the same folder conventions. Role permissions reference these paths, so agents know where they can write.

You can also customize roles in `dydo/_system/roles/` or create entirely new ones with `dydo roles create <name>`.

### 5. Customize agent workflows

Two options for customizing what agents read and do:

- **Template additions** (recommended) — drop markdown files in `dydo/_system/template-additions/`. Templates have `{{include:name}}` hooks at natural extension points. (You can also add your own.) Your additions survive `dydo template update`.
- **Edit templates directly** — modify files in `dydo/_system/templates/`. More flexible, but `dydo template update` won't update the edited files.

Fill out `about.md` with your project context and adjust `coding-standards.md` to your conventions — agents read these during onboarding.

**Tip:** If you want to customize roles, custom nudges (regex pattern with warn or block behaviour with custom message), or something advanced in general, don't write the files manually. Find out what you want with a co-thinker, direct them to DynaDocs's dydo folder on github to learn a bit about the system, lift the guard for them with `dydo guard lift <agent name> 5` and have them do it.
Then run `dydo validate` to make sure it works.

#### Template Addition Details

Templates ship with `{{include:name}}` tags at natural extension points. These resolve to markdown files in `dydo/_system/template-additions/`.

**Shipped hooks:**

| Tag | Template | Position |
|-----|----------|----------|
| `{{include:extra-must-reads}}` | All modes | After must-reads list |
| `{{include:extra-verify}}` | code-writer | After verify step |
| `{{include:extra-review-steps}}` | reviewer | After "Run tests" step |
| `{{include:extra-review-checklist}}` | reviewer | End of review checklist |

Any `{{include:whatever}}` works — not limited to shipped hooks.

You're ready to go. Keep docs up to date and accurate to match your intent — they're the memory your agents rely on.

> **Tip: Agent-driven test coverage.** DynaDocs doesn't ship test tooling, but the [dydo repo](https://github.com/bodnarbalazs/dydo) includes two Python scripts you can copy into your project and adapt: `run_tests.py` runs `dotnet test` in a temporary git worktree, avoiding DLL lock contention when multiple agents test concurrently. `gap_check.py` builds on it — runs tests, collects Cobertura coverage, and checks every source module against tier-based thresholds (line coverage, branch coverage, CRAP score). Together they give agents a fast feedback loop on test quality without manual intervention.

---

## How It Works

**Example prompt:** `Hey Adele, help me fix this bug in the auth service`

1. The agent reads `CLAUDE.md`, gets redirected to `dydo/index.md`
2. From `index.md`, it navigates to its workspace: `dydo/agents/Adele/workflow.md`
3. It claims its identity: `dydo agent claim Adele`
4. It reads the prompt, infers the appropriate role, and sets it: `dydo agent role code-writer --task auth-fix`
5. On every file operation, the `dydo guard` hook enforces permissions based on the current role
6. When done, it dispatches to a *different* agent for review — fresh eyes, enforced

**What's happening:** The AI onboards itself by following the documentation funnel — you don't have to re-explain what's already documented. Permissions aren't suggestions; the hook blocks unauthorized edits.

### The `--inbox` flag

For orchestrated work — where an agent is dispatched by another agent — the prompt includes `--inbox`. This tells the agent to check its inbox (`dydo inbox show`) for dispatched work items that contain the role, task, and brief. Everything else is inferred from the prompt.

---

## Multi-Agent Orchestration

For complex work, an orchestrator agent coordinates multiple agents working in parallel. Orchestrators dispatch agents with specific roles and tasks, monitor progress via `dydo wait`, and coordinate the feedback loop. Agents communicate through `dydo msg` and hand off work through the dispatch system.

Key capabilities:

- **Dispatch chains** — orchestrator dispatches code-writer, code-writer dispatches reviewer, reviewer reports back
- **Worktree isolation** — `dispatch --worktree` gives each agent an isolated git branch
- **Dispatch queues** — `--queue` serializes terminal launches to avoid resource contention
- **Inquisition** — adversarial QA agents audit code quality and documentation coverage
- **Dispute resolution** — judge agents review inquisitions and help evaluate evidence

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
| **judge** | Evaluates inquisition reports and arbitrates disputes | Issues, own workspace |

Roles are data-driven — defined in `.role.json` files. Projects can add custom roles with `dydo roles create <name>`.

---

## Folder Structure

![DynaDocs folder structure](../_assets/dydo-diagram.svg)

```
project/
├── dydo.json                    # Configuration
├── CLAUDE.md                    # AI entry point
└── dydo/
    ├── index.md                 # Documentation root
    ├── welcome.md               # Human entry point
    ├── glossary.md              # Project glossary
    ├── files-off-limits.md      # Security boundaries
    │
    ├── understand/              # Domain concepts, architecture
    │   ├── _index.md            # Hub file (auto-generated)
    │   ├── about.md             # Project context
    │   └── architecture.md      # Architecture overview
    │
    ├── guides/                  # How-to guides
    │   ├── _index.md            # Hub file (auto-generated)
    │   ├── coding-standards.md  # Development standards
    │   └── how-to-use-docs.md   # Documentation usage
    │
    ├── reference/               # API docs, specs
    │   ├── _index.md            # Hub file (auto-generated)
    │   ├── writing-docs.md      # Documentation guide
    │   └── about-dynadocs.md    # About DynaDocs
    │
    ├── project/                 # Decisions, pitfalls, changelog
    │   ├── _index.md            # Hub file (auto-generated)
    │   ├── tasks/               # Task tracking
    │   ├── decisions/           # Decision records
    │   ├── changelog/           # Change history
    │   ├── issues/              # Issue tracker
    │   ├── inquisitions/        # Inquisition reports
    │   ├── pitfalls/            # Known issues
    │   └── future-features/     # Feature proposals
    │
    ├── _system/templates/       # Customizable templates
    ├── _system/template-additions/  # Template extension points
    ├── _system/roles/           # Role definitions (.role.json)
    ├── _assets/                 # Images, diagrams
    └── agents/                  # Agent workspaces (gitignored)
        └── [Adele, Brian, ...]  # Per-agent folders
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

## Command Reference

**Note:** Agents call most of these commands themselves.
Commands frequently used by humans are **bold**.
Commands meant to be called only by agents are *italic*.

### Setup
| Command | Description |
|---------|-------------|
| `dydo init <integration>` | Initialize project (`claude`, `none`) |
| `dydo init <integration> --join` | Join existing project as new team member |
| *`dydo whoami`* | *Show current agent identity* |

### Documentation
| Command | Description |
|---------|-------------|
| **`dydo check [path]`** | **Validate documentation** |
| **`dydo fix [path]`** | **Auto-fix issues** |
| `dydo index [path]` | Regenerate index.md from structure |
| `dydo graph <file>` | Show graph connections for a file |
| `dydo graph stats [--top N]` | Show top docs by incoming links |

### Agent Lifecycle
| Command | Description |
|---------|-------------|
| *`dydo agent claim <name\|auto>`* | *Claim an agent identity* |
| *`dydo agent release`* | *Release current agent* |
| *`dydo agent status [name]`* | *Show agent status* |
| **`dydo agent list [--free] [--all]`** | **List agents (default: current human's)** |
| **`dydo agent tree`** | **Show dispatch hierarchy of active agents** |
| *`dydo agent role <role> [--task X]`* | *Set role and permissions* |
| **`dydo agent clean <agent>`** | **Clean agent workspace** |

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
| *`dydo dispatch --wait/--no-wait --role <role> --task <name>`* | *Hand off work to another agent* |
| *`dydo dispatch --worktree ...`* | *Dispatch into an isolated git worktree* |
| *`dydo dispatch --queue <name> ...`* | *Serialize launches via named queue* |
| *`dydo inbox list`* | *List agents with inbox items* |
| *`dydo inbox show`* | *Show current agent's inbox* |
| *`dydo inbox clear --all`* | *Archive processed items* |

### Messaging
| Command | Description |
|---------|-------------|
| *`dydo msg --to <agent> --body "..."`* | *Send message to another agent* |
| *`dydo msg --to <agent> --subject <task> --body "..."`* | *Message with task context* |
| *`dydo wait --task <name>`* | *Wait for task-specific message* |
| *`dydo wait --cancel`* | *Cancel active waits* |

### Tasks
| Command | Description |
|---------|-------------|
| `dydo task create <name>` | Create a new task |
| *`dydo task ready-for-review <name> --summary "..."`* | *Mark task ready for review* |
| **`dydo task approve <name>`** / **`--all`** | **Approve task(s) (human only)** |
| `dydo task reject <name>` | Reject task (human only) |
| `dydo task list` | List tasks |
| `dydo task compact` | Compact audit snapshots |
| *`dydo review complete <task>`* | *Complete a code review* |

### Issues
| Command | Description |
|---------|-------------|
| `dydo issue create --title "..." --area <area> --severity <level>` | Create an issue |
| `dydo issue list [--area <area>] [--all]` | List issues |
| `dydo issue resolve <id> --summary "..."` | Resolve an issue |

### Inquisition
| Command | Description |
|---------|-------------|
| `dydo inquisition coverage [--files] [--gaps-only] [--summary]` | Show inquisition coverage across areas |

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
| **`dydo guard lift <agent> [minutes]`** | **Temporarily lift guard restrictions** |
| `dydo guard restore <agent>` | Restore guard restrictions |
| *`dydo workspace init`* | *Initialize agent workspaces* |
| *`dydo workspace check`* | *Verify workflow before session end* |

### Audit
| Command | Description |
|---------|-------------|
| **`dydo audit`** | **Generate activity replay visualization** |
| `dydo audit --list` | List available sessions |
| `dydo audit --session <id>` | Show details for a session |
| `dydo audit compact [year]` | Compact audit snapshots |

### Template
| Command | Description |
|---------|-------------|
| **`dydo template update`** | **Update framework templates and docs** |
| `dydo template update --diff` | Preview changes without writing |

### Utility
| Command | Description |
|---------|-------------|
| `dydo version` | Display version |

---

## License

AGPL-3.0 — [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)

**Free to use, always.** You can use dydo as a tool on any project, including commercial ones. The AGPL obligations apply only if you modify or embed dydo's source code in your own software — for example, shipping dydo as part of a product you distribute or offer as a service.

For commercial licensing without AGPL obligations, [open a GitHub issue](https://github.com/bodnarbalazs/dydo/issues).
