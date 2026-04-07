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

### dydo graph stats

Show document link statistics ranked by incoming links. Useful for identifying the most referenced documents in your documentation.

```bash
dydo graph stats            # Show top 100 documents by incoming links
dydo graph stats --top 20   # Show top 20 documents
```

**Options:**
- `--top <n>` - Number of documents to show (default: 100)

**Output:**
```
Document Link Statistics (Top 100)
──────────────────────────────────
  #   In  Document
  1   23  glossary.md
  2   18  understand/architecture.md
  3   15  reference/api.md

Total: 47 documents, 156 internal links
```

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

List agents. By default, shows only agents assigned to the current human (with Task column). Use `--all` to show all agents across all humans (with Human column).

```bash
dydo agent list               # List current human's agents
dydo agent list --free        # List only free agents for current human
dydo agent list --all         # List all agents across all humans
dydo agent list --all --free  # List all free agents across all humans
```

### dydo agent role

Set the current agent's role.

```bash
dydo agent role code-writer                    # Set role
dydo agent role code-writer --task auth-login  # Set role with task
```

**Roles:** `code-writer`, `reviewer`, `co-thinker`, `docs-writer`, `planner`, `test-writer`, `orchestrator`, `inquisitor`, `judge`

### dydo agent clean

Clean agent workspace.

```bash
dydo agent clean Adele              # Clean specific agent
dydo agent clean --all              # Clean all agent workspaces
dydo agent clean --task auth-login  # Clean workspaces for a task
dydo agent clean --all --force      # Force clean even if working
```

**Options:**
- `--all` - Clean all agent workspaces
- `--force` - Force clean even if agents are working
- `--task <name>` - Clean workspaces associated with a task

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

Dispatch work to another agent. Requires `--wait` or `--no-wait` to indicate intent.

```bash
# Expecting feedback (creates wait marker, enters poll loop):
dydo dispatch --wait --auto-close --role reviewer --task auth-login --brief "Review PR"

# Fire and forget:
dydo dispatch --no-wait --role code-writer --task auth-login --brief "Implement OAuth"
dydo dispatch --no-wait --role code-writer --task auth-login --brief "Implement OAuth" --files "src/Auth/**"
```

**Options:**
- `--role <role>` - Role for the target agent (required)
- `--task <name>` - Task name (required)
- `--wait` - Wait for a response from the dispatched agent (required, mutually exclusive with --no-wait)
- `--no-wait` - Dispatch and return immediately (required, mutually exclusive with --wait)
- `--brief <text>` - Brief description (required unless --brief-file used)
- `--brief-file <path>` - Read brief from a file instead of inline
- `--files <pattern>` - File pattern to include
- `--to <agent-name>` - Send to specific agent (skips auto-selection)
- `--escalate` - Mark as escalated (after repeated failures)
- `--auto-close` - Auto-close the dispatched agent's terminal after release
- `--no-launch` - Don't launch terminal, just write to inbox
- `--tab` - Launch in a new tab instead of a new window (overrides config)
- `--new-window` - Launch in a new window (overrides config)
- `--worktree` - Run dispatched agent in a git worktree for isolated work
- `--queue <name>` - Named queue to serialize terminal launch (e.g., `--queue merge`). Defers terminal launch if another item is active in the queue. Agent selection and inbox happen immediately.

**Auto-transition:** When `--role reviewer` is used, the task is automatically marked `review-pending` and the `--brief` becomes the review summary. No need to call `dydo task ready-for-review` separately.

**Double-dispatch protection:** If another agent is already working on the same task, dispatch is blocked.

**`--wait` behavior:** Creates a wait marker, then polls for a response. The marker blocks release until cancelled.

**`--no-wait` behavior:** Returns immediately. Shows a release hint when appropriate.

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

## Messaging Commands

### dydo message

Send a message to another agent. Alias: `dydo msg`.

```bash
dydo message --to Brian --body "Auth done. Tests pass."
dydo msg --to Brian --body "Auth done."
dydo msg --to Brian --subject "auth-login" --body "Done."
dydo msg --to Brian --body-file ./summary.md
dydo msg --to Brian --body "Important." --force
```

