---
title: Docs mirror creates nested pages but leaves page BODIES empty -- structure syncs, doc content never written (observed on live re-smoke against scratch page, 2.0.4)
id: 225
area: backend
type: issue
severity: high
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
resolved-date: 2026-07-07
---

# Docs mirror creates nested pages but leaves page BODIES empty -- structure syncs, doc content never written (observed on live re-smoke against scratch page, 2.0.4)

The Notion docs mirror created the nested page structure but never wrote page bodies — the tree synced while every page stayed empty (observed on live re-smoke against a scratch page, 2.0.4).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

NOT a content bug -- false alarm (2nd incomplete-run illusion). Root cause: a transient Notion 504 killed the sync mid-phase-2 (bodies are written in phase 2, a single SyncRunner.Run; phase 1 creates pages empty). Reconcile math is correct for a fresh page, and unit tests Run_DocPageBody_MirrorsMarkdown + Run_IndexFile_BecomesFolderPageBody prove real markdown body content lands on a complete Run (same code path). Charlie re-ran to completion. The REAL underlying issue is the mirror's fragility on transient 5xx/429 (no retry -- filed separately by Charlie); that fragility is what caused the incomplete run, not a body-writing defect.