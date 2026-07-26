---
title: Watchdog resumed with stale spine state and minted ~300 duplicate GUID records
id: 306
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-24
---

# Watchdog resumed with stale spine state and minted ~300 duplicate GUID records

On 2026-07-24 ~11:45-11:58Z the auto-started watchdog 404ed on a data source absent from the live board (5c57cc81), then created 302 local GUID-named duplicates of board records (74 issues, 214 resolved, 3 future-features, 11 slices, 5 tasks) and pushed the session's 5 fresh issue files up 2-3x each (~9 duplicate board pages); watchdog now stopped with hold marker, cleanup pending a supervised recovery.

## Description

Evidence: watchdog.log tick_error 404 object_not_found data_source 5c57cc81 at 11:45Z, then sync_tick created=16/154/137/1 through 11:58Z; GUID file mtimes 11:49-11:54Z match. Board audit via API: Issues data source holds 314 pages and the ONLY duplicated titles are the five issues filed this session (0300-0304), so the board was not mass-duplicated — the damage is asymmetric: remote->local GUID duplicates plus repeated local->remote pushes of the new files inside the confused window. Four tracked local files were also body-rewritten by round-trip (blank-lines-after-heading stripped, e.g. issue 0213). One legit conflict shadow exists under _system/notion_sync_spine/Issue/. Recovery needs a decision: likely delete the untracked GUID files, archive the 9 duplicate board pages, then re-seed state with a supervised 'dydo notion sync' (mind the mass-delete fuse) or 'dydo notion reset' with the repo as canon. Design gaps this exposes: no mass-CREATE fuse mirroring the mass-delete fuse; an unmapped remote page is materialized as a new GUID file instead of attempting adoption by matching an existing local record; watchdog auto-start resumed into a state whose tracked data sources no longer exist instead of failing loudly; _system/notion_sync_spine is neither committed-by-convention nor gitignored.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Recovered 2026-07-24: deleted 296 duplicate GUID files + 1 stale conflict shadow, adopted the 5 board-born task records as tracked kebab files, restored round-trip-mangled issue 0213 from git, then ran dydo notion reset (7 databases re-minted from repo; board verified 306 issues / 0 duplicate titles) and restarted the watchdog (first ticks quiet, 0 creates). Hardening follow-ups carved out to issue 0307.