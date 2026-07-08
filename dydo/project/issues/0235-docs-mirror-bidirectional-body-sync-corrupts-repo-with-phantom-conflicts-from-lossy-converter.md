---
title: Docs-mirror bidirectional body sync corrupts the repo with phantom conflict markers (lossy converter round-trip)
area: backend
fix-release: 
needs-human: true
resolution: 
severity: high
status: open
work-type: 
id: 235
type: issue
found-by: sprint-auditor
date: 2026-07-08
---

# Docs-mirror bidirectional body sync corrupts the repo with phantom conflict markers

Found by the sprint-auditor over the 0234 tree, then confirmed: **176 markdown docs** under `dydo/`
were rewritten in the working tree with **nested three-way-merge conflict markers** (the
`repo` / `=======` / `external` labels emitted by `Sync/ThreeWayTextMerge.cs`), produced by the DR 033
docs-mirror **bidirectional body sync** during live `dydo notion sync --docs-only` re-smoke runs. HEAD
is clean (the corruption is entirely uncommitted). Affected: `dydo/guides/*` (incl. the must-read
`coding-standards.md`, +821 lines), `dydo/glossary.md`, `dydo/project/_index.md`, all of
`dydo/project/backlog/*` and `dydo/project/changelog/*`. `reference`/`issues`/`tasks`/`decisions` clean.
`needs-human` because the fix requires a design decision (below).

## Root cause

DR 033 makes each doc's **body bidirectional** (repo ↔ Notion via DR 025's base-snapshot + 3-way text
merge), but `NotionBlockConverter` is **deliberately lossy** (line-oriented; no inline formatting,
nesting, or exact-whitespace fidelity). So the Notion round-trip (`markdown → blocks → markdown`)
**never equals** the repo text. On sync:

- base body starts `""` (recorded at structural create); repo = real markdown; external = the
  round-tripped, formatting-drifted markdown read back from Notion.
- `base→repo` changed **and** `base→external` changed (drift), both from `""` → the 3-way merge treats
  it as a **two-sided conflict** and writes `repo`/`external` conflict markers into **both** the repo
  file and the Notion page.
- Re-running the sync reads the now-conflict-marked repo, syncs it to Notion, reads it back
  (re-drifted), and merges again → **conflict hunks nest and stack** (the +821-line blow-up).

So the lossy converter turns a one-way "push the doc body" into a self-amplifying conflict generator
that corrupts the canonical repo — the exact "delete the adapter and the repo is whole" invariant of
DR 025 §1, violated.

## Two parts

### A. Safety rail (concrete, must-fix before `--docs` is ever enabled)
The sync must **never write conflict markers into a canonical repo file**, and must never push a doc
that contains conflict markers to Notion. Options: refuse to run when unresolved markers are present in
the target tree; treat a would-be body conflict as a non-fatal skip + surfaced warning (leave the repo
untouched) rather than writing markers; and/or gate the mirror behind a clean-tree precondition.

### B. Make the two-way merge drift-insensitive (DECIDED: bidirectional stays — Balazs, 2026-07-08)
Bidirectional doc bodies are **non-negotiable** — the concept is sound; the *implementation's*
conflict detection is naive (it diffs **raw** text, so lossy round-trip drift reads as a real edit,
and the base is never advanced to a normalized form). Do **not** retreat to one-way. The fix is
**proper normalization** — compare/merge in a lossy-stable canonical space:

1. **Idempotent round-trip (prerequisite).** Make `NotionBlockConverter` satisfy
   `roundtrip(roundtrip(x)) == roundtrip(x)` — the conversion stabilizes after one pass. Establish it
   with a property test. Without idempotency the normalized form isn't well-defined.
2. **Normalized-space merge.** Define `N(x) = FromBlocks(ToBlocks(x))`. Do the 3-way diff/merge on
   `N(...)` and **store the base normalized**. Pure round-trip drift then becomes invisible
   (`N(external)` of a just-pushed doc == normalized base → no external change → repo untouched); a
   **genuine** Notion edit survives normalization → registers → merges. When a real merge occurs, map
   the result back to markdown preserving repo formatting in unchanged regions.
3. **Safety rail (part A above)** lands regardless as a backstop: never write conflict markers into a
   canonical file.

Open sub-question raised with Balazs: for markdown the converter can't round-trip stably (tables,
asymmetric nested lists), do we constrain the source or accept a flattened-but-convergent stable form?
Lean: **accept flattened-but-stable** (converge, don't restrict authoring).

## Immediate recovery
Restore the 176 corrupted docs to HEAD (`git checkout HEAD --` on the affected doc paths; HEAD is clean).
The scratch Notion page holds the conflicted bodies — disposable. Coordinated via the chief-of-staff
(shared tree). Do NOT run any `--docs` sync until A lands.

## Reproduction
1. `dydo notion sync --docs` (or `--docs-only`) against a page, twice.
2. Observe the repo doc bodies rewritten with `repo`/`external` conflict markers, nesting further each run.

## Resolution
(Filled when resolved)
