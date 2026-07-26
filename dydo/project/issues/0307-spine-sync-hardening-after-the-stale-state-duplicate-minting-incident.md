---
title: Spine sync hardening after the stale-state duplicate-minting incident
id: 307
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-07-24
---

# Spine sync hardening after the stale-state duplicate-minting incident

Four design gaps let issue 0306 happen: no mass-create fuse, no adopt-by-match for unmapped remote pages, silent re-provision when a tracked data source 404s, and no git policy for _system/notion_sync_spine.

## Description

Carved out of issue 0306 (incident resolved by supervised recovery on 2026-07-24). Hardening work: (1) a mass-CREATE fuse mirroring the mass-delete fuse — abort a tick that would locally mint more than N new records for a type; (2) adopt-by-match — an unmapped remote page whose title/identity matches an existing local record should adopt it instead of minting a GUID-named duplicate file; (3) fail loudly when a tracked data source returns 404 (board recreated / state stale) instead of re-provisioning and reconciling into duplication — the 0306 incident's entry point; (4) decide whether dydo/_system/notion_sync_spine (conflict shadows) is committed or gitignored — today it is neither by convention, so shadows surface as mystery untracked files.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)