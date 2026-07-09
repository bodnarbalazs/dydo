---
title: m0-3 Changelog Conformance (Stems + Titles)
blocked-by:
due:
needs-human: false
priority: High
sprint: m0-spine-types-completion
status: ready
work-type: chore
area: project
type: context
---

# m0-3 Changelog Conformance (Stems + Titles)

Make the 670-record `project/changelog/` tree poolable as the `Changelog` type: the spine loader
keys rows by filename stem and crashes on duplicates — the tree has 13 duplicated stems today —
and no record carries `title:` frontmatter. Scripted, mechanical; DR 040 §2 sanctions renaming
history files (git history is the archive).

## Work

**1. Stem-collision renames (~16 files).** The 13 colliding stems (verified 2026-07-09):
`auto-close-fix`, `auto-close-test`, `dispatch-commit-gap-fix`, `firefighting-sitrep-triage` (×4),
`fix-ide-analyzer-errors`, `fix-inquisition-state-isolation`, `guard-lift-command`,
`help-meta-audit`, `investigate-autoclose-escape-bug`, `orchestrator-handoff`,
`reviewer-verdict-routing`, `template-update-system`, `worktree-reliability` (others ×2).
Rule: the NEWEST occurrence keeps the clean stem; every older occurrence gets its day-folder date
suffixed: `changelog/2026/2026-03-13/auto-close-test.md` → `auto-close-test-2026-03-13.md`.
Use `git mv`. Re-derive the collision list in the script rather than trusting this snapshot
(M1 dispositions may add archives between plan and execution).

**2. Title backfill (all 670).** Insert `title:` from each H1, stripping the `Task: ` prefix
(H1 convention is `# Task: <name>`; if an H1 has no prefix, use it verbatim). Duplicate titles
across rows are fine in Notion — only stems must be unique. Skip `_`-prefixed files (day hubs) —
the loader skips them; they are not rows.

**3. Inbound-link re-check.** Planning survey found references to the colliding stems only in
gitignored agent workspaces (`dydo/agents/**`) — re-grep `dydo/project/**` + `understand/ guides/
reference/` for links to each renamed path as the slice gate; fix any hit (none expected).

## Gates

- Stem-collision script clean over `changelog/**` (and `tasks/**` unchanged).
- `dydo check` baseline (33) not worsened.
- `git log --follow` sanity on 2 renamed files (history intact).
- Spot-check 3 files: YAML parses, title correct.

## Success criteria

Zero duplicate stems under `changelog/**`; all 670 non-`_` records carry `title:`; no orphaned
inbound links; renames done via `git mv`.
