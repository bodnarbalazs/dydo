---
title: notion-sync.md contradicts the recorded DR-035 live-smoke results - still 'pending smoke', folder-page write shape live Notion rejects 400
id: 260
area: reference
type: issue
severity: medium
status: resolved
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
resolved-date: 2026-07-12
---

# notion-sync.md contradicts the recorded DR-035 live-smoke results - still 'pending smoke', folder-page write shape live Notion rejects 400

The designated API-truth reference still marks the native-markdown endpoints as pending Charlie's live smoke and documents a folder-page replace_content shape without the mandatory child-page-tag re-append; the Live-API Validation Constraints section omits both 2026-07-09 smoke findings (folder-write 400, lossy round-trip).

## Description

The 2026-07-09 live smoke ran and its findings were recorded in DR-035 (9a0e532c: create-with-body, replace_content shape, and child-safety are now "Landed + correct"), but `dydo/reference/notion-sync.md` — the project's designated API-truth reference — was last touched in 8406c6fa, which predates that, and now contradicts the recorded results:

- **Line 14** still says the native-markdown rows are "pending Charlie's live smoke" and create-with-body "has not been confirmed against a live page_id parent"; line 29 repeats "still pending the live smoke". Both confirmed false by DR-035 Status.
- **Line 32** documents `replace_content` with `allow_deleting_content:false` for folder pages WITHOUT the mandatory child-page-tag re-append — a write shape live Notion rejects 400 (DR-035 smoke finding 1: "Include these items in content using <page url> tags, OR set allow_deleting_content: true").
- **The "Live-API Validation Constraints" section (lines 52-61)** — whose stated charter is recording exactly these live-only constraints — lists only the four 2026-07-06 findings and omits both 2026-07-09 findings: (a) the folder-write 400 without tags; (b) the lossy markdown round-trip (H1-as-title drop, escapes, blank-line collapse — the corruption class that hit DR-040).

Anyone coding against this reference rebuilds the 400 and the corruption the smoke already proved. Related code-comment drift (same root, lower severity, report-only): `DocsPageAdapter.cs:170-178` and `DocsTreeSync.cs:66-68/:241-243/:262-263` still claim the child-safe folder-body PATCH works and "self-heals", and `DocsTreeSync.cs:64` / `DocsPageAdapter.cs:157` still call create-with-body "unconfirmed" — see the inquisition report.

Distinct from the known-open DR-035 convergence gap (the pending reappend-tags-on-write work): this is the reference doc contradicting recorded smoke results, not the pending behavior itself.

Found by the v2.0.6 campaign inquisition (doc-drift lens); adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12 (landed 66c1aa1e). dydo/reference/notion-sync.md reconciled to DR-035's recorded 2026-07-09 live-smoke results: native-markdown endpoints/create-with-body/PATCH-shape/child-safety marked landed+correct (was 'pending Charlie's smoke'); folder-page replace_content documented as requiring child-page-tag re-append (else live 400); Live-API Validation Constraints extended with the folder-tag 400 + the lossy native-markdown round-trip (DR-040 corruption class). Codex Frank (Terra, docs-writer), Claude-reviewed, every edit grounded in DR-035.