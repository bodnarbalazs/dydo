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
dydo init <integration>              # Initialize with integration (claude, codex, none)
dydo init <integration> --join       # Join existing project as new team member
dydo init claude --name "Your Name" --agents 3  # Non-interactive setup
dydo init codex --name "Your Name" --agents 3   # Non-interactive Codex setup
```

**Arguments:**
- `integration` - Integration type: `claude` or `codex` (with hooks wired up), or `none` (for other systems, more setup needed)

**Options:**
- `--join` - Join existing project instead of creating new
- `--name <name>` - Human name (skips prompt)
- `--agents <count>` - Number of agents to create/assign

### dydo sync

Compile dydo role definitions into native Claude Code and Codex artifacts - Claude `.claude/agents/<role>.md` / `.claude/skills/<role>/SKILL.md` outputs plus Codex `.codex/agents/<role>.toml` / `.agents/skills/<role>/SKILL.md` outputs (Decision 024).

```bash
dydo sync   # emit Claude and Codex agents/skills from roles + docs
```

**Behavior:**
- Worker roles (code-writer, reviewer, test-writer, docs-writer, sprint-auditor) emit both an agent definition and a skill.
- Tier-1 managers (orchestrator, co-thinker, chief-of-staff) and `planner` emit a skill only.
- Codex outputs are written to `.codex/agents/` and `.agents/skills/`.
- Model tiers declared per role are bound to concrete models at sync time (Decision 028).

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
- `--force` - Force clean even if agents are working (human-only; claimed agents are blocked from forcing-clean other agents)
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

Dispatch work to another agent. Reserves the target agent, writes a single assignment inbox item carrying the role/brief, and launches a terminal for that agent.

```bash
dydo dispatch --role reviewer --task auth-login --brief "Review PR"
dydo dispatch --role code-writer --task auth-login --brief "Implement OAuth"
dydo dispatch --role code-writer --task auth-login --brief "Implement OAuth" --files "src/Auth/**"
```

**Options:**
- `--role <role>` - Role for the target agent (required)
- `--task <name>` - Task name (required)
- `--brief <text>` - Brief description (required unless --brief-file used)
- `--brief-file <path>` - Read brief from a file instead of inline
- `--files <pattern>` - File pattern to include
- `--to <agent-name>` - Send to specific agent (skips auto-selection); alias `--agent`
- `--escalate` - Mark as escalated (after repeated failures)
- `--auto-close` - Auto-close the dispatched agent's terminal after release
- `--no-launch` - Don't launch terminal, just write to inbox
- `--tab` - Launch in a new tab instead of a new window (overrides config)
- `--new-window` - Launch in a new window (overrides config)
- `--codex` - Launch the dispatched agent in Codex
- `--claude` - Launch the dispatched agent in Claude Code

By default, dispatch launches the same host as the calling agent's session. If the caller host is unknown, it launches Claude Code. `--codex` and `--claude` override the default and cannot be used together.

**Auto-transition:** When `--role reviewer` is used, the task is automatically marked `in-review` and the `--brief` becomes the review summary. No need to call `dydo task ready-for-review` separately.

**Double-dispatch protection:** If another agent is already working on the same task, dispatch is blocked.

**Launch bridge:** Dispatch reserves the agent (status becomes `Dispatched`). The launched agent claims, reads its assignment from the inbox, and sets its role. If the launch fails, the stale-dispatch reclaim returns the agent to a re-dispatchable state.

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
dydo inbox clear --force --file dydo/agents/Name/inbox/x.md  # Recover an orphaned inbox file
```

**Options:**
- `--all` - Clear all items
- `--id <id>` - Clear specific item by ID
- `--force` - Force-clear an orphaned inbox file (only when the owner has no live session); requires `--file`
- `--file <path>` - Path to the specific inbox file to force-clear (used with `--force`)

### dydo read

Print a target's content and register the read in one step — display-equals-ack. On hosts where the guard cannot observe file Reads (shell-based, e.g. codex), this is how an agent registers its inbox items and must-reads. The content is always printed before the read is registered; there is no path that acks without printing.

