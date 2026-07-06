---
title: Two parallel link resolver implementations (LinkResolver vs DocLinkResolver)
id: 187
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# Two parallel link resolver implementations (LinkResolver vs DocLinkResolver)

LinkResolver (used by check rules) and DocLinkResolver (used by graph) carry equivalent logic on the inputs that surface today but have four named algorithmic differences that are latent tripwires.

## Description

**Category:** antipattern. No production divergence observed today, but the duplication is a maintenance/fix hazard.

**Two implementations:**
- Services/LinkResolver.cs — public class implementing ILinkResolver. Used by BrokenLinksRule.cs:46 and OrphanDocsRule.cs:132-138.
- Services/DocLinkResolver.cs — internal static class. Used by DocGraph.cs:33 only.

**Four algorithmic differences:**

1. **Anchor handling.** DocLinkResolver.cs:12-15 strips a trailing #… even though LinkExtractor.cs:69-74 has already done so. Dead in production but a tripwire for any future caller that bypasses LinkExtractor.
2. **Empty-target handling.** DocLinkResolver returns null for empty targets (anchor-only links); LinkResolver has no such early-exit and lets the empty target flow into Path.Combine. This is the indirect surface for issue #186.
3. **./.. walk.** DocLinkResolver walks .. segments against the relative string manually (DocLinkResolver.cs:27-42). LinkResolver delegates to Path.GetFullPath (PathUtils.ResolvePath:54-59). On a path that walks above the docs root, the OS resolver continues up the filesystem while the manual walker stops at empty — divergent but latent.
4. **Case handling.** LinkResolver.cs:18 compares with OrdinalIgnoreCase; DocGraph lowercases via PathUtils.NormalizeForKey (PathUtils.cs:159). Equivalent on case-insensitive filesystems; potentially divergent on Linux.

**Why this matters for issue #185.** Any fix that converges dydo check's scope to the docs root will likely route both consumers through one of these resolvers (or a third). The fix should be aware that the codebase pretends to have one resolver behaviour while actually carrying two.

**Suggested approach:** delete DocLinkResolver and have DocGraph call ILinkResolver (extracting whatever resolution-only method it needs). Add a parameterised cross-shape agreement test as named in finding 4.

**Reference:** dydo/project/inquisitions/link-validator-resolver-divergence.md finding 3.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated: DocLinkResolver.cs was deleted at HEAD (spot-verified absent); DocGraph converged on ILinkResolver, eliminating the parallel-resolver duplication. Triage sweep 2026-07-04 (Brian, CoS).