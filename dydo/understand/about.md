---
area: understand
type: context
must-read: true
---

# About This Project

DynaDocs (dydo) is a documentation-driven agent orchestration framework for AI coding assistants. AI tools have memory features, but that memory is unstructured, opaque, and not under your control. DyDo gives you explicit, structured control over project context — your documentation is the versioned source of truth, with a CLI that enforces identity, roles, and permissions so agents stay on track.

This is the DyDo project itself. If you're an agent, this is the framework that orchestrates you — and this documentation tree is both the project's knowledge base and a living example of the system.

---

## What DyDo Does

- **Documentation as memory** — agents onboard themselves each session by reading structured docs
- **Guard enforcement** — a `PreToolUse` hook checks every file operation against role permissions
- **Agent orchestration** — dispatch, inbox, messaging, and task tracking coordinate multi-agent workflows
- **Role-based access** — nine base roles (code-writer, reviewer, docs-writer, etc.) with customizable permissions
- **Audit trail** — every agent action recorded for replay and accountability

---

## Tech Stack

.NET 10 CLI with Native AOT (self-contained binary). Filesystem as state store — no database. All state (agent identity, tasks, inbox, audit) is Markdown or JSON files, human-readable and git-diffable.

---

## Related

- [Architecture](./architecture.md) — Technical structure and design choices
- [About DynaDocs](../reference/about-dynadocs.md) — Full feature overview and installation
