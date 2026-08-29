---
area: understand
type: concept
---

# Architecture Overview

DynaDocs is a .NET 10 CLI that authors and validates durable project knowledge, compiles native agent
methods, and enforces universal guard rules. Linear is outside the runtime boundary and remains the sole
owner of live project-management state.

## Main flows

1. `dydo init` scaffolds the documentation tree, source templates, runtime entry files, and guard hooks.
2. The host runtime sends matched tool calls to `dydo guard`.
3. The guard evaluates off-limits paths, dangerous commands, and configured nudges.
4. `dydo sync` compiles role, resource, and workflow templates into native Claude Code and Codex artifacts.
5. `dydo check`, `dydo fix`, `dydo index`, and `dydo graph` maintain the durable documentation graph.

No step provisions, polls, caches, or mirrors Linear. Agents use Linear's official MCP, UI, API, and
integrations outside dydo.

## Component layout

```text
Commands/        System.CommandLine factories and handlers
Services/        Documentation, configuration, template, and guard behavior
Models/          Configuration and parsing data types
Rules/           Documentation validation rules
Templates/       Embedded framework, role, resource, and workflow sources
DynaDocs.Tests/  Unit, integration, E2E, and coverage gates
npm/             Native-binary npm wrapper
```

Services are instantiated directly; interfaces provide test seams without a dependency-injection
container. JSON serialization is source-generated for Native AOT compatibility.

## Knowledge and work boundary

Linear owns Initiatives, Projects, Issues, optional Milestones and Cycles, live status, priority,
assignment, dependencies, updates, and review state.

Git/dydo owns architecture, Decisions, reviewed Project plans, guides, audits, inquisitions,
assimilation briefs, changelog, release tags, pitfalls, and repo-native FutureFeatures. A FutureFeature
is non-actionable until a human promotes it to exactly one Linear target; later delivery state remains
only in Linear.

Branches, worktrees, sessions, native subagents, commits, pull requests, and review attempts are
execution evidence linked to a Linear Issue, not work-record types.

## Roles, skills, and generated artifacts

The `mode-<name>.template.md` source defines a role's methodology and emission metadata. `dydo sync`
compiles skills for both hosts and agent definitions only for spawnable worker roles. Resource templates
compile beside their skill. Workflow templates compile to the host's native workflow surface.

Project overrides live under `dydo/_system/templates/`. Compiled `.claude/`, `.codex/`, and
`.agents/skills/` files are products of `dydo sync` and are never hand-edited.

## Guard system

The guard has three universal layers:

1. off-limits path patterns for secrets and protected system state;
2. dangerous-command detection for destructive shell patterns;
3. configurable nudges that notice, warn, or block.

The host platform owns identity and permissions. dydo does not maintain an agent roster, claim
ceremony, scheduler, queue, or worktree manager.

See [Guard System](./guard-system.md) for the wire contract.

## Documentation graph

Markdown files carry frontmatter and relative links. The scanner builds the document set; validation
rules check summaries, links, folder metadata, hubs, filenames, and project-specific invariants.
`dydo fix` applies supported repairs and `dydo graph` exposes navigation relationships.

The dydo 2.x work corpus was migrated and retired as part of the 3.0 transition. Historical evidence is
available through frozen Git commit permalinks, not as a live repository work model.

## Key design choices

- **Dedicated live-work owner** — Linear manages volatile project state; dydo does not duplicate it.
- **Git-native durable knowledge** — decisions and proof remain reviewable at exact commits.
- **Host-native execution** — Claude Code and Codex own delegation, isolation, and lifecycle.
- **Generated native methods** — one authored role source compiles to both supported runtimes.
- **Universal guard rules** — enforcement is independent of a dydo-managed identity.
- **No DI framework** — direct construction keeps the Native AOT CLI small.

## Related

- [Work Model](./work-model.md) — Linear/Git operating contract
- [Templates and Customization](./templates-and-customization.md) — Authoring and compilation
- [Configuration](../reference/configuration.md) — Runtime configuration
- [Coding Standards](../guides/coding-standards.md) — Repository conventions