```bash
dydo read <message-id>   # Print an inbox item and mark it read
dydo read <file-path>    # Print a file; if it is an unread must-read, mark it complete
```

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
dydo wait --register               # Register a durable wait and return immediately
dydo wait --task auth-login --cancel  # Cancel an active wait (remove marker)
dydo wait --cancel                 # Cancel all active waits
```

**Options:**
- `--task <name>` - Only wake on messages with this subject
- `--register` - Register a durable wait marker and return immediately, instead of blocking. For hosts whose runtime cannot hold a foreground wait (e.g. dispatched codex sessions). Auto-selected when the caller's session host is such a host.
- `--cancel` - Cancel an active wait (remove wait marker)

**Behavior:** Polls every 10 seconds. No timeout. If killed by bash timeout, re-run the command. General wait skips messages claimed by active wait markers (channel isolation). A durable wait (`--register`) does not block: it writes a marker keyed to the claimed session's host process, so it survives tool timeouts and stays valid while the session lives; poll for messages with `dydo inbox show` / `dydo read`. `dydo agent release` and `dydo wait --cancel` remove it; a dead host makes it stale and it is cleaned up like a dead wait.

---

## Workspace Commands

### dydo guard

Check if current agent can perform an action. Used by Claude Code and Codex guard hooks.
For `claude` and `codex` integrations, `dydo init <integration>` wires hooks automatically (`.claude/settings.local.json` for Claude Code, `.codex/hooks.json` for Codex). Other tools require manual hook setup.

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
- `--stop` - Stop-hook mode: derive the needs-human flag from turn-end (used by the Stop hook)

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

### dydo task done

Mark a task done after verification. An assigned agent cannot mark its own task done; human terminals and other agents may accept tasks in `in-progress` or `in-review`.

```bash
dydo task done auth-login
```

**Arguments:**
- `name` - Task name

### dydo task list

List tasks.

```bash
dydo task list                  # List active tasks
dydo task list --needs-review   # List tasks needing review
dydo task list --all            # Include done tasks
```

**Options:**
- `--needs-review` - Show only tasks needing review
- `--all` - Show all tasks including done

---

## Attention Commands

### dydo hand raise

Raise the needs-human attention flag (Decision 030) — the **explicit** half of the attention signal. Targets the current agent, or a named one via `--agent` so an orchestrator can flag the context it manages.

```bash
dydo hand raise                 # Flag the current agent's session
dydo hand raise --agent Adele   # Flag a specific agent
```

**Explicit vs derived.** The flag has two provenances. Machine detections — an `AskUserQuestion` tool call, a turn that ends mid-task (the Stop hook), or a crashed session caught by the watchdog — set a **derived** flag that self-heals: the agent's next guarded tool call clears it, and the watchdog reconcile sweep clears it once its cause disappears. `dydo hand raise` sets an **explicit** flag that is deliberately sticky: it is **not** cleared by the raiser's next tool call and **not** swept away when the target is idle — only `dydo hand lower`, agent release, or — once the runtime-to-board bridge lands — a human unchecking it in Notion clears it. Raising over an existing derived flag upgrades it to explicit.

`--agent` is validated against the agent pool before anything is written: an unknown or malformed name is a clear error with a non-zero exit and no state change.

**Options:**
- `--agent <name>` - Target agent (defaults to the current agent for this session)

---

### dydo hand lower

Clear the needs-human flag once the human's input is no longer needed. Lowers **both** derived and explicit flags.

```bash
dydo hand lower                 # Clear the current agent's flag
dydo hand lower --agent Adele   # Clear a specific agent's flag
```

**Options:**
- `--agent <name>` - Target agent (defaults to the current agent for this session)

---

## Issue Commands

### dydo issue create

Create a new issue.

```bash
dydo issue create --title "Null ref in AuthService" --area backend --severity high --summary "Login throws NRE when the principal lacks an email claim."
dydo issue create --title "Missing validation" --area backend --severity medium --summary "Sign-up accepts blank usernames." --found-by inquisition
dydo issue create --title "Race in queue" --area backend --severity high --summary "Two workers can claim the same job under contention." --body "Two workers can claim the same job."
dydo issue create --title "Schema drift" --area backend --severity medium --body-file ./issue-body.md  # no --summary → file gets a "(One-line summary)" placeholder that must be replaced before `dydo check` is clean.
```

**Options:**
- `--title <text>` - Issue title (required)
- `--area <area>` - Affected area, e.g. backend, frontend, general (required)
- `--severity <level>` - Severity: `low`, `medium`, `high`, `critical` (required)
- `--found-by <source>` - How it was found: `manual`, `inquisition`, `review` (optional)
- `--summary <text>` - One-line summary rendered between the title and `## Description` (optional but recommended; omitting it inserts a `(One-line summary)` placeholder that `dydo check` flags as a warning)
- `--body <text>` - Inline body content for the issue's Description section (optional)
- `--body-file <path>` - Read body content from a file (optional, mutually exclusive with `--body`)

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

