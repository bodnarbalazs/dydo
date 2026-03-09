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

## Key Design Choices

- **Hook enforcement over trust** — every file operation is checked before execution, not after
- **Markdown-as-database** — tasks, agent state, and changelogs are Markdown with YAML frontmatter (human-readable, git-diffable)
- **No DI framework** — services instantiated directly; interfaces exist for testability
- **Template overrides** — projects can customize any template at `dydo/_system/templates/` without forking

---

## Related

- [Coding Standards](../guides/coding-standards.md) — Code conventions
- [How to Use These Docs](../guides/how-to-use-docs.md) — Navigating the documentation
- [dydo Commands Reference](../reference/dydo-commands.md) — Full command documentation
