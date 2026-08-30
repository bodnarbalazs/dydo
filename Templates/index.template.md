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

## Skills

One hat at a time: a session wears the hat the work is in now and changes it as the work moves. The
human invokes the human commands, **chief-of-staff** and **manager** by name; a model reaches for
everything else itself. Skills compile from their templates: change the source and run `dydo sync`,
never the compiled copy.

### Hats — what the session is doing now

- **co-thinker** — an idea is unripe: think with the human until it is worth a DR or a Project.
- **planner** — ripe intent needs a route: an atomic Issue, or a Project plan and its Issue map.
- **implementer** — a ticket is picked: own one Issue from branch to merged PR, delegate to workers.
- **manager** — one Project, several Issues in flight, serial merges, a plan that shifts as fog clears.
- **chief-of-staff** — the human's attention needs triage: what waits on him, what has gone stale.

### Workers — spawned for one bounded job

- **code-writer** — production code for one scoped change.
- **test-writer** — tests that pin behaviour at a seam.
- **docs-writer** — one reviewed documentation change.
- **reviewer** — the gate before any merge: judge against one rubric, return the review block.
- **inquisitor** — one lens of an audit, hunting for what got through.
- **research** — facts from primary sources, cited, read-only.

### Methods — reference and procedure used inside other skills

- **grilling** — a claim, plan, or draft must survive questioning before it is trusted.
- **wayfinder** — a Project is foggy: chart what is known, file question Issues for what is not.
- **domain-modeling** — the words are slipping: name the domain and keep the glossary honest.
- **codebase-design** — a change touches module boundaries, seams, or depth.
- **diagnosing-bugs** — a defect needs a tight loop that goes red before any fix.
- **prototype** — a throwaway artifact answers the question more cheaply than argument.
- **writing-for-agents** — writing or editing anything an agent reads: a skill, a pointer, a doc.
- **self-improvement** — a friction keeps recurring: change the system, not just this task.

### Human commands

- **grill-me** — the human asks to be questioned.
- **bro** — that did not land: re-pitch it in plain language.
- **handoff** — pack the session's state for whoever picks it up next.
- **walkthrough** — before the human lands a feature: what changed, where to look, how to try it.
- **teach** — the human wants to learn the thing rather than receive it.
- **improve-codebase-architecture** — hunt architectural improvements and pitch the best one.

### Workflow

- **inquisition** — a rare, human-confirmed audit: inquisitors across lenses, reviewer as judge.

### Rubrics — the reviewer's resources; the invoker names one

- **code** — implementation changes.
- **tests** — test changes.
- **docs** — anything written to be read by an agent or a human.
- **plan** — a Project plan, or an Issue plan before architecture-sensitive code.
- **merge** — after every merge: a mechanical spot check on the integrated state.

### Planner resources — the two planning resolutions

- **project** — low resolution: destination, acceptance, architecture, the Issue map.
- **issue** — high resolution, just in time: files, the pattern to copy, steps, edge cases, gates.

## The Guard

Every tool call passes through `dydo guard`, which enforces universal rules:

- **Off-limits paths** ([files-off-limits.md](files-off-limits.md)) — secrets and system files
- **Protected paths** — this file, `files-off-limits.md`, `dydo.json`: any tool may read, none may write
- **Dangerous commands** — destructive patterns that are always blocked
- **Nudges** — configurable project reminders and blocks from `dydo.json`

A block is guidance. Re-read the relevant documentation; if it still looks wrong, tell the human rather
than working around it.