**Prerequisite:** The task must be in `in-review` state. This happens automatically when dispatching with `--role reviewer`. You can also run `dydo task ready-for-review <task> --summary "..."` manually.

```bash
# Normal workflow (dispatch auto-transitions the task):
dydo dispatch --auto-close --role reviewer --task auth-login --brief "Implemented OAuth flow"
dydo review complete auth-login --status pass
dydo review complete auth-login --status fail --notes "Found security issue"
```

**Arguments:**
- `task` - Task name being reviewed

**Options:**
- `--status <pass|fail>` - Review result (required)
- `--notes <text>` - Review notes

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

---

## Validation Commands

### dydo validate

Validate dydo configuration, role files, and agent state.

```bash
dydo validate
```

**Validates:** `dydo.json` configuration, role definition files, agent assignments, and overall system integrity.

---

## Model Commands

### dydo model cap

Temporarily rebind every tier using an unavailable model to a fallback model, then re-sync native agent definitions. The watchdog restores the original bindings after the reset time passes.

```bash
dydo model cap claude-fable-5 --until "07-14 09:00"
dydo model cap claude-fable-5 --until "2026-07-14 09:00" --fallback claude-opus-4-1
```

**Arguments:**
- `model` - Unavailable model id to cap.

**Options:**
- `--until <time>` - Local reset time from the limit error, as `[yyyy-]mm-dd hh:mm`.
- `--fallback <model>` - Model to rebind capped tiers to. Defaults to `models.fallback` in `dydo.json`.

### dydo model uncap

Restore a capped model's original tier bindings, clear the local cap marker, and re-sync native agent definitions.

```bash
dydo model uncap claude-fable-5
```

**Arguments:**
- `model` - Capped model id to restore.

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

## Notion Commands

### dydo notion connect

Store a Notion integration token for this project. The token is read from stdin (never a command-line argument, so it stays out of shell history) and saved to a gitignored local secret store — DPAPI-protected on Windows, `0600`-permissioned elsewhere — and never committed. Pass `--parent-page <id>` to also record the parent page in `notion.parentPageId`. Pass `--vault` to instead seal the token into a committed, passphrase-encrypted vault (prompts for a passphrase, entered twice) rather than the local-only store; if a vault already exists, `--vault` re-encrypts (rotates) it.

```bash
dydo notion connect
dydo notion connect --parent-page <page-id>
dydo notion connect --vault
```

### dydo notion reveal-token

Print the stored Notion integration token to stdout. A guarded break-glass for the show-once token: it requires `--yes` (or an interactive confirmation) and prints a warning, since it exposes a secret to the terminal.

```bash
dydo notion reveal-token --yes
```

### dydo notion sync

Reconcile the sync model's object types (default `Release` → `Campaign` → `Sprint` → `SprintTask` → `Issue`) against Notion bidirectionally, provisioning one Notion database per object type under a parent page. Requires a `DYDO_NOTION_TOKEN` integration token and a parent page from `notion.parentPageId` in dydo.json or the `DYDO_NOTION_PARENT_PAGE` environment variable. Use `--dry-run` to print the reconcile plan without applying it.

