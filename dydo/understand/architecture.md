---
area: understand
type: concept
must-read: true
---

# Architecture Overview

DynaDocs (`dydo`) is a .NET 10 CLI tool for documentation-driven AI agent orchestration. It enforces agent identity, role-based file permissions, and workflow integrity by intercepting every file operation via Claude Code's `PreToolUse` hook.

---

## How It Works

1. `dydo init` scaffolds a `dydo/` documentation tree and installs a `PreToolUse` hook into `.claude/settings.local.json`
2. Before every tool call (Read, Write, Edit, Bash, Glob, Grep), Claude Code pipes JSON to `dydo guard`
3. The guard enforces staged onboarding (claim identity → set role → read must-reads → work), role-based write permissions, off-limits file patterns, and bash command safety checks
4. Agents dispatch work to each other via inbox items; a task lifecycle tracks work through to approval

---

## Stack

- **.NET 10** with Native AOT — self-contained binary, no runtime needed
- **System.CommandLine** — CLI framework
- **Markdig** — Markdown/frontmatter parsing
- **Filesystem as state store** — agent state, tasks, inbox, and audit logs are all files (Markdown + JSON). No database.

---

## Project Layout

```
Commands/        CLI command handlers (static classes, factory pattern)
Services/        Business logic (interfaces + implementations, no DI container)
Models/          Data types and config (source-generated JSON for AOT)
Rules/           Documentation validation rules (IRule implementations)
Templates/       Embedded resource templates (overridable via dydo/_system/templates/)
DynaDocs.Tests/  Unit, integration, and E2E tests
npm/             npm wrapper — downloads native binary per platform
```

---

## Guard System

The guard is the enforcement backbone — a staged access model checked on every tool call:

| Stage | Condition | Can Do |
|-------|-----------|--------|
| 0 | No identity | Read bootstrap files only (`index.md`, `workflow.md`) |
| 1 | Claimed, no role | Read bootstrap + own mode files |
| 2 | Claimed + role set | Read everything, write per role permissions |

Additional layers: off-limits patterns (secrets, credentials) block all agents globally; dangerous bash patterns (fork bombs, `rm -rf /`) are always blocked; must-read enforcement blocks writes until critical docs are read.

---

## Role System

Nine base roles with data-driven permissions defined in `.role.json` files under `dydo/_system/roles/`. Each role specifies writable paths, read-only paths, a mode template, and optional constraints (e.g., reviewer cannot review their own code-writer work on the same task).

Custom roles: add a `.role.json` file with `"base": false`. Path patterns use `{source}`, `{tests}`, and `{self}` placeholders resolved from `dydo.json` and agent identity.

---

## Dispatch and Messaging

Agents hand off work via `dydo dispatch`, which writes an inbox item and launches a new terminal. The dispatching agent can `--wait` (blocking until a response) or `--no-wait` (fire-and-forget). Agents communicate directly via `dydo msg` and `dydo wait`.

A task lifecycle tracks work through states: pending → in-progress → review-pending → approved/rejected. Dispatching with `--role reviewer` auto-transitions to review-pending.

---

## Audit Trail

Every agent session produces a JSON audit file in `dydo/_system/audit/YYYY/`. Sessions capture a project snapshot (files, folders, doc links) and timestamped events (reads, writes, blocks, role changes). Compaction reduces storage via baseline+delta compression. An HTML replay visualization shows agent activity as an animated timeline.

---

## Worktree Dispatch

`dispatch --worktree` creates an isolated git worktree so agents work on separate branches without interfering with each other or the main working tree. The lifecycle:

1. A branch `worktree/{id}` is created from the current HEAD
2. A worktree directory is set up at `dydo/_system/.local/worktrees/{id}`
3. A junction/symlink to `dydo/agents/` shares the agent registry across worktrees
4. Workspace markers (`.worktree`, `.worktree-path`, `.worktree-base`) track the worktree state

Child dispatches from within a worktree inherit the same worktree instead of creating a new one. Worktrees are cleaned up on agent release via `dydo worktree cleanup`, with `git worktree prune` handling orphans.

---

## Dispatch Queue

The `--queue <name>` flag on dispatch serializes terminal launches through named queues. When a queue already has an active item, new dispatches are deferred — agent selection and inbox delivery happen immediately, but the terminal launch waits until the active item completes.

A watchdog process monitors queue state, advancing deferred items when the active agent releases. This prevents resource contention when orchestrators dispatch multiple agents that need sequential access (e.g., a merge queue).

---

## Custom Nudges

Projects can define coaching hints in `dydo.json` that trigger when agent actions match configurable patterns. Each nudge has:

- A **regex pattern** matched against tool inputs
- A **severity** level: `block` (exit 2, hard stop) or `warn` (exit 0, guidance injected)
- A **message** explaining the correct approach

Nudges are evaluated in the guard pipeline alongside built-in rules. They're useful for project-specific conventions that aren't covered by base guardrails — e.g., blocking direct database writes or warning about deprecated API patterns.

---

## Conditional Must-Reads

Some documentation is only relevant for specific roles or contexts. Conditional must-reads are dynamic — they're injected into an agent's required reading list based on:

- **Role**: Merge guides are required for code-writers working in worktrees
- **Task context**: Reviewers are required to read the task file for the task they're reviewing

This extends the base `must-read: true` frontmatter mechanism with runtime conditions evaluated by the guard.

---

## Issue Tracker

A lightweight issue management system at `dydo/project/issues/`. Issues are Markdown files with YAML frontmatter tracking title, area, severity, status, and provenance.

Issues integrate with the inquisition pipeline — confirmed findings from inquisitor agents are promoted to issues via `dydo issue create --found-by inquisition`. Judges can also create issues during dispute arbitration. Issues are resolved with `dydo issue resolve <id> --summary "..."`.

---

## Inquisition Coverage

File-level heatmaps tracking which areas of the codebase have been audited by inquisitor agents. Rolling per-area reports live at `dydo/project/inquisitions/`. The `dydo inquisition coverage` command provides a summary view of coverage across project areas, highlighting gaps and staleness.

---

## Watchdog

A background monitoring process that handles lifecycle events for dispatched agents:

- **Auto-close**: When `--auto-close` is set on dispatch, the watchdog polls the target agent's state every 10 seconds. When the agent releases, the watchdog closes its terminal.
- **Queue advancement**: Monitors named dispatch queues and launches deferred terminals when the active item completes.
- **Orphan detection**: Identifies agents that are stuck in Working state without an active process (parent PID liveness checks).

The watchdog runs as a background process spawned by the dispatch command. It's stateless — all state is derived from the filesystem (agent state files, queue markers).

---

## Key Design Choices

- **Hook enforcement over trust** — every file operation is checked before execution, not after
- **Markdown-as-database** — tasks, agent state, and changelogs are Markdown with YAML frontmatter (human-readable, git-diffable)
- **No DI framework** — services instantiated directly; interfaces exist for testability
- **Template overrides** — projects can customize any template at `dydo/_system/templates/` without forking
- **Data-driven roles** — role permissions defined in JSON, not hardcoded; custom roles via the same mechanism

---

## Related

- [Coding Standards](../guides/coding-standards.md) — Code conventions
- [How to Use These Docs](../guides/how-to-use-docs.md) — Navigating the documentation
- [dydo Commands Reference](../reference/dydo-commands.md) — Full command documentation
- [Guard System](./guard-system.md) — Guard enforcement details
- [Roles and Permissions](./roles-and-permissions.md) — Role system in depth
- [Dispatch and Messaging](./dispatch-and-messaging.md) — Agent communication
- [Audit System](../reference/audit-system.md) — Audit trail reference
