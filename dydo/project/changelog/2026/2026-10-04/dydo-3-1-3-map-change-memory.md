---
area: general
type: changelog
date: 2026-10-04
---

# dydo 3.1.3 - Map change memory

## Summary

`dydo map` remembers the last successfully fetched map for each project you open. On a revisit,
including after restarting dydo, the saved map appears while fresh Linear data loads. It stays
visible for at least two seconds after rendering and fitting in the viewer, so you can see the
previous state before the existing refresh animation shows what changed.

## Changed

- Each opened project keeps one latest snapshot on this machine, scoped to the Linear endpoint
  and API key. Fresh fetching starts after the saved lookup and runs alongside the saved map's
  layout and viewing window. After that window, fresh data replaces the snapshot automatically;
  changed maps use the existing refresh animation, and unchanged maps settle without a change
  animation.
- First visits and missing, unreadable or invalid snapshots load normally. If a fresh fetch fails
  while a saved map is displayed, the viewer keeps that map and its saved timestamp and shows a
  warning. The saved snapshot is replaced only by a successful fetch.
- Snapshots are disposable display data under the user's local application-data directory,
  in `dydo/map`. They can be deleted; their absence returns the project to normal first-visit
  loading. This is one latest snapshot per project, with no history browser or automatic expiry.
- Projects are fetched only when opened. There is no prefetch of unopened projects, polling or
  background synchronization. Linear remains authoritative, and `dydo map` only reads it.

## Files Changed

Release preparation changes `DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`npm/test/install.test.cjs`, this entry and `dydo/project/changelog/_changelog.md`. The map behavior
is delivered by the separate map-memory implementation and integrated before release publication.

## Upgrade

Install or update dydo to 3.1.3. No project configuration changes are required. A snapshot becomes
available after the first successful map fetch for that project.

## Related

- [dydo 3.1.2 - Map labels](../2026-10-02/dydo-3-1-2-map-labels.md)
