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
dydo init <integration> --join       # Wire up this machine's integration for an existing project
dydo init claude --name "Your Name"  # Non-interactive setup
dydo init codex --name "Your Name"   # Non-interactive Codex setup
```

**Arguments:**
- `integration` - Integration type: `claude` or `codex` (with hooks wired up), or `none` (for other systems, more setup needed)

**Options:**
- `--join` - Wire up this machine's hooks and entry files for an already-initialized project instead of creating a new one

### dydo sync

Compile the mode templates into native Claude Code and Codex artifacts — Claude `.claude/agents/<role>.md` / `.claude/skills/<role>/SKILL.md` outputs plus Codex `.codex/agents/<role>.toml` / `.agents/skills/<role>/SKILL.md` outputs.

```bash
dydo sync   # emit Claude and Codex agents/skills from roles + docs
```

**Behavior:**
- Roles are discovered from the mode templates (`mode-<name>.template.md`, built-in plus `dydo/_system/templates/` overrides); the template's frontmatter declares `description`, `emit` (agent+skill vs skill-only), and `read-only`.
- Custom role = drop a `mode-<name>.template.md` into `dydo/_system/templates/` and re-run `dydo sync`.
- Skill resources (`<role>-resource-<name>.template.md`) compile into the skill's `resources/` folder; workflow harnesses (`workflow-<name>.js`) compile to `.claude/workflows/`.
- Codex outputs are written to `.codex/agents/` and `.agents/skills/`.
- Model tiers declared per role are bound to concrete models at sync time (`models` in `dydo.json`).

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
- `--stop` - Stop-hook mode: retained no-op so existing Stop-hook wiring keeps resolving (always exits 0)

**Exit codes:** 0 = allowed, 2 = blocked.

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

**Prerequisite:** The task must be in `in-review` state. Run `dydo task ready-for-review <task> --summary "..."` to transition it.

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

## Validation Commands

### dydo validate

Validate the dydo configuration.

```bash
dydo validate
```

**Validates:** `dydo.json` — schema and value ranges, plus nudge definitions (pattern validity, severity values).

---

## Model Commands

Time-boxed operational swaps for a model outage. When a tier's bound model becomes unavailable — the canonical case is a weekly spend cap the API blocks with no retry and no native fallback — cap it to a fallback so gated work keeps running, then let it auto-restore.

### dydo model cap

Temporarily rebind every tier using an unavailable model to a fallback model, then re-sync native agent definitions. A local marker records what to restore; the guard's housekeeping puts the original bindings back on its next trigger after the reset time passes, or run `dydo model uncap` to restore on demand.

```bash
dydo model cap claude-fable-5 --until "07-14 09:00"
dydo model cap claude-fable-5 --until "2026-07-14 09:00" --fallback claude-opus-4-1
```

**Arguments:**
- `model` - Unavailable model id to cap.

**Options:**
- `--until <time>` - Local reset time from the limit error, as `[yyyy-]mm-dd hh:mm` (required).
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

Reconcile the sync model's object types (default `Release` → `Campaign` → `Sprint` → `Slice` → `Issue` → `Task` → `FutureFeature`) against Notion bidirectionally, provisioning one Notion database per object type under a parent page. Requires a `DYDO_NOTION_TOKEN` integration token and a parent page from `notion.parentPageId` in dydo.json or the `DYDO_NOTION_PARENT_PAGE` environment variable. Use `--dry-run` to print the reconcile plan without applying it.

The sync model owns the schema shape one-way (project → Notion): data values sync both ways, but which properties and select options exist flows only from the model. Schema drift — a property or select option added in Notion but absent from the model — is reported as a warning and left untouched by default. Pass `--prune` to delete it instead (rogue properties are removed; a drifted select's options are reset to the model's set). A rogue option's stored value still round-trips as data; only the schema option is pruned.

Alongside the PM spine, sync can also mirror the browsable docs tree to a nested-page hierarchy under the same parent (a `Docs` page). The mirror is **opt-in**: the plain `dydo notion sync` runs the spine only. Pass `--docs` to run the spine plus the docs mirror, `--docs-only` for the mirror alone (never touches the PM board), or `--spine-only` for the explicit spine-only scope (the default); `--docs-only` and `--spine-only` are mutually exclusive. Pass `--parent-page <page-id>` to mirror under an explicit page, overriding `notion.parentPageId` / `DYDO_NOTION_PARENT_PAGE` — e.g. to smoke-test the docs mirror against a scratch page.

A mass-delete fuse guards the repo: if a reconcile would locally delete more than 5 of a type's records **and** more than 20% of the type's tracked records (a poisoned snapshot, or a Notion-side mass archive), that type's apply aborts loudly, lists the would-be-deleted files, and the run exits non-zero — no other type is affected. Pass `--allow-mass-delete` to disable the fuse and apply the deletions anyway when they are intended.

```bash
dydo notion sync
dydo notion sync --dry-run
dydo notion sync --prune
dydo notion sync --docs
dydo notion sync --docs-only --parent-page <scratch-page-id>
dydo notion sync --spine-only
dydo notion sync --allow-mass-delete
```

### dydo notion reset

Wipe the tracked Notion databases and recreate them fresh from the sync model. Unlike `dydo notion sync` — a forward, create-only reconcile that never renames a database, restores a deleted view, or reverts a manual layout edit — a reset makes the live board match the model again regardless of manual mess. It archives (trashes) the tracked databases by their recorded ids, clears the provision state, then re-runs the normal spine provision, re-minting every database and re-pushing every repo doc. The archive happens **before** the state is cleared so the old databases are never orphaned into duplicates. Notion has no hard delete, so the wipe archives to Notion Trash — you can restore from there if needed. This is destructive to board data, so it confirms interactively first (pass `--yes` to skip). Use `--dry-run` to print the archive + recreate plan without touching Notion, and `--parent-page <page-id>` to recreate under an explicit page (e.g. a throwaway scratch workspace). If a reset is interrupted mid-run, re-run `dydo notion reset` (it is idempotent) rather than `dydo notion sync` — sync would try to reuse an already-archived database.

```bash
dydo notion reset --dry-run
dydo notion reset
dydo notion reset --yes --parent-page <scratch-page-id>
```

### dydo watchdog

Run a background daemon that keeps the Notion board current without manual `dydo notion sync`. `dydo watchdog start` spawns a detached loop that fires one **cheap** sync tick every interval (default 15s, floor 5s via `--interval <seconds>`); `dydo watchdog stop` ends it. A single instance is enforced by a pid file (`dydo/_system/.local/watchdog.pid`): a second `start` while one is live refuses, naming the running pid; a stale pid file (its process gone) is cleared automatically. The daemon refuses to start on a config error (no token, no dydo project, no configured parent page) but never dies on a sync/API error — it logs to `dydo/_system/.local/watchdog.log` and retries the next tick. It syncs the spine only, against the configured parent (the same token/parent resolution as `dydo notion sync`).

A tick is **O(changes), not O(corpus)** — a doc base 100× this repo syncs just as comfortably. Each tick asks Notion only for pages edited on or after its stamp cursor (one server-side filtered query, returning just the newest boundary page(s) on a quiet tick at any board size) and stat-walks the repo for changed files, then reconciles only that changed set; a quiet tick reads only its boundary pages (typically 0-2, which reconcile to no-ops), never the corpus. Because a filtered query cannot see archived pages, remote deletions are caught by a periodic **census** (`--census-interval <ticks>`, default 240 ≈ hourly) — a body-free id/stamp pagination whose disappeared ids surface archives, fuse-guarded. The full everything-reconcile stays with the manual `dydo notion sync`, run rarely and on purpose (which also seeds the daemon's cheap-tick state, so a watchdog started afterwards begins warm). `dydo watchdog run` is the foreground loop itself, used by `start`.

```bash
dydo watchdog start
dydo watchdog start --interval 30 --census-interval 120
dydo watchdog stop
```

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DYDO_NOTION_TOKEN` | Notion integration token enabling `dydo notion sync` |
| `DYDO_NOTION_PARENT_PAGE` | Notion parent page the `dydo notion sync` spine databases live under (overridden by `notion.parentPageId` in dydo.json) |


---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success / Action allowed |
| 1 | Validation errors found |
| 2 | Tool error / Action blocked |

