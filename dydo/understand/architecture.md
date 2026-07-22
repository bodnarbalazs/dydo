---
area: understand
type: concept
---

# Architecture Overview

DynaDocs (`dydo`) is a .NET 10 CLI tool for documentation-driven AI agent orchestration. It enforces agent identity, universal off-limits + nudges, and workflow integrity by intercepting every tool call via Claude Code's `PreToolUse` hook — for the main thread and its subagents alike.

---

## How It Works

1. `dydo init` scaffolds a `dydo/` documentation tree and installs a `PreToolUse` hook into `.claude/settings.local.json`
2. Before every tool call (Read, Write, Edit, Bash, Glob, Grep), Claude Code pipes JSON to `dydo guard`
3. The guard enforces universal rules — off-limits file patterns, dangerous-bash detection, and nudges — the same for every caller
4. `dydo sync` compiles the mode templates into the platform's native skills, agents, and workflows; PM records (tasks, issues, sprints, decisions) live as Markdown under `dydo/project/`

---

## Stack

- **.NET 10** with Native AOT — self-contained binary, no runtime needed
- **System.CommandLine** — CLI framework
- **Markdig** — Markdown/frontmatter parsing
- **Filesystem as state store** — docs, PM records, and config are all files (Markdown + JSON). No database.

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

The guard is the enforcement backbone — three universal layers checked on every tool call, for every caller ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)):

1. **Off-limits patterns** (secrets, credentials, system files) hard-block all access.
2. **Dangerous-bash patterns** (fork bombs, `rm -rf /`, download-and-execute) are always blocked; bash chains are tokenized so each touched path is checked individually.
3. **Nudges** — configurable regex rules that notice, warn-once, or block.

`dydo init` also installs a `Stop` hook; it is a retained no-op (the agent-state machinery it drove was removed) so existing hook wiring keeps resolving.

See [Guard System](./guard-system.md) for the full model.

---

## Roles and Skills

The mode template **is** the role: `dydo sync` discovers roles by enumerating `mode-<name>.template.md` files (built-ins plus overrides in `dydo/_system/templates/`) and compiles each into the platform's skill — and, for worker roles, a spawnable agent whose tool profile comes from frontmatter (`read-only: true` ⇒ no Edit/Write). Skill resources (`<role>-resource-<name>.template.md`) compile into the skill's `resources/`, and `workflow-*.js` templates compile into `.claude/workflows/`.

See [Customizing Roles](../guides/customizing-roles.md) for the authoring guide.

---

## Audit Trail

**Removed in 2.0** ([Decision 024](../project/decisions/024-dydo-2-native-pivot.md)). dydo no longer writes its own JSON audit sessions or the HTML replay — Claude Code's native session transcripts are the record now. The `dydo audit` command is gone and the `dydo/_system/audit/` tree is legacy.

---

## Custom Nudges

Project-defined regex patterns evaluated in the guard pipeline alongside built-in rules. Each nudge matches against tool inputs and either blocks the operation (exit 2) or injects a warning (exit 0), extending the guard's enforcement model without modifying dydo's source code.

See [Configuration Reference](../reference/configuration.md) for nudge format and [Guard System](./guard-system.md) for severities and the shipped defaults.

---

## Issue Tracker

Issues are Markdown files with YAML frontmatter stored at `dydo/project/issues/`, following the same filesystem-as-database pattern as tasks and inbox items.

See [DynaDocs](../reference/about-dynadocs.md) for issue workflow details and [dydo Commands Reference](../reference/dydo-commands.md) for command usage.

---

## Attention Ledger

Replaces the old audit-derived "inquisition coverage" ([Decision 032](../project/decisions/032-attention-ledger-and-housekeeping-nudge.md)): a computed view — not maintained state — of when each area of the project was last looked at, derived from artifacts rather than the (now-removed) audit trail. Campaign-end QA still lands as inquisition reports (Markdown) at `dydo/project/inquisitions/`, produced by the `inquisition` workflow.

---

## Watchdog

The Notion-sync daemon (ns-13, the DR-041 repurpose — the old agent-lifecycle watchdog was deleted with the orchestration layer, [Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)). `dydo watchdog start` spawns a detached loop (`WatchdogService`, `Commands/WatchdogCommand.cs`) that fires one **cheap** sync tick every interval (15s default, 5s floor); `stop` kills it. A pid file under `_system/.local/` enforces a single instance and detects a stale one; the loop is single-flight (a tick never overlaps or queues behind a running one), dies only on a startup config error, and logs one summary line per tick to `_system/.local/watchdog.log`.

The tick is **O(changes), not O(corpus)** so a doc base 100× this repo syncs just as comfortably (`NotionSpineDelta`): each type's tick issues one server-side `last_edited_time`-filtered query for the pages edited on or after a stamp cursor, stat-walks the repo for changed files, and feeds only that changed-id union to the same reconcile engine the manual sync uses — untouched records are never read, parsed, or re-pushed. A body is re-read only for a page strictly newer than the cursor or edited within a short recency window (the same-minute-re-edit safety); an idle type's months-old newest page is never re-read, so a **steady quiet tick costs exactly one filtered query per type — zero body reads, zero provisioning probes** (the log line carries `requests` per tick). The cursor (max server stamp seen) and file mtimes persist beside the base snapshot (`NotionDeltaState`), keyed like the snapshots; the manual sync seeds them so a daemon starts warm, and a missing/corrupt file degrades to correctness (reconcile local changes, re-establish the cursor). Provisioning/schema validation runs only on a cadence (~every 20 ticks), on process start, and after any tick error — never every tick. Remote deletions — invisible to a filtered query — are caught by a periodic body-free **census** (default hourly), fuse-guarded; the true everything-reconcile stays with the manual `dydo notion sync`.

---

## Key Design Choices

- **Hook enforcement over trust** — every file operation is checked before execution, not after
- **Markdown-as-database** — tasks, issues, decisions, and changelogs are Markdown with YAML frontmatter (human-readable, git-diffable)
- **No DI framework** — services instantiated directly; interfaces exist for testability
- **Template overrides** — projects can customize any template at `dydo/_system/templates/` without forking
- **The template is the role** — roles are Markdown templates with frontmatter, discovered by enumeration; custom roles are just more templates

---

## Related

- [Coding Standards](../guides/coding-standards.md) — Code conventions
- [How to Use These Docs](../guides/how-to-use-docs.md) — Navigating the documentation
- [dydo Commands Reference](../reference/dydo-commands.md) — Full command documentation
- [Guard System](./guard-system.md) — Guard enforcement details
- [Work Model](./work-model.md) — How work is structured and run
- [Audit System](../reference/audit-system.md) — Audit trail (removed in 2.0; what replaced it)
- [DynaDocs](../reference/about-dynadocs.md) — Full feature overview and installation
- [Configuration Reference](../reference/configuration.md) — Configuration, nudges, and customization
