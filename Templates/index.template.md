---
area: general
type: hub
---

# DynaDocs - AI Agent Entry Point

Documentation-driven context for AI coding assistants. dydo authors and knows your
project's structure; your platform (Claude Code, Codex) runs and coordinates the work.

---

## Start Here

Your platform entry file — `CLAUDE.md` (Claude Code) or `AGENTS.md` (Codex) — points you
into this tree. From there:

- **Understand the project** → [understand/about.md](understand/about.md), [understand/architecture.md](understand/architecture.md)
- **Know the conventions** → [guides/coding-standards.md](guides/coding-standards.md)
- **Find a command** → [reference/dydo-commands.md](reference/dydo-commands.md)

---

## The Guard

Every tool call passes through `dydo guard` (a `PreToolUse` hook). It enforces universal
rules for every agent:

- **Off-limits paths** (`files-off-limits.md`) — secrets and system files are blocked.
- **Dangerous-bash patterns** — destructive commands are always blocked.
- **Nudges** — configurable regex reminders that warn or block.

If the guard blocks you, re-read the relevant docs first — most blocks are a misuse, not a
wall. If you're still blocked, tell the human. Don't work around it.