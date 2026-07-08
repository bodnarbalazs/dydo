---
title: dydo check reports 44 pre-existing errors - validator/taxonomy debt inventory
id: 249
area: project
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# dydo check reports 44 pre-existing errors - validator/taxonomy debt inventory

Repo-wide dydo check currently reports 44 errors: invalid type backlog frontmatter (the validator predates DR-034 taxonomy - likely resolves or re-shapes with the migration), wikilinks in issues/decisions/inquisitions, broken links in resolved issues, and a missing project/releases/_index.md. Inventory from Paul during the DR 038 Phase B landing; triage after the DR-034 migration lands since several classes are moot or fixed by it.

## Update 2026-07-09 (CoS hygiene pass, pre-v2.0.6)

Tree-wide count reduced 52 → 33 errors (21 → 6 warnings): dydo fix regen (indexes, orphan
linking), relative-path repairs in resolved-issue files (depth change from the resolved/ moves),
releases/_index.md hub created, 3 backlog summary paragraphs added, gitignored-archive link
de-linked to prose. THE RESIDUE (33) is dominated by ONE class: literal `[label](#section)`
examples inside the issues that DOCUMENT that validator bug (0205/0186/0188 + index rows quoting
them) — not fixable by editing docs; needs the 0205 validator fix (code). The remaining orphan
flags are the same files. Route the residue together with 0205 into sprint H1.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)