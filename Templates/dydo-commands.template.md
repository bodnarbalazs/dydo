---
area: reference
type: reference
---

# CLI Commands Reference

Complete reference for all `dydo` commands.

---

## Setup Commands

### dydo init

Initialize DynaDocs in a project.

```bash
dydo init <integration>              # Initialize with integration (claude, none)
dydo init <integration> --join       # Join existing project as new team member
dydo init claude --name "Your Name" --agents 3  # Non-interactive setup
```

**Arguments:**
- `integration` - Integration type: `claude` (with hooks wired up) or `none` (for other systems, more setup needed)

**Options:**
- `--join` - Join existing project instead of creating new
- `--name <name>` - Human name (skips prompt)
- `--agents <count>` - Number of agents to create/assign

### dydo whoami

Show current agent identity and status.

```bash
dydo whoami
```

**Output:** Agent name, assigned human, role, task, workspace path, permissions.

---

## Documentation Commands

### dydo check

Validate documentation and report violations.

```bash
dydo check                    # Check all docs in dydo/
dydo check path/to/folder     # Check specific folder
dydo check path/to/file.md    # Check specific file
```

**Validates:** Naming conventions, frontmatter, links, summaries, hub files, orphans.

**Exit codes:** 0 = all valid, 1 = violations found.

### dydo fix

Auto-fix documentation issues where possible.

```bash
dydo fix                      # Fix all docs
dydo fix path/to/folder       # Fix specific folder
```

**Auto-fixes:** Kebab-case renaming, wikilink conversion, missing hub files.

### dydo index

Regenerate index.md from documentation structure.

```bash
dydo index                    # Regenerate root index
dydo index path/to/folder     # Regenerate folder index
```

### dydo graph

Show link graph connections for a documentation file.

```bash
dydo graph path/to/file.md              # Show outgoing links
dydo graph path/to/file.md --incoming   # Show backlinks
dydo graph path/to/file.md --degree 2   # Show 2-hop connections
```

**Options:**
- `--incoming` - Show docs that link TO this file
- `--degree <n>` - Show docs within n link-hops (default: 1)

---

## Agent Workflow Commands

### dydo agent claim

Claim an agent for this terminal session.

```bash
dydo agent claim auto         # Claim first available agent for current human
dydo agent claim Adele        # Claim specific agent by name
dydo agent claim A            # Claim by letter (A=Adele, B=Brian, etc.)
```

### dydo agent release

Release the current agent.

```bash
dydo agent release
```

### dydo agent status

Show agent status.

```bash
dydo agent status             # Show current agent's status
dydo agent status Adele       # Show specific agent's status
```

### dydo agent list

List all agents.

```bash
dydo agent list               # List all agents
dydo agent list --free        # List only free agents
```

### dydo agent role

Set the current agent's role.

```bash
dydo agent role code-writer                    # Set role
dydo agent role code-writer --task auth-login  # Set role with task
```

**Roles:** `code-writer`, `reviewer`, `co-thinker`, `docs-writer`, `interviewer`, `planner`, `tester`

---

## Agent Management Commands

### dydo agent new

Create a new agent and assign to a human.

```bash
dydo agent new William alice
```

**Arguments:**
- `name` - New agent name (e.g., William)
- `human` - Human to assign the agent to

### dydo agent rename

Rename an existing agent.

```bash
dydo agent rename Adele Aurora
```

**Arguments:**
- `old-name` - Current agent name
- `new-name` - New agent name

**Updates:** dydo.json, workspace folder, workflow file, mode files.

### dydo agent remove

Remove an agent from the pool.

```bash
dydo agent remove William           # Interactive confirmation
dydo agent remove William --force   # Skip confirmation
```

**Arguments:**
- `name` - Agent name to remove

**Options:**
- `--force` - Skip confirmation prompt

**Deletes:** dydo.json entry, workspace folder, workflow file.

### dydo agent reassign

Reassign an agent to a different human.

```bash
dydo agent reassign Adele bob
```

**Arguments:**
- `name` - Agent name to reassign
- `human` - New human to assign the agent to

---

## Dispatch & Inbox Commands

### dydo dispatch

Dispatch work to another agent.

```bash
dydo dispatch --role code-writer --task auth-login --brief "Implement OAuth"
dydo dispatch --role code-writer --task auth-login --brief "Implement OAuth" --files "src/Auth/**"
dydo dispatch --role reviewer --task auth-login --brief "Review PR" --no-launch
```

