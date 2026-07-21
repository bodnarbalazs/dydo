---
title: Notion FutureFeature board: page titles render as 'New page' (record title not written) + 'idea' status option colored red (semantically wrong)
id: 278
area: backend
type: issue
severity: medium
status: resolved
resolved-date: 2026-07-21
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# Notion FutureFeature board: page titles render as 'New page' (record title not written) + 'idea' status option colored red (semantically wrong)

Live Notion board 2026-07-11 (Future Features board): (1) TITLES - every FutureFeature card shows 'New page' instead of the record's title, i.e. the sync is not writing the record title into the Notion page title property for FutureFeature. May be FutureFeature-specific (new type's title mapping missing) or broader (verify Issue/Task/Decision boards render titles correctly - the notion-sync-live-api-constraints note flagged 'issue titles from H1' as a prior title-source quirk). If records genuinely lack a title field post-migration, the fix is upstream in the FutureFeature record shape / migration. (2) STATUS COLORS - the FutureFeature status options (raw/idea/promoted/dropped) are mis-colored: 'idea' renders RED (reads as bad/blocked), 'promoted' green, 'dropped' brown, 'raw' grey. idea should be neutral/blue. The status-option color mapping in the sync-model for FutureFeature is miscalibrated - pick semantically sane colors (raw=grey, idea=blue, promoted=green, dropped=red/brown). Both are FutureFeature-type finalization = fold into sprint M0 (spine object-type completion, which owns the FutureFeature type + colors + title mapping). Found by balazs.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

**Titles:** fixed by the same `EnsureTitle` fallback as 0290 — FutureFeature rows now render real titles. LIVE-VERIFIED 2026-07-21 (ns-10, Opus 4.8 continuation): `NotionLiveFutureFeatureTests` passes (FutureFeature provisions with its status options and a created row's title reads back non-empty); the 3 FutureFeature records synced with real titles on the real board.

**Status options:** the model's canonical set is `raw`/`shaping`/`promoted`/`dropped` (the issue text's `idea` was a stale label, reconciled to the model).

**Color half:** WONTFIX per the sprint's locked "option colors are Notion-owned" decision — sync manages option *names* only and never touches colors (drift ignores color); a human recolors once in Notion, and a fresh mint takes the model's create-payload colors. Closed.