The sync model owns the schema shape one-way (project → Notion): data values sync both ways, but which properties and select options exist flows only from the model. Schema drift — a property or select option added in Notion but absent from the model — is reported as a warning and left untouched by default. Pass `--prune` to delete it instead (rogue properties are removed; a drifted select's options are reset to the model's set). A rogue option's stored value still round-trips as data; only the schema option is pruned.

Alongside the PM spine, sync can also mirror the browsable docs tree to a nested-page hierarchy under the same parent (a `Docs` page). The mirror is **opt-in**: the plain `dydo notion sync` runs the spine only. Pass `--docs` to run the spine plus the docs mirror, `--docs-only` for the mirror alone (never touches the PM board), or `--spine-only` for the explicit spine-only scope (the default); `--docs-only` and `--spine-only` are mutually exclusive. Pass `--parent-page <page-id>` to mirror under an explicit page, overriding `notion.parentPageId` / `DYDO_NOTION_PARENT_PAGE` — e.g. to smoke-test the docs mirror against a scratch page.

```bash
dydo notion sync
dydo notion sync --dry-run
dydo notion sync --prune
dydo notion sync --docs
dydo notion sync --docs-only --parent-page <scratch-page-id>
dydo notion sync --spine-only
```

### dydo notion reset

Wipe the tracked Notion databases and recreate them fresh from the sync model. Unlike `dydo notion sync` — a forward, create-only reconcile that never renames a database, restores a deleted view, or reverts a manual layout edit — a reset makes the live board match the model again regardless of manual mess. It archives (trashes) the tracked databases by their recorded ids, clears the provision state, then re-runs the normal spine provision, re-minting every database and re-pushing every repo doc. The archive happens **before** the state is cleared so the old databases are never orphaned into duplicates. Notion has no hard delete, so the wipe archives to Notion Trash — you can restore from there if needed. This is destructive to board data, so it confirms interactively first (pass `--yes` to skip). Use `--dry-run` to print the archive + recreate plan without touching Notion, and `--parent-page <page-id>` to recreate under an explicit page (e.g. a throwaway scratch workspace). If a reset is interrupted mid-run, re-run `dydo notion reset` (it is idempotent) rather than `dydo notion sync` — sync would try to reuse an already-archived database.

```bash
dydo notion reset --dry-run
dydo notion reset
dydo notion reset --yes --parent-page <scratch-page-id>
```

---

## Model Commands

Time-boxed operational swaps for a model outage (issue #214). When a tier's bound model becomes unavailable — the canonical case is Fable hitting its weekly spend cap, which the API blocks with no retry and no native fallback — cap it to a fallback so the review/audit gate keeps running, then let it auto-restore.

### dydo model cap

Rebind every tier currently pointing at `<model>` to a fallback and re-run `dydo sync` so the compiled agents use it. Pass `--until <time>` (required) with the reset time from the limit error, in `[yyyy-]mm-dd hh:mm` local-time form (the year is optional). Pass `--fallback <model>` to choose the replacement; without it, `models.fallback` from dydo.json is used. A local marker records what to restore, and the watchdog puts the original bindings back once the reset time passes.

```bash
dydo model cap claude-fable-5 --until "07-13 09:00"
dydo model cap claude-fable-5 --until "2026-07-13 09:00" --fallback claude-opus-4-8
```

### dydo model uncap

Restore `<model>`'s tier bindings immediately — the manual counterpart to the watchdog's time-based restore. Reverses the rebind, clears the cap marker, and re-syncs.

```bash
dydo model uncap claude-fable-5
```

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DYDO_HUMAN` | Human identifier for agent assignment |
| `DYDO_NOTION_TOKEN` | Notion integration token enabling `dydo notion sync` |
| `DYDO_NOTION_PARENT_PAGE` | Notion parent page the `dydo notion sync` spine databases live under (overridden by `notion.parentPageId` in dydo.json) |

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

As of 2.0 (Decision 024), dydo no longer enforces per-role writable/read-only **path matrices**. The guard enforces **universal off-limits + nudges** for every agent, and a worker role's read-only scope is set by its **native tool allowlist** — `dydo sync` emits read-only agents (reviewer, inquisitor, sprint-auditor) with no `Edit`/`Write` tool.
