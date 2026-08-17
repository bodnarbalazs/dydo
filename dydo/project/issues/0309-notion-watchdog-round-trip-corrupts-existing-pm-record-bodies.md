---
title: Notion watchdog round-trip corrupts existing PM record bodies
id: 309
area: backend
type: issue
severity: high
status: open
found-by: manual
date: 2026-08-17
---

# Notion watchdog round-trip corrupts existing PM record bodies

An existing Slice record was silently rewritten on a watchdog round-trip with duplicated sections above its H1, stripped formatting, and board-derived frontmatter; restoring the tracked file recovered it, while a Notion-originated CRC-blindness record arrived intact and is the clean create-path control.

## Description

Production report (slice-11 / CRC-blindness, 2026-08-17): no agent or human edit produced the damaged file. The Notion watchdog imported its own lossy page echo into the existing canonical record. Git made recovery possible in this case; the same behavior can silently damage a record that has no intact tracked version.

The asymmetric control is important:

- Notion-originated creation reached the repo intact (`CreateToRepo` is a direct create path).
- An already tracked record entered `ReconcileExisting`, where a perceived external body change can take `WriteToRepoResult` or `MergeBoth` and replace/merge the canonical body.

The code still gives PM-spine bodies to `NotionBlockConverter` in both directions (`NotionSyncAdapter` reads with `FromBlocks`, writes with `ToBlocks`, and uses the same projection as its body normalizer). That converter intentionally handles block structure only: inline markdown is not represented as Notion annotations, and rich-text reads are flattened to plain text. The normalization can suppress known dialect-only churn, but it is not a lossless representation of the body. A shape outside the fixed-point corpus, or a real Notion echo that differs from the in-process projection, can therefore be classified as an external edit. `WriteToRepoResult` then persists the external body; `MergeBoth` runs a raw line merge, which can duplicate or reorder regions. Schema-mapped Notion properties are also eligible to serialize back into frontmatter on that write, explaining the board metadata that appeared with the body damage.

This is production evidence that the resolution of issue 0236 was too narrow. Its fixed-point sweep proved `FromBlocks(ToBlocks(x))` stability for the then-current dydo corpus, and its live gate proved a reset followed by a no-edit dry-run. It did not prove byte-safe round-tripping of a formatting-rich existing record through a watchdog delta tick. DR 035 already records the intended follow-up: move the spine off the custom block converter and onto the native Markdown API with a convergent comparison layer.

Until fixed, the safe operational rule is: keep PM records tracked before allowing watchdog round-trips, and restore unexpected body/frontmatter rewrites from git rather than accepting them as authored changes.

## Reproduction

1. Start from an existing tracked Slice whose body has multiple sections and inline formatting; seed its base snapshot and Notion page.
2. Change the local record and let the watchdog push it, then let a later delta tick read the real Notion echo.
3. Observe that the existing-record path may plan `WriteToRepo`/`Merged` even though nobody edited the page, and that the canonical file can acquire the flattened body, duplicated merge regions, and schema-derived frontmatter.
4. Control: create the same content in Notion with no local/base record and import it; the direct create path does not exercise the existing-record reconciliation asymmetry.

## Required fix and verification

1. Migrate PM-spine body read/write to the native Markdown API, keeping properties on the existing schema path; retire `NotionBlockConverter` as a body transport.
2. Compare against a stable external projection (or store distinct local and external base projections) so a daemon's own write echo cannot become an authored external change. Do not advance the canonical file from an uncertain/non-convergent echo.
3. Fail closed for canonical writes: an unexplained external body rewrite of an existing record goes to the spine conflict shadow with a loud warning, never straight into the canonical file.
4. Add the sanitized slice-11 body as a regression fixture. The live acceptance must be: existing file -> watchdog push -> real Notion read -> watchdog tick plans `None`, with the file byte-identical; then a genuine Notion body edit imports once without collateral formatting or frontmatter changes. Keep the Notion-originated create control green.

## Resolution

(Filled when resolved)
