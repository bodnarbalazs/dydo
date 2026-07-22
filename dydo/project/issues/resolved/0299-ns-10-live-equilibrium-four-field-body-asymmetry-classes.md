---
title: "ns-10 live equilibrium: four field/body asymmetry classes between fake-modeled and live Notion echoes"
id: 299
area: backend
type: issue
severity: medium
status: resolved
resolved-date: 2026-07-22
found-by: manual
found-by-agent: coordinator
found-by-vendor: claude
found-by-model: claude
date: 2026-07-22
---

# ns-10 live equilibrium: four field/body asymmetry classes between fake-modeled and live Notion echoes

The ns-10 equilibrium work (664b0b28) converged the spine's dry-run drift from 397 → 0 by canonicalizing field/body echoes, but four live-discovered asymmetry classes between what the fake modeled and what live Notion actually echoes each needed a distinct fix. This issue is the citation anchor for that equilibrium seam (the code comments reference it).

## The four classes

1. **Schema-default echoes.** Live Notion returns EVERY schema property, so a page echoes `needs-human=false` (checkbox) and empty `select`/`date`/`url`/`relation` for properties a legacy record never carried. `NormalizeFields` canonicalizes ABSENT == EMPTY == DEFAULT (checkbox keeps only `"true"`; empty scalars drop) so those echoes never read as an external edit — no phantom `WriteToRepo`.
2. **Synthesized-title echo.** A record with no `title:` key (only `name:`/localId) is pushed with a prettified board title by `EnsureTitle`; the board echoes it back as a title field the file lacks. `NormalizeFields` mirrors the exact `SynthesizedTitle` on the same predicate `EnsureTitle` uses, so the echo round-trips as a no-op while a genuine human rename still imports.
3. **Field-only body collateral.** A genuine field change fired `WriteToRepo`, which also rewrote the file body into Notion's normalized dialect (blank lines stripped). `WriteToRepoResult` now carries the LOCAL body verbatim when the bodies are equal modulo the block round-trip; the external body is written only when it genuinely differs.
4. **Field order.** The base carries authored frontmatter order while the external echo is `ToFields`' canonical (title-first, then alphabetical) order. `ReconcileEngine.Equal` is now order-insensitive (sorted compare), so a pure reorder never reads as a change.

## Follow-on correctness fixes (this inquisition round)

- Overlay visibility means "schema-mapped" (representable scalar), not "field-normalizer dropped it" — a `needs-human: false` local key no longer clobbers a genuine board check (F1); the overlay no longer plants schema-default echoes into legacy files on a genuine rewrite (F3).
- `EnsureTitle`/`NormalizeFields` mirror predicate aligned so a present-but-empty `title:` converges (F2).
- Genuine local clears of `select`/`number`/`date`/`url` push the explicit Notion clear shape (`{"select": null}`, `{"rich_text": []}`) on an update instead of silently reverting (F5).
- health/attention gate-result matched case-insensitively via `test(prop("gate-result"), "(?i)fail")` (F4).
- Table row-batching implemented (a table > 100 rows appends its overflow rows to the created table id, live-confirmed 2026-07-22) (F19).

## Resolution

Resolved by ns-10-core (664b0b28: the four classes) plus this inquisition round (F1–F5, F19 and the coverage/deadcode/doc fixes). Verified: full suite green, both fixed-point sweeps green, live dry-run zero across all spine records.
