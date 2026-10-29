---
area: reference
type: reference
---

# DynaDocs (dydo)

Own your project's knowledge, use Linear for live work, and let native coding agents execute.

DynaDocs is a documentation, skill-authoring, and guardrail framework for AI coding systems. It keeps
durable project knowledge explicit and versioned, authors shared methods as native skills for
Claude Code and Codex, and enforces project rules through hooks.
Linear owns the live Initiative/Project/ticket graph; the coding platform owns sessions, worktrees,
delegation, and scheduling.

This project is an opinionated personal harness, not a compatibility-first product. It evolves with the
needs of the projects using it and deliberately removes machinery that native runtimes or dedicated
work-management tools now do better.

## The Project That Remembers

Decisions, architecture, guides, reviewed Project plans, audits, assimilation briefs, and changelog live
as Markdown in Git. They are human-readable, reviewable, linkable at an exact commit, and written for AI
consumption as much as for people.

Linear holds volatile work state: Initiatives, Projects, tickets, optional Milestones and Cycles, status,
priority, assignee, dependencies, current updates, and review state. dydo does not copy that graph into
Markdown. Linear links to durable repository artifacts; knowledge discovered during execution flows
back into the appropriate Decision, guide, plan, audit, or assimilation brief.

FutureFeatures are distinct unscheduled strategic possibilities in Linear. They stay in `FutureFeature`
until the user promotes or cancels them; durable knowledge they uncover flows into dydo. The
[Linear Workspace Standard](./linear-workspace-standard.md) defines the promotion paths.

## Stop Doing Agent Work Yourself

The user should spend attention on intent and value choices. Agents implement, test, document,
review, coordinate, and audit from independently reviewable contracts.

- Officers hold one thing and never switch: the ticket-captain, the default officer, holds a ticket,
  and the chief-of-staff the board; an admiral holds a Project only when the user invokes one. They
  keep Linear current.
- A Project's map holder, the user or an admiral the user invoked, charts it with `wayfinder`.
- Crew are spawned for one bounded job, hold nothing, and return their result: code-writer,
  docs-writer, reviewer, inquisitor, research, scout.
- Every other skill, `co-thinker` among them, is a plain skill any session loads when the work needs
  it.
- A fresh agent independently reviews each implementation ticket before the user harmonizes it.
- The [Working-Tree Contract](../guides/working-tree-contract.md) governs Project integration,
  an Inquisition's audit, proof and record branches, and landing.

Branches, worktrees, sessions, subagents, commits, PRs, and reviewer attempts are execution evidence
linked to a ticket. They are not additional work types.

## What dydo Provides

### 1. Durable, AI-friendly knowledge

A structured tree (`understand/`, `guides/`, `reference/`, and durable `project/` knowledge) with
validation, auto-fixing, indexes, and graph tooling. This is the context that compounds across sessions.

### 2. One skill, native on each host

A skill is one plain `skills/<category>/<name>/` folder in the cross-vendor format, sorted by kind:
the roles under `roles/officers/` and `roles/crew/`, every other skill under `engineering/` or
`productivity/`. `node setup-skills.mjs` walks the tree by rule (a folder holding `SKILL.md` is a
skill, any other folder a category) and
exposes it flat (no category level) through Claude Code and Codex discovery roots. OpenCode may read
those roots too; that is untested, and dydo has no OpenCode init mode. There is no compile step or
generated agent definition. The host runtime owns agent identity and orchestration.

Skills do not ship with the package or `dydo init`. Copy `skills/`, `setup-skills.mjs` and
`THIRD-PARTY-NOTICES.md` (the MIT notices of the adapted skills travel with them) from the dydo
repository into the project root, commit them, and run `node setup-skills.mjs`. The script always
creates both `.claude/skills/` and `.agents/skills/`, and `dydo init` and every
`dydo init <integration> --join` add both to `.gitignore` in every mode.

### 3. Enforced project rules

`dydo guard` checks every tool call its hook matcher names, native subagents' calls included.
Off-limits paths and dangerous commands hard-block; project nudges add configurable notices,
warnings, and blocks.

### 4. An opinionated scaffold

`dydo init claude`, `dydo init codex`, or `dydo init all` creates the knowledge tree, guard wiring,
and runtime entry files; `dydo init none` creates the tree and `AGENTS.md` without guard wiring. It
does not create a second live work graph; use Linear for work
management, including FutureFeatures.

## How Work Runs

1. **Shape intent** - record durable decisions and create the appropriately sized Linear ticket or Project.
2. **Review the contract** - an atomic ticket may be its own contract; coordinated or architecture-sensitive
   work links to one reviewed repository Project plan.
3. **Execute tickets** - native agents work in isolated branches/worktrees and attach governing commits,
   tests, reviews, and delivery evidence to the ticket.
4. **Inquisition, when confirmed** - this optional, user-confirmed audit maps the landed work into
   small areas, reads each, breaks and drives each where its behaviour changed, files Bugs and
   delivers its own record to the feature. Rounds repeat from zero until one finds no blocker and no
   should, then the Project lands.
5. **Land and inspect** - the landing Merge obtains acceptance review, the user lands the feature,
   then its last Milestone's Walkthrough inspects it; each earlier Milestone had its own. An empty
   landing Walkthrough closes the Project; findings reopen the lap in the same Project.

No dydo command writes, polls, provisions, or mirrors Linear. The one command that reads it is
`dydo map`, an on-demand view of one Project that uses the user's `LINEAR_API_KEY` and keeps one
disposable saved map per opened Project for comparison on revisit. Agents use
Linear's official MCP, UI, API, and integrations for live work.

## Installation

```bash
# npm (recommended)
npm install -g dydo

# .NET global tool
dotnet tool install -g dydo
```

## Quick Start

```bash
dydo init codex       # or: dydo init claude / dydo init all / dydo init none
dydo check            # validate the documentation tree
dydo fix              # repair supported documentation issues
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json` to the project. Use `--join` when wiring another
runtime or machine into an existing project. Install the skills as described above before the first
agent session: the `AGENTS.md` entry point sends a thinking session to `co-thinker`.

## Customize

- **Nudges** - project regex rules and messages in `dydo.json`, matched against shell command
  lines or, with `"match": "new-file"`, against the paths of files a write tool is about to create
- **Skills** - plain `skills/<category>/<name>/` folders, edited directly and exposed flat by `setup-skills.mjs`

Edit the skill folder in place. A project's copy is its own; there is no automatic reconciliation.

## Folder Structure

```
project/
|-- dydo.json                    # Integrations, scan exclusions, nudges, testing
|-- AGENTS.md                    # Entry point for Claude Code and Codex
|-- setup-skills.mjs             # Copied from the dydo repository; creates the host projections
|-- skills/                      # Copied from the dydo repository; roles/officers/, roles/crew/, engineering/, productivity/
|-- THIRD-PARTY-NOTICES.md       # Copied from the dydo repository; MIT notices of the adapted skills
|-- .claude/skills/              # Per-skill Claude projections; ignored by init in every mode
|-- .agents/skills/              # Per-skill Codex projections; ignored by init in every mode
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

MIT - see LICENSE.

## Related

- [dydo Glossary](./dydo-glossary.md) - Locked work and knowledge vocabulary
- [dydo Commands Reference](./dydo-commands.md) - Local CLI surface
