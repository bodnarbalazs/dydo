---
area: understand
type: concept
---

# Architecture Overview

DynaDocs is a .NET 10 CLI that authors and validates durable project knowledge, compiles shared agent
methods into native host artifacts, and enforces universal guard rules. Linear sits outside the runtime
boundary and remains the sole owner of live project-management state.

## Main flows

1. `dydo init` scaffolds the documentation tree, template sources, runtime entry files, and guard hooks.
2. The host runtime sends matched tool calls to `dydo guard`.
3. The guard evaluates path tiers, dangerous commands, and configured nudges.
4. `dydo sync` compiles skill and resource templates into native Claude Code and Codex agents and skills.
5. `dydo template update` refreshes framework-owned documents.
6. `dydo check`, `dydo fix`, `dydo index`, and `dydo graph` maintain the durable documentation graph.

No step provisions, polls, caches, or mirrors Linear. Agents reach Linear through its official MCP, UI,
API, and integrations, outside dydo.

## Component layout

```text
Commands/        System.CommandLine factories and handlers
Services/        Documentation, configuration, template, and guard behavior
Models/          Configuration and parsing data types
Rules/           Documentation validation rules
Templates/       Embedded framework, skill, and resource sources
DynaDocs.Tests/  Unit, integration, E2E, and coverage gates
npm/             Native-binary npm wrapper
```

Services are instantiated directly; interfaces provide test seams without a dependency-injection
container. JSON serialization is source-generated for Native AOT compatibility.

## The compiler

`Templates/skill-<name>.template.md` is the role: its frontmatter carries the metadata, its body
carries the whole methodology. `dydo sync` discovers every shipped skill template, and emits:

| Output | Host | Emitted for |
|---|---|---|
| `.claude/skills/<role>/SKILL.md` and its `resources/` | Claude Code | every role |
| `.claude/agents/<role>.md` | Claude Code | roles that emit an agent |
| `.agents/skills/<role>/SKILL.md` and its `resources/` | Codex | every role; an `agents/openai.yaml` policy file joins it for explicit-only ones |
| `.codex/agents/<role>.toml` | Codex | roles that emit an agent |

DR 047 retires Workflow as an operating-model concept. Sync no longer discovers or emits workflows.
It removes only the retired `.claude/workflows/run-sprint.js` and `inquisition.js` root files,
preserves custom siblings and nested files, and removes the directory only when empty.
The guarantees this compilation owes a spawned agent are
[Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §10's;
what each frontmatter key compiles to is in [Customizing Roles](../guides/customizing-roles.md), and
the pipeline — update, cleanup — in
[Templates and Customization](./templates-and-customization.md).

The compiler-emitted agents, skills, and resources under `.claude/`, `.codex/`, and
`.agents/` are build products: change the source template and sync.

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
rules check titles, links, folder metadata, hubs, filenames, and project-specific invariants.
`dydo fix` applies supported repairs and `dydo graph` exposes navigation relationships.

## Key design choices

- **Dedicated live-work owner** — Linear manages volatile project state; dydo does not duplicate it.
- **Git-native durable knowledge** — decisions and proof stay reviewable at exact commits.
- **Host-native execution** — Claude Code and Codex own delegation, isolation, and lifecycle.
- **One authored source per role** — a single template compiles to both supported runtimes.
- **Universal guard rules** — enforcement is independent of any dydo-managed identity.
- **No DI framework** — direct construction keeps the Native AOT CLI small.

## Related

- [Work Model](./work-model.md) — Linear/Git operating contract
- [Templates and Customization](./templates-and-customization.md) — Authoring and compilation
- [Guard System](./guard-system.md) — Enforcement layers and the hook contract
- [Configuration](../reference/configuration.md) — Runtime configuration
- [Coding Standards](../guides/coding-standards.md) — Repository conventions
