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
4. `dydo sync` compiles skill, resource, and workflow templates into native Claude Code and Codex artifacts.
5. `dydo template update` refreshes framework-owned documents and the project's template copies.
6. `dydo check`, `dydo fix`, `dydo index`, and `dydo graph` maintain the durable documentation graph.

No step provisions, polls, caches, or mirrors Linear. Agents reach Linear through its official MCP, UI,
API, and integrations, outside dydo.

## Component layout

```text
Commands/        System.CommandLine factories and handlers
Services/        Documentation, configuration, template, and guard behavior
Models/          Configuration and parsing data types
Rules/           Documentation validation rules
Templates/       Embedded framework, skill, resource, and workflow sources
DynaDocs.Tests/  Unit, integration, E2E, and coverage gates
npm/             Native-binary npm wrapper
```

Services are instantiated directly; interfaces provide test seams without a dependency-injection
container. JSON serialization is source-generated for Native AOT compatibility.

## The compiler

`Templates/skill-<name>.template.md` is the role: its frontmatter carries the metadata, its body
carries the whole methodology. `dydo sync` discovers every shipped skill template plus any project-local
one under `dydo/_system/templates/`, and emits:

| Output | Host | Emitted for |
|---|---|---|
| `.claude/skills/<role>/SKILL.md` and its `resources/` | Claude Code | every role |
| `.claude/agents/<role>.md` | Claude Code | roles that emit an agent |
| `.agents/skills/<role>/SKILL.md` and its `resources/` | Codex | every role; an `agents/openai.yaml` policy file joins it for explicit-only ones |
| `.codex/agents/<role>.toml` | Codex | roles that emit an agent |
| `.claude/workflows/<name>.js` from `Templates/workflow-<name>.js` | Claude Code | the only host with a workflow surface |

Four guarantees the compiler owes a spawned agent
([Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §10):

- **The methodology arrives.** Every authored section survives into the compiled skill, `## Must-Reads`
  included, and an agent definition is a thin identity wrapper that preloads its skill — `skills:` and
  the `Skill` tool on Claude; on Codex, whose agents have no preload, an instruction to load the skill
  by name.
- **Its pointers resolve.** Links in the compiled body are rewritten to resolve from the folder the
  skill was emitted into, and a `resources/<name>.md` link becomes the host's emitted path, so an
  agent holding a preloaded skill with no folder to resolve against can still read its rubric.
- **Tools match the declaration.** `read-only` withholds Edit and Write, `delegates` alone grants the
  Agent tool so a worker cannot fan out, and `invocation: explicit` emits each host's opt-out from
  model invocation.
- **Retirement is swept.** A role, workflow, or resource dydo itself retires is removed from every
  host's output on the next sync, so nothing orphaned keeps loading its description.

`dydo template update` mirrors shipped skill and resource templates plus the framework-owned documents
into the project and tracks each by content hash. Everything under `.claude/`, `.codex/`, and
`.agents/` is a build product: change the source template and sync. See
[Templates and Customization](./templates-and-customization.md) for the frontmatter keys and the
update flow.

## Knowledge and work boundary

Linear owns Initiatives, Projects, Issues, optional Milestones and Cycles, and every live field:
status, priority, assignment, dependencies, updates, review state.

Git and dydo own architecture, Decisions, reviewed Project plans, guides, audits, inquisitions,
assimilation briefs, changelog, release tags, pitfalls, and repo-native FutureFeatures. Branches,
worktrees, sessions, native sub-agents, commits, pull requests, and review passes are execution
evidence linked to a Linear Issue, not work-record types. The [Work Model](./work-model.md) states the
contract; the [Linear Issue Lifecycle](./task-lifecycle.md) states how one Issue moves through it.

## Guard system

Three universal layers, applied to every caller: path tiers (off-limits paths that no tool may even
read, and protected paths that every tool may read and none may write), dangerous-command detection for
destructive shell patterns, and configurable nudges that notice, warn, or block.

The host platform owns identity and permissions. dydo maintains no agent roster, scheduler, queue, or
worktree manager: the [Working-Tree Contract](../guides/working-tree-contract.md) is a procedure agents
follow, not machinery the CLI runs. See [Guard System](./guard-system.md) for the wire contract.

## Documentation graph

Markdown files carry frontmatter and relative links. The scanner builds the document set; validation
rules check summaries, links, folder metadata, hubs, filenames, and project-specific invariants.
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
