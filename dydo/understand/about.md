---
area: understand
type: context
---

# About This Project

DynaDocs (dydo) is a documentation, skill-authoring, and guardrail framework for AI coding assistants.
It makes durable project context explicit and versioned, then compiles shared methods for native coding
agent runtimes. Linear owns live project management; dydo/Git owns knowledge and reviewed proof.

This repository is both the dydo implementation and a living example of its documentation model. Claude
Code and Codex own runtime identity, permissions, process lifecycle, worktree isolation, and native
agent coordination.

## What dydo does

- **Documentation as memory** — agents onboard from structured, reviewable project knowledge.
- **Native-runtime compilation** — `dydo sync` compiles shared roles, skills, resources, and workflows.
- **Guard enforcement** — `dydo guard` applies universal off-limits rules and project nudges.
- **Documentation tooling** — `dydo check`, `dydo fix`, indexes, and graph commands keep knowledge usable.
- **Reviewed delivery knowledge** — Decisions, Project plans, audits, and assimilation evidence remain in Git.

dydo does not manage Linear objects or mirror their state. A fresh project scaffolds durable knowledge
folders and FutureFeature idea documentation, not a repository-backed work hierarchy.

## Technology

The product is a .NET 10 CLI with Native AOT, System.CommandLine, Markdig, and source-generated JSON.
Markdown and JSON are durable local state; Linear remains the external system of record for current work.

## Related

- [Architecture](./architecture.md) — Component and boundary overview
- [DynaDocs](../reference/about-dynadocs.md) — Product overview and setup
- [Work Model](./work-model.md) — Linear/Git operating contract
