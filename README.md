# DynaDocs (dydo)

A platform-agnostic AI orchestration and context-management framework.

## The Problem

AI code editors need persistence. Without it, each session starts fresh and the agent has to gather context about the project before it can even begin working on your actual task.

So we have to explain the same context each time.

Claude Code and Cursor don't have memory built in. Tools like Windsurf and Antigravity have some form of it, but you don't control it.

## The Solution

DynaDocs combines an agent-friendly documentation format with a CLI tool for deterministic rule enforcement and framework management.

You point your AI assistant to `index.md` (via CLAUDE.md or equivalent), and from there it goes through an onboarding process where it learns about the framework and how to use it. Based on the prompt, it self-assigns to the appropriate workflow and mode.

![DynaDocs Architecture](dydo_diagram.svg)

---

## Installation

```bash
# npm (recommended)
npm install -g dydo

# .NET developers
dotnet tool install -g DynaDocs
```

## Quick Start

### 1. Set up dydo in your project

If you use Claude Code:
```bash
dydo init claude
```
If you use something else:
```bash
dydo init none
```

This creates the `dydo/` folder structure, templates, and configures Claude Code hooks automatically.

### 2. Link your AI entry point

Add this to your `CLAUDE.md` (or equivalent for other AI tools):

```markdown
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

---

## How It Works

**Example prompt:** `Hey Adele, help me implement authentication --feature`

1. The agent reads `CLAUDE.md`, gets redirected to `dydo/index.md`
2. From `index.md`, it navigates to its workspace: `dydo/agents/Adele/workflow.md`
3. It claims its identity: `dydo claim Adele`
4. The `--feature` flag tells it to follow: **interview → plan → code → review**
5. It sets its role: `dydo agent role interviewer --task auth`
6. On every file operation, the `dydo guard` hook enforces permissions based on the current role

**No self-review:** An agent that wrote code cannot review it. The system tracks role history per task and enforces fresh-eyes validation.

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
| `reviewer` | *(read-only)* | Review code |
| `planner` | `tasks/**`, agent workspace | Design implementation |
| `tester` | `tests/**`, `pitfalls/**` | Write tests, report bugs |
| `docs-writer` | `dydo/**` (except agents/) | Write documentation |
| `co-thinker` | `decisions/**`, agent workspace | Explore ideas |
| `interviewer` | agent workspace | Gather requirements |

---

## Folder Structure

```
project/
├── dydo.json              # Configuration
├── CLAUDE.md              # AI entry point
└── dydo/
    ├── index.md           # Documentation root
    ├── _system/templates/ # Customizable templates
    ├── understand/        # Domain concepts, architecture
    ├── guides/            # How-to guides
    ├── reference/         # API docs, specs
    ├── project/           # Decisions, pitfalls, changelog
    │   └── tasks/         # Cross-agent task handoff
    └── agents/            # Agent workspaces (gitignored)
```
---

## Commands

| Command | Description |
|---------|-------------|
| `dydo init <integration>` | Initialize project (`claude`, `none`) |
| `dydo check [path]` | Validate documentation |
| `dydo fix [path]` | Auto-fix issues |
| `dydo agent claim <name\|auto>` | Claim an agent identity |
| `dydo agent role <role> [--task X]` | Set role and permissions |
| `dydo agent list` | List all agents and their status |
| `dydo dispatch --role <role> --task <name>` | Hand off work to another agent |
| `dydo guard` | Check permissions (for hooks) |




---

## License

MIT
