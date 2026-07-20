---
area: understand
type: context
---

# About This Project

DynaDocs (dydo) is a documentation-driven agent orchestration framework for AI coding assistants. AI tools have memory features, but that memory is unstructured, opaque, and not under your control. DyDo gives you explicit, structured control over project context — your documentation is the versioned source of truth, with a CLI that enforces identity, roles, and permissions so agents stay on track.

This is the DyDo project itself. If you're an agent, this is the framework that orchestrates you — and this documentation tree is both the project's knowledge base and a living example of the system.

---

## What DyDo Does

- **Documentation as memory** — agents onboard themselves each session by reading structured docs
- **Guard enforcement** — a `PreToolUse` hook checks every tool call (main thread *and* subagents) against universal off-limits and custom nudges
- **Native orchestration** — `dydo sync` compiles roles and docs into Claude Code's native agents, skills, and workflows; light Tier-1 messaging coordinates the agents you talk to
- **Data-driven roles** — seven base roles (code-writer, reviewer, docs-writer, etc.) with customizable permissions; add your own
- **Optional Notion sync** — a two-way team PM board view over your canonical repo files

---

## Tech Stack

.NET 10 CLI with Native AOT (self-contained binary). Filesystem as state store — no database. Everything (docs, PM records, config) is Markdown or JSON files, human-readable and git-diffable.

---

## Related

- [Architecture](./architecture.md) — Technical structure and design choices
- [About DynaDocs](../reference/about-dynadocs.md) — Full feature overview and installation
