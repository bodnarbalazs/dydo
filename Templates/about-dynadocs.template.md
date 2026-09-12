---
area: reference
type: reference
---

# DynaDocs (dydo)

Own your project's knowledge, use Linear for live work, and let native coding agents execute.

DynaDocs is a documentation, skill-authoring, and guardrail framework for AI coding systems. It keeps
durable project knowledge explicit and versioned, compiles shared role methods for Claude Code and
Codex, and enforces project rules through hooks. Linear owns the live Initiative/Project/Issue graph;
the coding platform owns sessions, worktrees, delegation, and scheduling.

This project is an opinionated personal harness, not a compatibility-first product. It evolves with the
needs of the projects using it and deliberately removes machinery that native runtimes or dedicated
work-management tools now do better.

## The Project That Remembers

Decisions, architecture, guides, reviewed Project plans, audits, assimilation briefs, and changelog live
as Markdown in Git. They are human-readable, reviewable, linkable at an exact commit, and written for AI
consumption as much as for people.

Linear holds volatile work state: Initiatives, Projects, Issues, optional Milestones and Cycles, status,
priority, assignee, dependencies, current updates, and review state. dydo does not copy that graph into
Markdown. Linear links to durable repository artifacts; knowledge discovered during execution flows
back into the appropriate Decision, guide, plan, audit, or assimilation brief.

FutureFeatures are distinct unscheduled strategic possibilities in Linear. They stay in `FutureFeature`
until the human promotes or cancels them; durable knowledge they uncover flows into dydo. The
[Linear Workspace Standard](./linear-workspace-standard.md) defines the promotion paths.

## Stop Doing Agent Work Yourself

Humans should spend their attention on intent and value choices. Agents implement, test, document,
review, coordinate, and audit from independently reviewable contracts.

- Thinking and coordination roles help shape intent, publish a reviewed Project plan when needed, and
  keep Linear current.
- Execution roles implement one Linear Issue, prove its gates, and return commit and test evidence.
- A fresh agent independently reviews each implementation Issue before human harmonization.
- The [Working-Tree Contract](../guides/working-tree-contract.md) governs Project integration,
  optional Inquisition record delivery, and landing.

Branches, worktrees, sessions, subagents, commits, PRs, and reviewer attempts are execution evidence
linked to an Issue. They are not additional work types.

## What dydo Provides

### 1. Durable, AI-friendly knowledge

A structured tree (`understand/`, `guides/`, `reference/`, and durable `project/` knowledge) with
validation, auto-fixing, indexes, and graph tooling. This is the context that compounds across sessions.

### 2. One role, native on each host

A role is a plain `SKILL.md` folder in the cross-vendor format, committed to each host's discovery
path: `.claude/skills/<role>/` and `.agents/skills/<role>/`. There is no compile step and no
generated agent definition. The host runtime owns agent identity and orchestration.

### 3. Enforced project rules

`dydo guard` checks every tool call, including native subagents. Off-limits paths and dangerous
commands hard-block; project nudges add configurable notices, warnings, and blocks.

### 4. An opinionated scaffold

`dydo init claude`, `dydo init codex`, or `dydo init all` creates the knowledge tree, guard wiring,
and runtime entry files. It does not create a second live work graph; use Linear for work
management, including FutureFeatures.

## How Work Runs

1. **Shape intent** — record durable decisions and create the appropriately sized Linear Issue or Project.
2. **Review the contract** — an atomic Issue may be its own contract; coordinated or architecture-sensitive
   work links to one reviewed repository Project plan.
3. **Execute Issues** — native agents work in isolated branches/worktrees and attach governing commits,
   tests, reviews, and delivery evidence to the Issue.
4. **Inquisition, when confirmed** — this optional, human-confirmed audit files Bugs and delivers its
   record to the feature before landing, following the Working-Tree Contract above.
5. **Land and inspect** — the landing Merge obtains acceptance review, the human lands the feature,
   then a Walkthrough inspects it. An empty Walkthrough closes the Project; findings reopen the lap
   in the same Project.

No dydo command reads, writes, caches, polls, provisions, or mirrors Linear. Agents use Linear's official
MCP, UI, API, and integrations outside the dydo runtime.

## Installation

```bash
# npm (recommended)
npm install -g dydo

# .NET global tool
dotnet tool install -g dydo
```

## Quick Start

```bash
dydo init codex       # or: dydo init claude / dydo init all
dydo check            # validate the documentation tree
dydo fix              # repair supported documentation issues
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json` to the project. Use `--join` when wiring another
runtime or machine into an existing project.

## Customize

- **Nudges** — project regex rules and messages in `dydo.json`
- **Roles** — plain `SKILL.md` folders under `.claude/skills/` and `.agents/skills/`, edited directly

Edit the skill folder in place. A project's copy is its own; there is no automatic reconciliation.

## Folder Structure

```
project/
|-- dydo.json                    # Integrations, scan exclusions, nudges, testing
|-- CLAUDE.md                    # Claude Code entry point
|-- AGENTS.md                    # Codex entry point
|-- .claude/skills/              # Claude skill folders (SKILL.md + resources)
|-- .agents/skills/              # Codex skill folders (SKILL.md + resources + openai.yaml)
`-- dydo/
    |-- index.md                 # Knowledge map
    |-- understand/              # Domain concepts and architecture
    |-- guides/                  # How-to guidance
    |-- reference/               # Exact commands and specifications
    |-- project/                 # Durable knowledge and delivery proof
    |-- _system/                 # types.json and local runtime state
    `-- _assets/
```

## Command Reference

See [dydo Commands Reference](./dydo-commands.md) for the surviving documentation, guard,
validation, testing, and utility commands.

## License

MIT — see LICENSE.

## Related

- [dydo Glossary](./dydo-glossary.md) — Locked work and knowledge vocabulary
- [dydo Commands Reference](./dydo-commands.md) — Local CLI surface
