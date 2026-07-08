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

## Directions to consider

- `--all` should only sweep tasks in `review-pending` (or `human-reviewed`) state; require
  a confirmation listing what it will archive; add `--dry-run`.
- Refuse (or require `--force`) to approve a task currently claimed by a working agent.
- Preserve original frontmatter in the archived copy (e.g. under `original:` keys) so
  restore is lossless.
- Consider separating `approve` (gate pass) from `archive` (board removal) as distinct
  verbs.
