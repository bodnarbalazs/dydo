---
area: understand
type: concept
---

# Architecture Overview

DynaDocs is a .NET 10 CLI that authors and validates durable project knowledge, authors shared agent
methods as native host skills, and enforces universal guard rules. Linear sits outside the runtime
boundary and remains the sole owner of live project-management state.

## Main flows

1. `dydo init` scaffolds the documentation tree, runtime entry files, and guard hooks.
2. The host runtime sends matched tool calls to `dydo guard`.
3. The guard evaluates path tiers, dangerous commands, and configured nudges.
4. `dydo check`, `dydo fix`, `dydo index`, and `dydo graph` maintain the durable documentation graph.

No step provisions, polls, caches, or mirrors Linear. Agents reach Linear through its official MCP, UI,
API, and integrations, outside dydo.

## Component layout

```text
Commands/        System.CommandLine factories and handlers
Services/        Documentation, configuration, scaffold, and guard behavior
Models/          Configuration and parsing data types
Rules/           Documentation validation rules
Scaffold/        The project tree `dydo init` copies out, embedded in the binary
DynaDocs.Tests/  Unit, integration, E2E, and coverage gates
npm/             Native-binary npm wrapper
```

Services are instantiated directly; interfaces provide test seams without a dependency-injection
container. JSON serialization is source-generated for Native AOT compatibility.

## Native skills

A skill is a plain `SKILL.md` folder authored directly in the cross-vendor
[agentskills.io](https://agentskills.io) format — there is no compile step
([Decision 049](../project/decisions/049-skills-are-the-source-retire-the-compiler.md)). The
canonical folder is `skills/<category>/<name>/`, sorted by kind
([Decision 050](../project/decisions/050-officers-crew-and-skills-hats-retired.md)): the roles under
`roles/officers/` and `roles/crew/`, every other skill under `engineering/` or `productivity/`. It
keeps Claude's frontmatter, resources, and Codex's `agents/openai.yaml` together. The
dependency-free `setup-skills.mjs` walks the tree by rule — a folder holding `SKILL.md` is a skill,
any other folder a category — and creates one directory symlink or Windows junction per skill in
`.claude/skills/` and `.agents/skills/`, flat with no category level. OpenCode is not a supported host: it may read those two roots, but
that is untested, dydo has no OpenCode init mode, and setup creates no third projection. DR 047 retires
Workflow as an operating-model concept; no workflow scripts exist. What each metadata key means is in
[Customizing Roles](../guides/customizing-roles.md), the shapes and link rules in
[Scaffold and Customization](./scaffold-and-customization.md).

The folders under `skills/` are the only editable skill source. Host projections are ignored local setup.

## Knowledge and work boundary

Linear owns Initiatives, Projects, Issues, optional Milestones and Cycles, and every live field:
status, priority, assignment, dependencies, updates, review state.

Git and dydo own architecture, Decisions, reviewed Project plans, guides, audits, inquisitions,
assimilation briefs, changelog, release tags, and pitfalls. Linear owns FutureFeatures with the rest
of the work graph. Branches, worktrees, sessions, native sub-agents, commits, pull requests, and
review passes are execution evidence linked to a Linear Issue, not work-record types. The
[Work Model](./work-model.md) states the contract; the [Linear Issue Lifecycle](./task-lifecycle.md)
states how one Issue moves through it.

## Guard system

Three universal layers, applied to every caller: path tiers (off-limits paths that no tool may even
read, and protected paths that every tool may read and none may write), dangerous-command detection for
destructive shell patterns, and configurable nudges that notice, warn, or block.

The host platform owns identity and permissions. A sandbox value in native configuration requests a
restriction; only observed host behaviour establishes that the runtime enforces it. dydo maintains no
agent roster, scheduler, queue, or worktree manager: the
[Working-Tree Contract](../guides/working-tree-contract.md) is a procedure agents follow, not machinery
the CLI runs. See [Guard System](./guard-system.md) for the wire contract.

## Documentation graph

Markdown files carry frontmatter and relative links. The scanner builds the document set; validation
rules check titles, links, filenames, and project-specific invariants.
`dydo fix` applies supported repairs and `dydo graph` exposes navigation relationships.

## Key design choices

- **Dedicated live-work owner** — Linear manages volatile project state; dydo does not duplicate it.
- **Git-native durable knowledge** — decisions and proof stay reviewable at exact commits.
- **Host-native execution** — Claude Code and Codex own delegation, isolation, and lifecycle.
- **One canonical native skill tree** — host discovery paths point at the same authored role folders.
- **Universal guard rules** — enforcement is independent of any dydo-managed identity.
- **No DI framework** — direct construction keeps the Native AOT CLI small.

## Related

- [Work Model](./work-model.md) — Linear/Git operating contract
- [Scaffold and Customization](./scaffold-and-customization.md) — Authoring and customization
- [Guard System](./guard-system.md) — Enforcement layers and the hook contract
- [Configuration](../reference/configuration.md) — Runtime configuration
- [Coding Standards](../guides/coding-standards.md) — Repository conventions
