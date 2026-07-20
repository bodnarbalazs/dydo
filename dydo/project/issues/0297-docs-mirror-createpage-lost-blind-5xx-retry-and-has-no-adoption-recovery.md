---
title: Docs mirror CreatePage lost blind 5xx retry and has no adoption recovery
id: 297
area: backend
type: issue
severity: low
status: open
found-by: review
date: 2026-07-20
---

# Docs mirror CreatePage lost blind 5xx retry and has no adoption recovery

An ambiguous create failure in the docs-mirror page adapter can orphan a child page that duplicates on the next tick.

## Description

ns-5 made CreatePage non-idempotent at the client (no blind 5xx/transport-throw retry) and added re-query/adopt recovery only to the spine's NotionSyncAdapter. DocsPageAdapter.cs:150 (and the root/folder create path via DocsTreeSync.cs:243) inherits the non-idempotent behavior but has NO adoption recovery: an ambiguous create surfaces as a throw, the base is not advanced, and the docs mirror re-creates the page on the next tick — orphaning the page the lost create may have already made and duplicating it in the nested-page tree. This is spec-conform under ns-5 (adoption was scoped to the spine adapter only), and the tree reconcile matches child pages by title so the window is bounded, but it is a genuine gap. Candidate for the docs-mirror hardening sprint: give DocsPageAdapter a title-scoped adoption recovery (match an existing child_page under the parent by title before re-creating), mirroring the spine adapter's ns-5 recovery.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)