---
area: general
type: hub
---

# DynaDocs — Orientation

dydo authors and knows: it keeps this project's durable knowledge and policy in Git, then compiles
shared roles into native skills and agents. Linear owns the live work graph. Claude Code or Codex owns
runtime identity, permissions, isolation, and agent coordination.

---

## The Knowledge Tree

- [understand/](understand/_index.md) — what this project is and how it is built. Start with
  [about](understand/about.md) and [architecture](understand/architecture.md).
- [guides/](guides/_index.md) — how to work here, including
  [coding standards](guides/coding-standards.md).
- [reference/](reference/_index.md) — exact rules and specifications:
  [dydo commands](reference/dydo-commands.md), [writing docs](reference/writing-docs.md), and the
  locked [dydo glossary](reference/dydo-glossary.md).
- [glossary.md](glossary.md) — this project's domain vocabulary.
- [project/](project/_index.md) — durable Decisions, plans, evidence, release history, pitfalls, and
  repo-native FutureFeatures.

Use the locked dydo glossary when work touches plans, roles, skills, reviews, or execution evidence.
Use the project glossary for project-domain terms. `dydo check` validates the tree; `dydo fix` repairs
what it can.

## Work and Knowledge

Use Linear Initiatives, Projects, Issues, optional Milestones, and Cycles for live work. An Issue is the
only actionable tracked work item; Sub-issues are optional when children need independent tracking.
Status, priority, assignment, dependencies, updates, and current review state stay in Linear.

Use Git for durable knowledge and proof: Decisions, reviewed Project plans, guides, audits,
inquisitions, assimilation briefs, changelog, and release tags. Current navigation may use a
branch-following GitHub URL; governing contracts and historical evidence use exact commit permalinks.
Branches, worktrees, sessions, subagents, commits, and reviews are evidence linked to an Issue, not extra
levels in the work graph.

A FutureFeature remains an unscheduled repo-native idea. Only the human may promote it to exactly one
Linear Initiative, Project, or Issue. The stable Linear URL is recorded once; later delivery state stays
only in Linear.

## Skills and Roles

Role methodologies are authored once in dydo and compiled into platform-native skills and agents.
Compiled output is never hand-edited; change the source template and run `dydo sync`.

## The Guard

Every tool call passes through `dydo guard`, which enforces universal rules:

- **Off-limits paths** ([files-off-limits.md](files-off-limits.md)) — secrets and system files
- **Dangerous commands** — destructive patterns that are always blocked
- **Nudges** — configurable project reminders and blocks from `dydo.json`

A block is guidance. Re-read the relevant documentation; if it still looks wrong, tell the human rather
than working around it.
