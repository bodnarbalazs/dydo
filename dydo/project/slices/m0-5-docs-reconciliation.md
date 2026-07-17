---
title: m0-5 Docs Reconciliation
blocked-by: m0-1-template-model-completion, m0-2-decision-title-backfill
due:
needs-human: false
priority: Normal
sprint: m0-spine-types-completion
status: ready
work-type: docs
area: project
type: context
---

# m0-5 Docs Reconciliation

Bring the prose in line with the completed model. Small, surgical; runs after m0-1 (prose
describes the final model) and after m0-2 (both touch DRs 033/034 — frontmatter insert lands
first, declared in the sprint record).

**CROSS-SPRINT OVERLAP (declared):** the `understand/work-model.md` + `understand/architecture.md`
edits collide with Olivia's M1-S5 (her plan lines 152/158). NOT parallel-safe with M1-S5 — the
landing order between the two is Adele's call; land serially.

## Work

- **DR-033 §5** (`decisions/033-docs-notion-nested-page-mirror.md`): the model-declared DB set it
  cites now includes `decisions/`, `changelog/`, `pitfalls/` (and `tasks/` once M1 moves land) —
  update the enumeration prose; the mechanism ("derived from sync-model, never hardcoded") is
  unchanged and stays.
- **DR-034**: one-line annotations — §4 amended by [DR 040](../decisions/040-spine-completion-shapes.md)
  §2 (changelog is its own type; archive-relocation question dissolved); §5 confirmed by DR 040 §1.
  Follow the precedent of the planned §8 annotation (M1-S5) — annotate, don't rewrite history.
- **Folder metas**: `project/changelog/` meta (if present) and `decisions/_index.md` /
  `_decisions.md` format sections gain the `title:` frontmatter requirement; `_pitfalls.md` notes
  pitfalls are a spine record type now (file format section already fits).
- **`understand/work-model.md` / `understand/architecture.md`**: grep for prose claiming
  decisions/changelog/pitfalls are mirrored docs or that changelog rows are Task-done rows; fix
  only genuine contradictions of DR 040 — do not restyle.
- Command-reference docs for `model-update` are m0-4's own surface — NOT this slice (no shared
  files between m0-4 and m0-5).

## Gates (exact commands)

- `dydo check` → error count not above the 33-error baseline (issue 0249); zero broken-link or
  relative-link findings on any file this slice touched.

## Success criteria

No doc contradicts DR 040 / the 10-type model; annotations in place; zero overlap with m0-4's
file set.
