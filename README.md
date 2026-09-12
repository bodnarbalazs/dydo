# DynaDocs (dydo)

Own your project's durable knowledge, use Linear for live work, and let native coding agents execute.

DynaDocs is a documentation, skill-authoring, and guardrail framework for AI coding systems. It keeps
project knowledge explicit and versioned in Git, authors shared role methods as native skills for
Claude Code and Codex, and enforces project rules through hooks. Linear owns the live
Initiative/Project/Issue graph; the coding platform owns sessions, worktrees, delegation, and scheduling.

This project is an opinionated personal harness, not a compatibility-first product. It evolves with the
projects using it and deliberately removes machinery that native runtimes or dedicated work-management
tools now do better.

## The Project That Remembers

Decisions, architecture, guides, reviewed Project plans, audits, assimilation briefs, and changelog live
as Markdown in Git. They are human-readable, reviewable, linkable at an exact commit, and written for AI
consumption as much as for people.

Linear holds volatile work state: Initiatives, Projects, Issues, optional Milestones and Cycles, status,
priority, assignment, dependencies, current updates, and review state. dydo does not copy that graph into
Markdown. Linear links to durable repository artifacts; knowledge discovered during execution flows back
into the appropriate Decision, guide, plan, audit, or assimilation brief.

FutureFeatures are the deliberate exception. An unscheduled idea remains repo-native until the human
promotes it to exactly one Linear Initiative, Project, or Issue. The idea records the stable Linear URL
once and never mirrors subsequent delivery state.

## What dydo Provides

### Durable, AI-friendly knowledge

A structured tree (`understand/`, `guides/`, `reference/`, and durable `project/` knowledge) with
validation, auto-fixing, indexes, and graph tooling. Agents onboard through progressive disclosure,
reading only the durable context relevant to the current Issue.

### One role, native on each host

A role is a plain `SKILL.md` folder committed to each host's discovery path
(`.claude/skills/<role>/` and `.agents/skills/<role>/`). There is no compile step; edit both copies
directly. The host runtime owns agent identity and orchestration.

### Enforced project rules

`dydo guard` checks every tool call, including subagents and workflows. Off-limits paths and dangerous
commands hard-block; project nudges add configurable notices, warnings, and blocks.

### An opinionated scaffold

Every `dydo init` mode creates the knowledge tree, and `CLAUDE.md`. The `claude`,
`codex`, and `all` modes wire guard hooks only for the selected runtimes; Codex selections also add
`AGENTS.md`. The `none` mode installs no guard hooks and no `AGENTS.md`. A new project contains durable
Decisions, changelog, pitfalls, and FutureFeature idea documentation. It creates no repository-backed
live-work hierarchy: use Linear for work management.

## How Work Runs

1. **Shape intent** — record durable decisions and create the appropriately sized Linear Issue or Project.
2. **Review the contract** — an atomic Issue may be its own contract; coordinated, cross-cutting, or
   architecture-sensitive work links to one reviewed repository Project plan.
3. **Execute Issues** — native agents work in isolated branches or worktrees and attach governing commits,
   tests, reviews, and delivery evidence to the Issue.
4. **Audit Projects** — verify the combined result against the linked plan, then publish durable audit and
   assimilation evidence.

Each implementation Issue receives a fresh independent review before completion. Branches, worktrees,
sessions, subagents, commits, pull requests, and reviewer attempts are evidence linked to an Issue; they
are not additional work types.

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

Run from the project root:

```bash
dydo init codex       # or: dydo init claude / dydo init all / dydo init none
dydo check            # validate the documentation tree
dydo fix              # repair supported documentation issues
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json` to the project. Use `--join` when wiring another
runtime or machine into an existing project. The full checklist, Linear workspace and host
configuration included, is [Getting Started](dydo/guides/getting-started.md); point an agent at it
to set dydo up in a project.

Keep current work in Linear. Put information in Git only when it should remain useful and reviewable
after current workflow state changes.

## Customize

- **Nudges** — project regex rules and messages in `dydo.json`.
- **Roles** — plain `SKILL.md` folders under `.claude/skills/` and `.agents/skills/`, edited directly.

A role is its own source; there is no compile step and no automatic reconciliation.

## Folder Structure

```text
project/
|-- dydo.json                    # Integrations, scan exclusions, nudges
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
    |   |-- decisions/           # Accepted choices
    |   |-- plans/               # Reviewed coordinated-work contracts
    |   |-- future-features/     # Unscheduled repo-native ideas
    |   |-- changelog/           # Completed change and release history
    |   `-- pitfalls/            # Recurring gotchas and constraints
    |-- _system/                 # types.json and local runtime state
    `-- _assets/
```

## For Teams

Share the repository and the Linear workspace. Each member wires up their machine's local integration
for the already-initialized project:

```bash
dydo init codex --join
# or
dydo init claude --join
```

Git carries durable knowledge; Linear carries current work and attention state. Do not create a second
work graph in repository files.

## Command Reference

### Setup

| Command | Description |
|---|---|
| `dydo init <integration>` | Initialize for `claude`, `codex`, `all`, or `none` |
| `dydo init <integration> --join` | Wire another runtime or machine into an existing project |

### Documentation and validation

| Command | Description |
|---|---|
| `dydo check [path]` | Validate documentation |
| `dydo fix [path]` | Apply supported documentation repairs |
| `dydo index [path]` | Regenerate documentation indexes |
| `dydo graph <file>` | Show document graph connections |
| `dydo graph stats [--top N]` | Summarize graph connectivity |
| `dydo validate` | Validate local configuration and nudges |

### Guard

| Command | Description |
|---|---|
| `dydo guard` | Evaluate universal hook rules |

See the [complete CLI reference](dydo/reference/dydo-commands.md) for options, examples, transition-only
commands, and exit codes.

## Self-Documentation

dydo documents itself using its own system. Browse the `dydo/` tree in this repository to see durable
knowledge, reviewed plans, migration evidence, and product guidance in practice.

## License

MIT — see LICENSE.