**Options:**
- `--role <role>` - Role for the target agent (required)
- `--task <name>` - Task name (required)
- `--brief <text>` - Brief description (required)
- `--files <pattern>` - File pattern to include
- `--context-file <path>` - Path to context file
- `--to <agent-name>` - Send to specific agent (skips auto-selection)
- `--escalate` - Mark as escalated (after repeated failures)
- `--no-launch` - Don't launch terminal, just write to inbox

### dydo inbox list

List agents with pending inbox items.

```bash
dydo inbox list
```

### dydo inbox show

Show current agent's inbox.

```bash
dydo inbox show
```

### dydo inbox clear

Clear processed inbox items.

```bash
dydo inbox clear --all        # Clear all items
dydo inbox clear --id abc123  # Clear specific item
```

**Options:**
- `--all` - Clear all items
- `--id <id>` - Clear specific item by ID

---

## Workspace Commands

### dydo guard

Check if current agent can perform an action. Used by the hooks. 
For Claude Code they're wired up automatically for other tools it has to be set up manually.

```bash
# Via stdin (hook mode)
echo '{"tool":"Edit","path":"src/file.cs"}' | dydo guard

# Via arguments (manual testing)
dydo guard --action edit --path src/file.cs
dydo guard --command "cat secrets.json"
```

**Options:**
- `--action <action>` - Action: edit, write, delete, read
- `--path <path>` - Path being accessed
- `--command <cmd>` - Bash command to analyze

**Exit codes:** 0 = allowed, 2 = blocked.

### dydo clean

Clean agent workspace.

```bash
dydo clean Adele              # Clean specific agent
dydo clean --all              # Clean all agent workspaces
dydo clean --task auth-login  # Clean workspaces for a task
dydo clean --all --force      # Force clean even if working
```

**Options:**
- `--all` - Clean all agent workspaces
- `--force` - Force clean even if agents are working
- `--task <name>` - Clean workspaces associated with a task

### dydo workspace init

Initialize agent workspaces.

```bash
dydo workspace init
dydo workspace init --path /custom/path
```

### dydo workspace check

Verify workflow requirements before session end.

```bash
dydo workspace check
```

**Checks:** Active tasks, unprocessed inbox items, workflow completion.

---

## Task Commands

### dydo task create

Create a new task.

```bash
dydo task create auth-login
dydo task create auth-login --description "Implement user authentication"
```

**Arguments:**
- `name` - Task name (kebab-case)

**Options:**
- `--description <text>` - Task description

### dydo task ready-for-review

Mark task ready for review.

```bash
dydo task ready-for-review auth-login --summary "Implemented OAuth flow"
```

**Arguments:**
- `name` - Task name

**Options:**
- `--summary <text>` - Review summary (required)

### dydo task approve

Approve a task (human only).

```bash
dydo task approve auth-login
dydo task approve auth-login --notes "Great work!"
```

**Arguments:**
- `name` - Task name

**Options:**
- `--notes <text>` - Approval notes

### dydo task reject

Reject a task (human only).

```bash
dydo task reject auth-login --notes "Missing error handling"
```

**Arguments:**
- `name` - Task name

**Options:**
- `--notes <text>` - Rejection reason (required)

### dydo task list

List tasks.

```bash
dydo task list                  # List active tasks
dydo task list --needs-review   # List tasks needing human review
dydo task list --all            # Include closed tasks
```

**Options:**
- `--needs-review` - Show only tasks needing review
- `--all` - Show all tasks including closed

---

## Review Commands

### dydo review complete

Complete a code review.

```bash
dydo review complete auth-login --status pass
dydo review complete auth-login --status fail --notes "Found security issue"
```

**Arguments:**
- `task` - Task name being reviewed

**Options:**
- `--status <pass|fail>` - Review result (required)
- `--notes <text>` - Review notes

---

## Utility Commands

### dydo version

Display version information.

```bash
dydo version
```

### dydo help

Display help information.

```bash
dydo help
```

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DYDO_HUMAN` | Human identifier for agent assignment |

Set before running commands:

```bash
# Bash/Zsh
export DYDO_HUMAN="your_name"

# PowerShell
$env:DYDO_HUMAN = "your_name"
```

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success / Action allowed |
| 1 | Validation errors found |
| 2 | Tool error / Action blocked |

---

## Role Permissions

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | (read-only) | (all files) |
| `co-thinker` | `dydo/agents/{agent}/**`, `dydo/project/decisions/**` | `src/**`, `tests/**` |
| `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**`, `tests/**` |
| `interviewer` | `dydo/agents/{agent}/**` | Everything else |
| `planner` | `dydo/agents/{agent}/**`, `dydo/project/tasks/**` | `src/**` |
| `tester` | `dydo/agents/{agent}/**`, `tests/**`, `dydo/project/pitfalls/**` | `src/**` |
