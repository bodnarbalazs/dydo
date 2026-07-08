---
area: project
type: backlog
date: 2026-07-08
requested-by: balazs
---

# Rethink the task approve/archive workflow

## Trigger

On 2026-07-08 balazs intended to approve two review-pending tasks and accidentally
approved **all 46 tasks on the board** (bulk approve). Every task — including in-progress,
claimed ones (the active chief-of-staff task, Charlie's active DR 033 campaign task) —
was archived into `changelog/2026/2026-07-08/` as if completed. Chief-of-staff restored
the two live tasks by hand; the rest were stale enough that the wipe was accepted as a
de-facto board reset. balazs: "this workflow should probably be thought through, it was
never perfect."

## Problems observed

1. **Bulk approve is a footgun** — one command can archive the whole board with no
   confirmation and no dry-run. (`--all` exists per the 2026-03-07 `task-approve-all-flag`
   changelog; it predates the multi-agent board.)
2. **Approve conflates two things**: passing a review gate vs archiving/completing.
   Approve happily "completed" tasks that were `pending` / `in-progress` and never
   review-pending at all.
3. **No claimed-task guard**: archiving a task an agent is actively bound to
   (`dydo agent role --task X`) silently breaks the agent's task binding and moves its
   task file out from under it.
4. **Archive rewrites frontmatter destructively**: `name`/`status`/`assigned` are
   stripped and replaced with changelog frontmatter, so an accidental archive loses the
   task's state (restore required git or hand-reconstruction).
5. **Duplicate-stem hazard**: restoring an archived task while its changelog copy exists
   creates a duplicated filename stem across subfolders — which crashes the Notion spine
   sync (DR 034 loader keys rows by stem).

## balazs's deeper reframe (2026-07-08, supersedes the mechanical fixes below as the primary goal)

The human-approve gate itself is the problem, not just its ergonomics. His words: typing each task
name is "super menial", so "99% of cases I ran dydo task approve --all. And that made me think.
It's useless. I'm not reviewing any of them myself. I already rely on it being done. And I review
in a different pace." The task lifecycle stays (decided), but the human-approval step is theater:
automated review gates (run-sprint reviewer + sprint-auditor) are what he actually relies on, and
his real review happens asynchronously at his own pace, decoupled from task completion. The reform
should redesign WHAT (if anything) a human gate certifies, WHEN tasks flip to done, and how
balazs's actual async review pace gets first-class support instead of a fake synchronous gate.
A co-thinker design round was dispatched for this (2026-07-08). Interaction: DR-034 S2a implements
the status vocab (data model) independently; the reform re-wires who/what flips in-review -> done.

## Directions to consider

- `--all` should only sweep tasks in `review-pending` (or `human-reviewed`) state; require
  a confirmation listing what it will archive; add `--dry-run`.
- Refuse (or require `--force`) to approve a task currently claimed by a working agent.
- Preserve original frontmatter in the archived copy (e.g. under `original:` keys) so
  restore is lossless.
- Consider separating `approve` (gate pass) from `archive` (board removal) as distinct
  verbs.
