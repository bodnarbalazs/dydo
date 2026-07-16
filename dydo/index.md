---
area: general
type: hub
---
# DynaDocs - AI Agent Entry Point

Documentation-driven context for AI coding assistants. dydo authors and knows; the
platform (Claude Code, Codex) runs and coordinates ([Decision 041](project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)).

---

## Start Here

Your platform entry file — `CLAUDE.md` (Claude Code) or `AGENTS.md` (Codex) — points you
into this tree. From there:

- **Understand the project** → [understand/about.md](understand/about.md), [understand/architecture.md](understand/architecture.md)
- **Know the conventions** → [guides/coding-standards.md](guides/coding-standards.md)
- **Find a command** → [reference/dydo-commands.md](reference/dydo-commands.md)
- **Navigate the docs** → [guides/how-to-use-docs.md](guides/how-to-use-docs.md)

There is no identity to claim: sessions are assigned their identity by the platform, and
work runs as native subagents and workflows, not a dydo-managed roster.

---

## The Guard

Every tool call passes through `dydo guard` (a `PreToolUse` hook). It enforces only
universal rules — no per-agent identity gates:

- **Off-limits paths** (`files-off-limits.md`) — secrets and system files are blocked for everyone.
- **Dangerous-bash patterns** — destructive commands (e.g. `rm -rf /`, fork bombs) are always blocked.
- **Nudges** — configurable regex reminders that warn or block (see [reference/guardrails.md](reference/guardrails.md)).

If the guard blocks you, re-read the relevant docs first — most blocks are a misuse, not a
wall. If you're still blocked, tell the human. Don't work around it.