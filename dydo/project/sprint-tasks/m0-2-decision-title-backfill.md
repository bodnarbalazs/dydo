---
title: m0-2 Decision Title Backfill
blocked-by:
due:
needs-human: false
priority: Normal
sprint: m0-spine-types-completion
status: ready
work-type: chore
area: project
type: context
---

# m0-2 Decision Title Backfill

Give every decision record a `title:` frontmatter key so the Decision DB's Notion titles are not
blank (live constraint #4 in reference/notion-sync.md — the exact trap that hit issues, fixed the
same way: backfill from each H1).

## Work

- For **every non-`_` file in `dydo/project/decisions/` that does not already carry `title:`**
  (42 of the 43 at plan time — DR-040 already has one; the script MUST skip files that have the
  key, never double-insert): insert `title: <full H1 text>` as the first frontmatter key. Keep
  the `NNN — ` number prefix in the title (it sorts and identifies).
- Escape/quote YAML as needed (H1s contain `—`, `:`, quotes — a colon inside the value requires
  quoting; script it, don't hand-edit 42 files).
- Do not touch `_index.md` / `_decisions.md`.
- Do not change any other frontmatter key, the H1, or body content.
- **Ordering:** this slice lands BEFORE m0-5 (both touch DRs 033/034; declared in the sprint
  record's dependency order).

## Gates (exact commands)

- `dydo check` — decisions must contribute zero NEW errors (tree baseline is 33, issue 0249).
- Stem-uniqueness (flat dir — trivially green, run it anyway):
  `find dydo/project/decisions -name '*.md' ! -name '_*' | sed 's|.*/||' | sort | uniq -d`
  → must print nothing.
- Idempotence proof: `grep -L '^title:' dydo/project/decisions/[0-9]*.md` → must print nothing
  after the run (the numeric glob excludes the `_index.md`/`_decisions.md` meta files, which
  never carry `title:` and are out of scope); re-running the script must produce zero diff.
- Spot-check 3 files by eye: YAML parses, title matches H1.

## Success criteria

Every non-`_` decision record carries exactly one `title:` matching its H1; no other diff lines;
check baseline not worsened.
