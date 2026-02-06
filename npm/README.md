# DynaDocs (dydo)

Documentation-driven context and agent orchestration for AI coding assistants.

100% local, 100% under your control.

## The Problem

**Is your project too large for LLMs to understand? Then this is for you.**

AI code editors forget everything between sessions. Every time you start, the agent wakes up with amnesia — no memory of your architecture, your conventions, or the context from yesterday.

So you explain the same things. Again. And again.

Claude Code and Cursor don't have memory built in. Tools like Windsurf and Antigravity have some form of it, but you don't control it.

## The Solution

DynaDocs is a documentation-based approach. Your docs **ARE** the memory.

Think of it like Groundhog Day: the AI wakes up fresh each session, but you've left it a note explaining everything it needs to know. It reads the note, onboards itself, and gets to work.

You maintain your project's intent, architecture, and conventions in structured documentation. Each session, the AI follows an onboarding funnel — reading just what it needs for the current task. A CLI tool enforces roles and permissions, so the AI stays in its lane.

![DynaDocs Architecture](https://raw.githubusercontent.com/bodnarbalazs/dydo/master/dydo_diagram.svg)

### What you get

- **Documentation as memory** — Your docs are the source of truth; AI re-reads them each session
- **Self-documenting folders** — Meta files describe folder purposes; summaries appear in hub links
- **Self-onboarding** — AI follows the funnel, no manual context-setting
- **Role-based permissions** — Reviewer can't edit code, code-writer can't touch docs
- **No self-review** — The agent that wrote the code cannot review it
- **Multi-agent workflows** — Run parallel agents on different tasks
- **Team support** — Each team member gets their own pool of agents
- **Platform-agnostic** — Works across AI tools (Claude, Cursor, etc.) and operating systems
- **Your process, your rules** — Modify templates, roles, and workflows to match how you work
- **Useful history** — Need fixing a bug caused by a change three days ago?
  - No problem, the agent will have a record of each finished task and the files touched.

---

## Installation

```bash
# npm (recommended)
npm install -g dydo

# if you have .NET installed
dotnet tool install -g DynaDocs
```

**Note:** The setup will prompt you to set the `DYDO_HUMAN` environment variable. Agents use this to know which human they belong to.

## Quick Start (2 min)

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

Dydo expects a certain format — relative links, frontmatter, consistent structure. Run these periodically:

```bash
dydo check    # Find issues
dydo fix      # Auto-fix what's possible
```

**Tip:** [Obsidian](https://obsidian.md) makes navigating the docs easier, but it converts links when you move files. Run `dydo fix` afterward. The fix command also generates missing hub files and folder meta files.

### 5. Customize the templates

Edit templates in `dydo/_system/templates/` to fit your project. Changes take effect when agents are claimed.

Fill out the `about.md` and modify the `coding-standards.md` to your taste.

You're ready to go. For best results, keep docs up to date and accurate to match your intent. Not everything needs documenting—just what you wouldn't know from reading the code.

---

## How It Works

**Example prompt:** `Hey Adele, help me implement authentication --feature`

1. The agent reads `CLAUDE.md`, gets redirected to `dydo/index.md`
2. From `index.md`, it navigates to its workspace: `dydo/agents/Adele/workflow.md`
3. It claims its identity: `dydo agent claim Adele`
4. The `--feature` flag tells it to follow: **interview → plan → code → review**
5. It sets its role: `dydo agent role interviewer --task auth`
6. On every file operation, the `dydo guard` hook enforces permissions based on the current role

**What's happening:** The AI onboards itself by following the documentation funnel — you don't have to re-explain what's already documented. Permissions aren't suggestions; the hook blocks unauthorized edits. When the code-writer finishes, it dispatches to a *different* agent for review. Fresh eyes, enforced.

---

## Workflow Flags

| Flag | Workflow |
|------|----------|
| `--feature` | Interview → Plan → Code → Review |
| `--task` | Plan → Code → Review |
| `--quick` | Code only (simple changes) |
| `--think` | Co-thinker mode |
| `--review` | Reviewer mode |
| `--docs` | Docs-writer mode |
| `--test` | Tester mode |

---

## Agent Roles

| Role | Can Edit | Purpose |
|------|----------|---------|
| `code-writer` | `src/**`, `tests/**` | Implement features |
| `reviewer` | agent workspace | Review code |
| `planner` | `tasks/**`, agent workspace | Design implementation |
| `tester` | `tests/**`, `pitfalls/**`, agent workspace | Write tests, report bugs |
| `docs-writer` | `dydo/**` (except agents/) | Write documentation |
| `co-thinker` | `decisions/**`, agent workspace | Explore ideas |
| `interviewer` | agent workspace | Gather requirements |

---

## Folder Structure

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
    │   └── pitfalls/            # Known issues
    │
    ├── _system/templates/       # Customizable templates
    ├── _assets/                 # Images, diagrams
    │   └── dydo-diagram.svg
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
| `dydo agent list [--free]` | List all agents |
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
| `dydo dispatch --role <role> --task <name>` | Hand off work |
| `dydo inbox list` | List agents with inbox items |
| `dydo inbox show` | Show current agent's inbox |
| `dydo inbox clear` | Clear processed items |

### Tasks
| Command | Description |
|---------|-------------|
| `dydo task create <name>` | Create a new task |
| `dydo task ready-for-review <name>` | Mark task ready for review |
| `dydo task approve <name>` | Approve task (human only) |
| `dydo task reject <name>` | Reject task (human only) |
| `dydo task list` | List tasks |
| `dydo review complete <task>` | Complete a code review |

### Workspace
| Command | Description |
|---------|-------------|
| `dydo guard` | Check permissions (for hooks) |
| `dydo clean <agent>` | Clean agent workspace |
| `dydo workspace init` | Initialize agent workspaces |
| `dydo workspace check` | Verify workflow before session end |

### Audit
| Command | Description |
|---------|-------------|
| `dydo audit` | Generate activity replay visualization |
| `dydo audit --list` | List available sessions |
| `dydo audit --session <id>` | Show details for a session |

---

### Limitations
Currently it's tested to work with Claude Code (hooks setup), but the principle should be the same for all coding agents.

## License

MIT — [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)
