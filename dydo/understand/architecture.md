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
Services/        Documentation, configuration, template, and guard behavior
Models/          Configuration and parsing data types
Rules/           Documentation validation rules
Templates/       Embedded framework document templates
DynaDocs.Tests/  Unit, integration, E2E, and coverage gates
npm/             Native-binary npm wrapper
```

Services are instantiated directly; interfaces provide test seams without a dependency-injection
container. JSON serialization is source-generated for Native AOT compatibility.

## Native skills

A role is a plain `SKILL.md` folder authored directly in the cross-vendor
[agentskills.io](https://agentskills.io) format — there is no compile step
([Decision 049](../project/decisions/049-skills-are-the-source-retire-the-compiler.md)). The
canonical folder is `.claude/skills/<role>/SKILL.md`; its committed Codex copy is
`.agents/skills/<role>/SKILL.md`, joining `agents/openai.yaml` where explicit invocation or an
argument hint is declared. Both hosts read the body where it lives.

Committed per-host copies were chosen over symlinks because a checkout with symlinks disabled
materialises a link as a text file, stranding the host. DR 047 retires Workflow as an operating-model
concept; no workflow scripts exist. What each frontmatter key means is in
[Customizing Roles](../guides/customizing-roles.md), the shapes and link rules in
[Templates and Customization](./templates-and-customization.md).

The skill folders under `.claude/skills/` and `.agents/skills/` are the source: edit them directly.

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

The host platform owns identity and permissions. dydo maintains no agent roster, scheduler, queue, or
worktree manager: the [Working-Tree Contract](../guides/working-tree-contract.md) is a procedure agents
follow, not machinery the CLI runs. See [Guard System](./guard-system.md) for the wire contract.

## Documentation graph

Markdown files carry frontmatter and relative links. The scanner builds the document set; validation
rules check titles, links, filenames, and project-specific invariants.
`dydo fix` applies supported repairs and `dydo graph` exposes navigation relationships.

## Key design choices

- **Dedicated live-work owner** — Linear manages volatile project state; dydo does not duplicate it.
- **Git-native durable knowledge** — decisions and proof stay reviewable at exact commits.
- **Host-native execution** — Claude Code and Codex own delegation, isolation, and lifecycle.
- **Committed native skills per host** — one role, authored as a plain `SKILL.md` folder in each host's discovery path.
- **Universal guard rules** — enforcement is independent of any dydo-managed identity.
- **No DI framework** — direct construction keeps the Native AOT CLI small.

## Related

- [Work Model](./work-model.md) — Linear/Git operating contract
- [Templates and Customization](./templates-and-customization.md) — Authoring and compilation
- [Guard System](./guard-system.md) — Enforcement layers and the hook contract
- [Configuration](../reference/configuration.md) — Runtime configuration
- [Coding Standards](../guides/coding-standards.md) — Repository conventions
