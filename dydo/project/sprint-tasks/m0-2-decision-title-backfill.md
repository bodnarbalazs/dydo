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

- For each of the 42 non-`_` files in `dydo/project/decisions/` (including `resolved`-style
  subfolders if any — there are none today): insert `title: <full H1 text>` as the first
  frontmatter key. Keep the `NNN — ` number prefix in the title (it sorts and identifies).
- Escape/quote YAML as needed (H1s contain `—`, `:`, quotes — a colon inside the value requires
  quoting; script it, don't hand-edit 42 files).
- Do not touch `_index.md` / `_decisions.md`.
- Do not change any other frontmatter key, the H1, or body content.

## Gates

- `dydo check` — decisions must contribute zero NEW errors (tree baseline is 33, issue 0249).
- Stem-uniqueness check over `decisions/**` (flat dir — trivially green, run it anyway).
- Spot-check 3 files by eye: YAML parses, title matches H1.

## Success criteria

42 files carry `title:` matching their H1; no other diff lines; check baseline not worsened.
