---
area: project
type: changelog
date: 2026-11-04
---

# DYD-547 — PathUtils Windows root forms tested on every host

## Summary

`PathUtils.RootPrefix` and `ServerShareLength` failed DR 048's HCRAP ≤ 20 in the Linux .NET
coverage gate: 40.53 (cc 13, cognitive 9, 6/14 covered) and 29.0 (cc 5, cognitive 4, 0/7). Their
Windows UNC and device branches sat behind `OperatingSystem.IsWindows()`, so a Linux run never
executed them.

The host reading is now a parameter. Public `CollapseRelativeSegments(path)` keeps its signature
and passes `OperatingSystem.IsWindows()` to an internal `CollapseRelativeSegments(path, windows)`,
which hands the flag to `RootPrefix`. Tests reach the overload through the existing
`InternalsVisibleTo("DynaDocs.Tests")`. That substitution is the only logic change, so every host
keeps base `00d09e65`'s output byte for byte.

One shared `RootForms` table pins 31 inputs, each with its Windows and other-host reading. It covers
`//server/share`, a bare `//server`, `..` stopping at the share, `//?/C:`, `//?/UNC/server/share`
(with and without a server, and a lowercase `unc` whose `..` stops at the share), `//.`, too-short
and slashless device forms, drives, and the other-host reading of a leading `//` as `/`. The host
theory checks the running host's column. A new theory checks both columns through the seam on any
host.

## Characterisation and proof

- Before any production edit, the 16 new rows were added to the host theory and run at base on
  Linux: 107/107 PathUtils tests passed. The Windows column came from base code compiled in
  `/tmp` with only the `OperatingSystem.IsWindows()` token replaced by a flag. Its first 14 rows
  equal DYD-396's existing table in both columns.
- The seam test failed to compile before the overload existed (CS1739).
- Red proof against three deliberate `RootPrefix`/`ServerShareLength` breaks. Only the new theory
  failed: 6, 3 and 5 rows. The host theory stayed green on Linux each time, which shows these
  branches were unreached before.
- First-round review nit: a case-sensitive `UNC/` match survived the table. The added lowercase
  `//?/unc/s/sh/..` row fails against that mutant (`OrdinalIgnoreCase` to `Ordinal`).
- Local AltCover 9.0.102 runs the PathUtils tests and reads `GateMetrics --syntax` on a `/tmp` copy:

| Member | Base | Candidate |
|---|---|---|
| `RootPrefix` | cc 13, cog 9, 6/14 seq (3/11 branch), HCRAP 40.53 | cc 13, cog 9, 14/14 seq (11/11 branch), HCRAP 9 |
| `ServerShareLength` | cc 5, cog 4, 0/7 seq, HCRAP 29 | cc 5, cog 4, 7/7 seq (4/4 branch), HCRAP 4 |

These are local measurements. The canonical static and coverage gates are the captain's
final-candidate run.

## Files Changed

- `Utils/PathUtils.cs`: internal `windows` seam on `CollapseRelativeSegments` and `RootPrefix`.
- `DynaDocs.Tests/Services/PathUtilsTests.cs`: `RootForms` table and the both-readings theory.
- `dydo/project/changelog/2026/2026-11-04/dyd-547-pathutils-hcrap.md`: this entry.

code-writer/model: claude-opus-5-5.

## Related

- [Testing strategy](../../../../guides/testing-strategy.md)
- [Coverage tools](../../../../reference/coverage-tools.md)
