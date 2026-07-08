---
title: dydo fix has no directory-scoped mode - repo-wide side effects on shared dirty trees
id: 248
area: backend
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# dydo fix has no directory-scoped mode - repo-wide side effects on shared dirty trees

dydo fix always runs repo-wide: an agent needing one _index regen (e.g. after adding a guide) also rewrites ~35 unrelated _index files, converts wikilinks, and normalizes frontmatter across the whole tree - noisy on a shared dirty tree and forces the sequencer to adopt unrelated churn. Add a path argument scope (dydo fix dydo/guides) matching dydo check's. Surfaced during the DR 038 Phase B landing.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)