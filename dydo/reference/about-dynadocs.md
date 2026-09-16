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

FutureFeatures are distinct unscheduled strategic possibilities in Linear. They stay in `Backlog`
until the human promotes or cancels them; durable knowledge they uncover flows into dydo.

## Stop Doing Agent Work Yourself

Humans should spend their attention on intent and value choices. Agents implement, test, document,
review, coordinate, and audit from independently reviewable contracts.

- Thinking and coordination roles help shape intent, publish a reviewed Project plan when needed, and
  keep Linear current.
- Execution roles implement one Linear Issue, prove its gates, and return commit and test evidence.
- A fresh agent independently reviews each implementation Issue before human harmonization.
- A coordinated Project closes only after an integrated audit against its linked plan and a durable
  assimilation brief proportionate to the change.

Branches, worktrees, sessions, subagents, commits, PRs, and reviewer attempts are execution evidence
linked to an Issue. They are not additional work types.

## What dydo Provides

### 1. Durable, AI-friendly knowledge

A structured tree (`understand/`, `guides/`, `reference/`, and durable `project/` knowledge) with
validation, auto-fixing, indexes, and graph tooling. This is the context that compounds across sessions.

### 2. One source for native roles and skills

`dydo sync` compiles role templates and resources into Claude Code and Codex artifacts. Edit the source
once; both runtimes receive the same method. The host runtime owns agent identity and orchestration.

### 3. Enforced project rules

`dydo guard` checks every tool call, including subagents and workflows. Off-limits paths and dangerous
commands hard-block; project nudges add configurable notices, warnings, and blocks.

### 4. An opinionated scaffold

`dydo init claude`, `dydo init codex`, or `dydo init all` creates the knowledge tree, role templates,
guard wiring, and runtime entry files. It does not create a second live work graph; use Linear for work
management, including FutureFeatures.

## How Work Runs

1. **Shape intent** — record durable decisions and create the appropriately sized Linear Issue or Project.
2. **Review the contract** — an atomic Issue may be its own contract; coordinated or architecture-sensitive
   work links to one reviewed repository Project plan.
3. **Execute Issues** — native agents work in isolated branches/worktrees and attach governing commits,
   tests, reviews, and delivery evidence to the Issue.
4. **Audit Projects** — verify the combined result against the linked plan, then publish durable audit and
   assimilation evidence.

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
dydo sync             # compile shared roles and skills
dydo check            # validate the documentation tree
dydo fix              # repair supported documentation issues
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json` to the project. Use `--join` when wiring another
runtime or machine into an existing project.

## Customize

- **Nudges** — project regex rules and messages in `dydo.json`
- **Roles** — shipped source templates
- **Template additions** — Markdown in `dydo/_system/template-additions/`, included through durable hooks

Do not hand-edit compiled skills or agents. Change their source templates and run `dydo sync`.

## Folder Structure

```
project/
|-- dydo.json                    # Model tiers, integrations, nudges
|-- CLAUDE.md                    # Claude Code entry point
|-- AGENTS.md                    # Codex entry point
|-- .claude/                     # Compiled Claude agents, skills, and workflows
|-- .codex/agents/               # Compiled Codex agents
|-- .agents/skills/              # Compiled Codex skills
`-- dydo/
    |-- index.md                 # Knowledge map
    |-- understand/              # Domain concepts and architecture
    |-- guides/                  # How-to guidance
    |-- reference/               # Exact commands and specifications
    |-- project/                 # Durable knowledge and delivery proof
    |-- _system/template-additions/
    `-- _assets/
```

## Command Reference

See [dydo Commands Reference](./dydo-commands.md) for the surviving documentation, role-compilation,
guard, validation, template, and utility commands.

## License

MIT — see LICENSE.

## Related

- [dydo Glossary](./dydo-glossary.md) — Locked work and knowledge vocabulary
- [dydo Commands Reference](./dydo-commands.md) — Local CLI surface
