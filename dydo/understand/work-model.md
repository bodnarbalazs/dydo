---
area: understand
type: concept
---

# Work Model

dydo tracks work at four nested levels — **Task → Sprint → Campaign → Release**. The levels are defined by their **exit gate**, not their size: what has to be true for the work to be *done and sound* is the invariant; span (minutes to weeks) merely follows. This is the ontology behind the canonical PM files under `dydo/project/` and the shape `dydo sync` projects into external views like Notion.

Two concepts are kept strictly orthogonal:

- **Container** — where a unit of work sits in the hierarchy (which sprint, which campaign).
- **Status** — where it is in its lifecycle (`backlog`, `active`, `done`…).

Confusing the two is the classic PM tangle. "Backlog" is a **status**, not a container.

---

## The Four Levels

Each level is a gate. Work crosses it exactly when the gate's condition is met — nothing about elapsed time or line count.

| Level | One-liner | Exit gate | Typical span |
|---|---|---|---|
| **Task** | One agent-loop's worth of work; specific and verifiable | reviewer **PASS** | minutes–hours |
| **Sprint** | One `run-sprint` invocation: a few disjoint tasks/slices, merged | **human re-engages** to refine the next sprint | hours–a day |
| **Campaign** | One goal, many sprints; the unit of "actually done and sound" | **mandatory inquisition QA gate** | days |
| **Release** | One ship vehicle; a spec-driven set of campaigns | **ship checklist** (regression / beta) | weeks+ |

### Task

A task is the smallest scheduled unit: one code → review → loop → pass cycle inside a workflow. Its gate is a reviewer verdict. Tasks are the leaves the `run-sprint` workflow drives (see [Delegation](../guides/coding-standards.md); Decision 026).

### Sprint

A sprint is exactly what one `run-sprint` workflow invocation covers — a handful of disjoint slices run in parallel, then merged sequentially. Its gate is *social*, not automated: the human re-engages to inspect the merged result and shape the next sprint. That human touchpoint is the sprint boundary.

### Campaign

A campaign is one goal pursued across many sprints — the unit at which we claim work is genuinely finished and trustworthy. Its gate is the **mandatory inquisition** (no-bugs / coverage / well-tested). Per-sprint inquisitions are overkill; the QA gate lives at campaign end (on-demand for critical work in between). See the [dydo 2.0 campaign roadmap](../project/backlog/dydo-2-campaign-roadmap.md) for a worked example: seven sprints capped by one inquisition.

### Release

A release is different in kind. Campaigns, sprints, and tasks are **work** — they burn down through workflows. A release is a **goal state** driven by a spec document: a title, a spec reference, its set of campaigns, and a status. No workflow machinery *runs* a release; it is modeled lightly and gated by a ship checklist. Release is the fourth spine level above Campaign (Decision 025 session).

---

## Backlog Is a Status, Not a Container

A backlog item is simply a task (or campaign) with `status: backlog` and no sprint attached — a floating unit awaiting scheduling. Floating tasks are explicitly allowed: a backlog task may exist with no sprint container. Nothing needs a dedicated "backlog folder" for this to be true; the status field carries the meaning.

The **idea funnel** rides this: a thought dropped anywhere (a Notion row, an Obsidian file, the `dydo` CLI) lands as a domain-tagged `status: backlog` task file, which a domain orchestrator later pulls from its queue.

---

## Promotion and Demotion Are Cheap

The ontology is fluid; the gates are fixed. A task discovered to be larger than one agent-loop is **promoted** to a sprint or a campaign — a frontmatter edit or a file move, nothing heavier. Work over-scoped can be **demoted** the same way. Because a level *is* its gate, re-leveling a unit just changes which gate it must eventually clear; no work is lost in the move. Treat promotion/demotion as normal and routine, not exceptional.

---

## Issue ≠ Task

An **issue** is an *observed problem* — a bug report, a smell, a gap someone noticed. A **task** is *scheduled work*. They are different objects:

- An issue records that something is wrong; it does not, by itself, schedule a fix.
- A task is a committed unit of work with a gate.
- An issue **spawns** a task when the fix is scheduled.

Keeping them distinct prevents the "every observation becomes an obligation" pile-up and lets triage decide what actually gets scheduled.

---

## Frontmatter Is Canonical, Folders Are Derived

The canonical truth of a work object is its **frontmatter** — `status`, `priority`, `sprint`, `campaign`, `blocked-by`, and so on. Folder placement is **derived presentation**: an ergonomic view (Obsidian-friendly open/closed folders, hub grouping) that dydo regenerates from the frontmatter (the `dydo fix` / hub-regen pattern).

Encoding status in the *path* (e.g. `issues/open/` vs `issues/closed/`) is deliberately avoided: a path encoding fares worse under 3-way merge and Notion sync than a single frontmatter line. Code pools objects from all folders into one list and works from that; folders are for humans, frontmatter is for the machine. Notion mirrors this — one database with a default filter is presentation, not synced structure (Decision 025 §2).

---

## The Gates-Are-Global Lesson

Slicing a sprint into parallel units assumes the slices are **independent**. They are only independent if *nothing they share* can fail for all of them at once.

**Disjoint files do not make disjoint slices.** When repo-wide gates couple all in-tree work — a `dydo check` that validates the whole tree, a test suite that compiles the entire solution, a coverage gate over the full assembly — a slice that trips a shared gate blocks *every* sibling, even ones editing entirely separate files. The seam is the gate, not the file set.

The rule that follows: **sequence tree-shared work.** Parallelize only slices whose gates are genuinely disjoint; when slices share a repo-wide gate, order them so a red gate never strands unrelated work. This is why a docs-only slice (whose gate is `dydo check` plus the doc-consistency tests) is sequenced against source slices that recompile the same tree, rather than run blind against them.

---

## Related

- [Architecture Overview](./architecture.md) — Technical structure of the framework
- [Coding Standards — Delegation](../guides/coding-standards.md) — Who writes code and how work is delegated
- [dydo 2.0 Campaign Roadmap](../project/backlog/dydo-2-campaign-roadmap.md) — A campaign modeled sprint-by-sprint
- [Decision 024](../project/decisions/024-dydo-2-native-pivot.md) — Native pivot: two-tier identity, workflows own orchestration
- [Decision 025](../project/decisions/025-notion-sync-architecture.md) — Canonical files, swappable view adapter (frontmatter-canonical basis)
- [Decision 026](../project/decisions/026-tier1-managers-doctrine.md) — Tier-1 managers; code-writing happens in workflows
- [Decision 028](../project/decisions/028-model-tier-abstraction.md) — Model tiers bound per role/stage by the compiler
