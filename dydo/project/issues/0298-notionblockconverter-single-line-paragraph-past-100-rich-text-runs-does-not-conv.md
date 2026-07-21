---
title: NotionBlockConverter: single-line paragraph past ~100 rich_text runs does not converge
id: 298
area: backend
type: issue
severity: low
status: open
found-by: review
date: 2026-07-21
---

# NotionBlockConverter: single-line paragraph past ~100 rich_text runs does not converge

The per-block rich_text overflow (ns-7 item 1) splits a >200KB single logical line into sibling paragraph blocks; the join newline re-parses into the paragraph and re-splits at a shifted boundary, so norm never reaches a fixed point (it oscillates).

## Description

Discovered during ns-7 converter-hardening review (Probe2). `NotionBlockConverter.ParagraphBlocks` emits sibling paragraph blocks when a single paragraph exceeds ~100 rich_text runs (Notion's per-array cap). On read, sibling paragraphs join with a newline; a single unbroken logical line thus gains a newline that the next parse absorbs as a soft break and re-splits one run later, so norm^1..^4 are pairwise distinct (no fixed point).

Impact: none in practice. No synced record comes within orders of magnitude of ~100 runs (that needs ~200KB of unbroken, newline-free text in one markdown block); the fixed-point sweep never exercises it. Documented in the ParagraphBlocks doc-comment and pinned by NotionBlockConverterTests.GiantSingleLineParagraph_DoesNotConverge_DocumentedInstability.

Possible fix if it ever matters: carry an explicit continuation marker on overflow siblings so the reader rejoins them without inserting a newline, or hard-split only at existing newline boundaries. Not worth the complexity now.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)