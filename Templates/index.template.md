---
area: general
type: hub
---

# DynaDocs — Orientation

dydo authors and knows: it holds this project's knowledge, policy, and work records, and
compiles its roles into your platform's native skills and agents. Your platform (Claude Code,
Codex) runs and coordinates the work. Behavior rules live in the root entry file
(`CLAUDE.md` / `AGENTS.md`); this page is the map.

---

## The knowledge tree

- [understand/](understand/_index.md) — what this project is and how it's built. Read-first
  material: [about](understand/about.md), [architecture](understand/architecture.md).
- [guides/](guides/_index.md) — how to do things here, including
  [coding-standards](guides/coding-standards.md).
- [reference/](reference/_index.md) — exact rules and specs:
  [dydo commands](reference/dydo-commands.md), [writing docs](reference/writing-docs.md),
  [dydo glossary](reference/dydo-glossary.md) — the system's terms, locked.
- [glossary.md](glossary.md) — the project's terms.

Conventions for writing any of it: [reference/writing-docs.md](reference/writing-docs.md).
`dydo check` validates the tree; `dydo fix` repairs what it can.

## The work records

Everything in flight lives under [project/](project/_index.md) as markdown records:

- **Sprints** (`project/sprints/`) — a plan's root: specification + slice map.
  Statuses: `planning → plan-review → active → audit → done`.
- **Slices** (`project/slices/`) — one file per slice: the implementer's contract.
  `ready → in-progress → done`.
- **Tasks** (`project/tasks/`) — day-to-day tracked work: `backlog → in-progress →
  in-review → done` (`dydo task create/list/done`).
- **Issues** (`project/issues/`) — discovered problems (`dydo issue create`).
- **Decisions** (`project/decisions/`) — why things are the way they are. Read before
  re-deciding something.

## Skills and roles

Role methodologies are authored once in dydo and compiled into your platform's skills and
agents (a skill is a folder: `SKILL.md` plus its `resources/`). Compiled output is never
hand-edited — if a skill needs changing, that's a template change for whoever maintains
dydo here.

## The guard

Every tool call passes through `dydo guard` (a PreToolUse hook), enforcing universal rules:

- **Off-limits paths** ([files-off-limits.md](files-off-limits.md)) — secrets and system
  files, blocked for all agents.
- **Dangerous commands** — destructive patterns, always blocked.
- **Nudges** — configurable regex rules (`dydo.json`) that warn or block with guidance.

A block is guidance. Re-read the relevant doc; if you still believe it's wrong, tell the
human — never work around it.