**Options:**
- `--to <agent>` - Target agent (required)
- `--body <text>` - Message content (required unless --body-file)
- `--body-file <path>` - Read body from file
- `--subject <name>` - Topic/task identifier
- `--force` - Send to inactive agents

**Restrictions:** No self-messaging. No cross-human messaging.

### dydo wait

Wait for an incoming message. Blocks until a message arrives in the agent's inbox.

```bash
dydo wait                          # Wait for any message
dydo wait --task auth-login        # Wait for message with matching subject
dydo wait --task auth-login --cancel  # Cancel an active wait (remove marker)
dydo wait --cancel                 # Cancel all active waits
```

**Options:**
- `--task <name>` - Only wake on messages with this subject
- `--cancel` - Cancel an active wait (remove wait marker)

**Behavior:** Polls every 10 seconds. No timeout. If killed by bash timeout, re-run the command. General wait skips messages claimed by active wait markers (channel isolation).

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
dydo task create auth-login --area backend
dydo task create auth-login --area backend --description "Implement user authentication"
```

**Arguments:**
- `name` - Task name (kebab-case)

**Options:**
- `--area <area>` - Task area, e.g. backend, frontend, general (required)
- `--description <text>` - Task description

### dydo task ready-for-review

Mark task ready for review. **This must be called before `dydo review complete`.**

```bash
dydo task ready-for-review auth-login --summary "Implemented OAuth flow"
```

**Arguments:**
- `name` - Task name

**Options:**
- `--summary <text>` - Review summary (**required** - describe what you did)

### dydo task approve

Approve a task (human only).

```bash
dydo task approve auth-login
dydo task approve auth-login --notes "Great work!"
dydo task approve --all
dydo task approve --all --notes "Batch approved"
```

**Arguments:**
- `name` - Task name (optional when using `--all`)

**Options:**
- `--all`, `-a` - Approve all pending tasks
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

## Issue Commands

### dydo issue create

Create a new issue.

```bash
dydo issue create --title "Null ref in AuthService" --area backend --severity high
dydo issue create --title "Missing validation" --area backend --severity medium --found-by inquisition
```

**Options:**
- `--title <text>` - Issue title (required)
- `--area <area>` - Affected area, e.g. backend, frontend, general (required)
- `--severity <level>` - Severity: `low`, `medium`, `high`, `critical` (required)
- `--found-by <source>` - How it was found: `manual`, `inquisition`, `review` (optional)

### dydo issue list

List issues.

```bash
dydo issue list                  # List open issues
dydo issue list --area backend   # Filter by area
dydo issue list --status resolved  # Filter by status
dydo issue list --all            # Include resolved issues
```

**Options:**
- `--area <area>` - Filter by area
- `--status <status>` - Filter by status
- `--all` - Show all issues including resolved

### dydo issue resolve

Resolve an issue.

```bash
dydo issue resolve 0001 --summary "Fixed in commit abc123"
```

**Arguments:**
- `id` - Issue ID (e.g., 0001)

**Options:**
- `--summary <text>` - Resolution summary (required)

---

## Review Commands

### dydo review complete

Complete a code review.

**Prerequisite:** The task must be in `review-pending` state. This happens automatically when dispatching with `--role reviewer`. You can also run `dydo task ready-for-review <task> --summary "..."` manually.

```bash
# Normal workflow (dispatch auto-transitions the task):
dydo dispatch --wait --auto-close --role reviewer --task auth-login --brief "Implemented OAuth flow"
dydo review complete auth-login --status pass
dydo review complete auth-login --status fail --notes "Found security issue"
```

**Arguments:**
- `task` - Task name being reviewed

**Options:**
- `--status <pass|fail>` - Review result (required)
- `--notes <text>` - Review notes

---

## Audit Commands

### dydo audit

View and visualize agent activity logs.

```bash
dydo audit                   # Generate activity replay visualization
dydo audit /2025             # Filter to specific year
dydo audit --list            # List available sessions
dydo audit --session <id>    # Show details for a session
```

**Arguments:**
- `path` - Path filter (e.g., /2025 for year 2025)

**Options:**
- `--list` - List available sessions
- `--session <id>` - Show details for a specific session ID

### dydo audit compact

Compact audit snapshots using baseline+delta compression.

```bash
dydo audit compact           # Compact current year
dydo audit compact 2025      # Compact specific year
```

**Arguments:**
- `year` - Year to compact (e.g., 2025). Defaults to current year.

---

## Template Commands

### dydo template update

Update framework-owned templates and docs to the latest version.

```bash
dydo template update           # Apply updates, re-anchor user includes
dydo template update --diff    # Preview changes without writing
dydo template update --force   # Overwrite even if re-anchoring fails (backs up first)
```

**Options:**
- `--diff` - Preview what would change without writing any files
- `--force` - Overwrite files even when user-added include tags can't be re-anchored (creates `.backup` of original)

**Behavior:**
- Compares on-disk templates to embedded (latest) versions using SHA256 hashes
- Clean files (hash matches) are overwritten with the new version
- User-edited files: extracts user-added `{{include:...}}` tags, writes new template, re-anchors tags
- Non-include edits are lost on update (only include tags are preserved)
- Binary files (`_assets/dydo-diagram.svg`) use byte-level hash comparison

**Exit codes:** 0 = success, 1 = warnings (unplaceable tags without `--force`).

---

## Inquisition Commands

### dydo inquisition coverage

Show inquisition coverage across project areas.

```bash
dydo inquisition coverage                    # Folder-level overview
dydo inquisition coverage --files            # File-level coverage heatmap
dydo inquisition coverage --files --gaps-only # Only gap and low-coverage files
dydo inquisition coverage --summary          # Folder-level aggregates only
dydo inquisition coverage --path Commands/   # Scope to a subtree
dydo inquisition coverage --since 90         # Only consider last 90 days
```

**Options:**
- `--files` - File-level coverage heatmap (shows per-file scores)
- `--gaps-only` - Only show gap (never inspected) and low-coverage files
- `--summary` - Folder-level aggregates only
- `--path <path>` - Scope output to a subtree
- `--since <days>` - Days lookback (default: 365)

**Output:** Lists project areas with their inquisition coverage status based on reports in `dydo/project/inquisitions/`.

---

## Role Commands

### dydo roles list

List all loaded role definitions.

```bash
dydo roles list
```

**Output:** Lists all roles (base and custom) with their descriptions.

### dydo roles reset

Regenerate base role definition files.

```bash
dydo roles reset              # Regenerate base role files only
dydo roles reset --all        # Remove all role files (including custom) before regenerating
```

**Options:**
- `--all` - Remove all role files (including custom) before regenerating base roles

### dydo roles create

Scaffold a new custom role definition file.

```bash
dydo roles create my-role
```

**Arguments:**
- `name` - Name for the new role

**Creates:** A new `.role.json` file in `dydo/_system/roles/`.

### dydo validate

Validate dydo configuration, role files, and agent state.

```bash
dydo validate
```

**Validates:** `dydo.json` configuration, role definition files, agent assignments, and overall system integrity.

---

## Utility Commands

### dydo completions

Generate shell completion scripts.

```bash
dydo completions bash        # Generate bash completions
dydo completions zsh         # Generate zsh completions
dydo completions powershell  # Generate PowerShell completions
```

**Arguments:**
- `shell` - Shell type: `bash`, `zsh`, or `powershell`

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
| `planner` | `dydo/agents/{agent}/**`, `dydo/project/tasks/**` | `src/**` |
| `test-writer` | `dydo/agents/{agent}/**`, `tests/**`, `dydo/project/pitfalls/**` | `src/**` |
| `orchestrator` | `dydo/agents/{agent}/**`, `dydo/project/tasks/**`, `dydo/project/decisions/**` | `src/**`, `tests/**` |
| `inquisitor` | `dydo/agents/{agent}/**`, `dydo/project/inquisitions/**` | `src/**`, `tests/**` |
| `judge` | `dydo/agents/{agent}/**`, `dydo/project/issues/**` | `src/**`, `tests/**` |
