---
area: project
type: reference
---

# DYD-314 — Map memory implementation evidence

[DYD-314](https://linear.app/bodnar-balazs/issue/DYD-314) implements the
[DR 052 amendment](../decisions/052-dydo-map-read-only-linear-view.md) on base
`55378ef911588399b00212aa4324e9ae5c5e3e7c`. The containing commit is the author candidate;
this record does not claim independent review or integration approval.

## Red proof

The first viewer test, `a saved revisit renders the saved map and timestamp while fresh Linear is
blocked`, failed against the original App: `Unable to find an element with the text: /Saved map/`.
After the implementation passed targeted tests, production and documentation changes were stashed,
leaving the tests in place. The same viewer test failed again (one failed, exit 1).

The .NET tests referencing the new cache contract could not compile without it (`CS0246`,
`MapSnapshotCache` and `MapSnapshot` absent). That is contract/compiler evidence, not an executed
scenario failure. For an executed boundary witness, an ignored, temporary xUnit project linked the
unchanged new `MapServerTests.Saved_FirstVisitIsAnExplicitMissWithoutReadingLinear` source and its
existing fake-Linear helpers to the baseline `dydo.dll`, with the existing xUnit versions and friend
assembly name. It ran one test and failed, exit 1:

```text
Assert.Equal() Failure: Values differ
Expected: OK
Actual:   NotFound
```

The test-only rollback stash was restored before final validation. No test assertion was weakened.
The initial .NET `--no-restore` attempt emitted no discovered tests and is not counted as evidence.

## Acceptance surfaces

- `DynaDocs.Tests/Features/map-memory.feature` and `MapMemorySteps` execute first visits, server
  restart with a new port, failed fetch and recovery, Project/key/endpoint isolation, invalid and
  inaccessible storage, simultaneous complete writes, and a saved lookup during a blocked fetch
  followed by cancellation/drain.
- `MapSnapshotCacheTests` exercise normalized endpoint/GUID identity, exact keys, nested graph data,
  schema/timestamp/project/collection validation, duplicate identities, relation endpoints, failed
  writes and cancellation preserving previous snapshots.
- `MapApiTests` prove every fresh request reads Linear, errors never replace saved success, a
  response for a different Project is rejected, and same-Project fresh operations serialize through
  commit while saved reads remain available.
- `ProjectVisit.test.ts` controls monotonic time: no application at 1,999 ms after ready; application
  at 2,000 ms. It covers delayed readiness, late saved results, failure/retry, unchanged diffs,
  layout fallback, and disposal during fetch/hold/layout. `MapCanvas.test.tsx` separately controls
  viewport fitting and animation frames, including StrictMode, empty maps and unmounting.
- `savedVisit.test.tsx` exercises the rendered App and actual A→B→A visits during fetch, hold and
  layout. `saved-map.spec.ts` covers the browser journeys with normal and reduced motion.
- `full-stack.spec.ts` launches the published native binary with an isolated cache per test. Its
  restart journey shares one deliberate cache between two actual processes on different ports,
  verifies the saved map while fresh HTTP is held, then observes the new assignee.

## Gate provenance

Raw author-session logs are ignored under `artifacts/dyd-314/`; configured facade reports are under
`DynaDocs.Tests/coverage/results/`. The captain attaches immutable CI artifacts for review of the
committed candidate. Logs from dirty development runs are not exact-commit CI proof.

The configured .NET coverage adapter cannot execute on this Linux host: its preflight reports
`Owner requires Windows CPython 3.12` and exits 2. The full .NET test suite is therefore also run
directly; the native Windows coverage campaign remains required before final acceptance.
Mutation is unavailable in the configured facade (exit 2), pending DYD-103 for .NET and without an
adopted TypeScript mechanism. Neither limitation is a passing gate.

An initial viewer coverage campaign ran all tests successfully but was invalid because an author
edit changed the outer/inventory source fingerprints. Its report is not passing evidence; the
viewer campaign must be rerun with all source edits settled.

## Empty-canvas fix after review

The first independent review of `885452c4afde8c1b1e8a173a472f93fd2f0cde73` found that
React Flow leaves its fit promise unresolved when there are no nodes. Saved empty maps therefore
never acknowledged paint, never completed their viewing window, and kept Refresh disabled.

The fix skips fitting an empty canvas but retains the existing two animation frames before its
ready acknowledgement. An empty canvas also leaves the first populated layout eligible for fitting,
even when the fit key is unchanged. The visit's 2,000 ms hold and cancellation guards are unchanged.

The new real-browser journeys exercise empty saved → empty fresh and empty saved → populated fresh.
They verify the saved timestamp and disabled Refresh during the hold, then its removal and enabled
Refresh; the populated journey also verifies an issue is inside the viewport. An empty first visit
is a compatibility regression: it already passed before the fix and remains free of a saved hold.
The unit regression now leaves the fit promise unresolved and checks both paint frames in StrictMode;
a second unit regression checks fitting the first populated layout after an empty canvas.

With the final regression sources retained and only `MapCanvas.tsx` restored to `885452c4`, both
saved-map browser tests failed (`Expected: 0`, `Received: 1` saved timestamp), exit 1; both unit
regressions failed (zero ready acknowledgements and zero populated fit calls), exit 1. Restoring
the fix passed all three new browser tests and the 42 focused canvas/visit tests. The existing
controlled-clock visit test still proves no application at 1,999 ms and application at 2,000 ms.

Raw fix-hop commands, exits, before/after source fingerprints, and rollback browser traces are
retained separately under `artifacts/dyd-314/fix-empty/`; original author and reviewer evidence is
preserved. The first browser harness attempt froze ELK before layout and is excluded as defect
proof. Native Windows exact-candidate validation remains the captain's required follow-up; the
Linux .NET coverage and configured mutation limitations above remain unchanged.

## Related

- [Review evidence](./_reviews.md)
- [Testing strategy](../../guides/testing-strategy.md)
