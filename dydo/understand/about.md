---
area: understand
type: context
---

# About This Project

DynaDocs (dydo) is a documentation, skill-authoring, and guardrail framework for AI coding assistants.
It makes durable project context explicit and versioned, then authors shared methods as native skills
for coding agent runtimes. Linear owns live project management; dydo/Git owns knowledge, temporary feature specs and reviewed proof.

This repository is both the dydo implementation and a living example of its documentation model. Claude
Code and Codex own runtime identity, permissions, process lifecycle, worktree isolation, and native
agent coordination.

## What dydo does

- **Documentation as memory** - agents onboard from structured, reviewable project knowledge.
- **Native skill authoring** - skills are plain `skills/<category>/<name>/` folders, edited
  directly and exposed flat through each host's discovery roots by `setup-skills.mjs`; there is no
  compile step.
- **Guard enforcement** - `dydo guard` applies universal off-limits rules and project nudges.
- **Documentation tooling** - `dydo check`, `dydo fix`, indexes, and graph commands keep knowledge usable.
- **Reviewed delivery knowledge** - Decisions, Project plans, audits, and assimilation evidence remain in Git.

dydo does not manage Linear objects or mirror their state. A fresh project scaffolds durable knowledge,
with optional [working specs](../reference/working-specs/_working-specs.md); FutureFeatures live in Linear.

## Technology

The product is a .NET 10 CLI with Native AOT, System.CommandLine, Markdig, and source-generated JSON.
Markdown and JSON are durable local state; Linear remains the external system of record for current work.

## Related

- [Architecture](./architecture.md) - Component and boundary overview
- [DynaDocs](../reference/about-dynadocs.md) - Product overview and setup
- [Work Model](./work-model.md) - Linear/Git operating contract
