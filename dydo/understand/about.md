---
area: understand
type: context
---

# About This Project

DynaDocs (dydo) is a documentation-driven context, project-management, skill-authoring, and
guardrail framework for AI coding assistants. AI tools have memory features, but that memory is
unstructured, opaque, and not under your control. dydo makes project context explicit and
versioned, then compiles its durable guidance for native coding-agent runtimes.

This is the dydo project itself. This documentation tree is both the project's knowledge base
and a living example of the system. dydo authors and synchronizes context and skills; Claude Code
and Codex own runtime identity, permissions, process lifecycle, and native subagent coordination.

---

## What DyDo Does

- **Documentation as memory** — agents onboard themselves each session by reading structured docs
- **Guard enforcement** — a `PreToolUse` hook checks every tool call (main thread *and* subagents) against universal off-limits and custom nudges
- **Native-runtime compilation** — `dydo sync` compiles shared role and skill sources into native
  Claude Code and Codex artifacts; the host runtime coordinates execution
- **Data-driven roles** — seven base roles (code-writer, reviewer, docs-writer, etc.) with customizable permissions; add your own
- **Optional Notion sync** — a two-way team PM board view over your canonical repo files

---

## Tech Stack

.NET 10 CLI with Native AOT (self-contained binary). Filesystem as state store — no database. Everything (docs, PM records, config) is Markdown or JSON files, human-readable and git-diffable.

---

## Related

- [Architecture](./architecture.md) — Technical structure and design choices
- [About DynaDocs](../reference/about-dynadocs.md) — Full feature overview and installation
