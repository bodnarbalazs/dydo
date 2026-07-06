---
title: SnapshotService.cs has private ResolveLink that duplicates LinkResolver — route through ILinkResolver.ResolveToRelativeKey
id: 204
area: backend
type: issue
severity: low
status: resolved
found-by: manual
date: 2026-05-19
resolved-date: 2026-07-04
---

# SnapshotService.cs has private ResolveLink that duplicates LinkResolver — route through ILinkResolver.ResolveToRelativeKey

SnapshotService.cs:173 carries a private static ResolveLink that mirrors the (now-deleted) DocLinkResolver's anchor-strip + segment-walk logic, used only to build the snapshot outgoing-links list; orphaned in spirit by #0187 since the cross-resolver-divergence surface should converge on ILinkResolver.ResolveToRelativeKey.

## Description

Flagged in Dexter's review of the link-validator-fix slice (commit 5783867) and acknowledged in Brian's deviation notes. #0187 deleted Services/DocLinkResolver.cs and routed DocGraph through ILinkResolver.ResolveToRelativeKey, but Services/SnapshotService.cs:173 still holds a private static ResolveLink with the same anchor-strip + segment-walk shape, called only from SnapshotService.ExtractDocLinks to populate the snapshot outgoing-links list. The spirit of #0187 — single resolver, no parallel implementations — leaves this leftover. Fix is to inject ILinkResolver (or wire a new LinkResolver() directly, matching GraphCommand/SnapshotService callsites) and replace the private helper with _linkResolver.ResolveToRelativeKey(...). Bundle is small (one method deletion + one call-site swap). Out of scope for the original slice but worth closing the parallel-implementation surface for real.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated by the 2.0 pivot: SnapshotService.cs was removed with the audit/snapshot teardown, so the duplicated private ResolveLink is gone. Triage sweep 2026-07-04 (Brian, CoS).