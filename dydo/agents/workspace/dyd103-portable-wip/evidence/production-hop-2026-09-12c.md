# DYD-103 production hop evidence — 2026-09-12c (frozen candidate, gates 3/9/8)

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`,
worktree `.worktrees/dyd103-prod`, resumed at `57c9889e` on admiral ruling 2.

## Last authorized cross-ownership edit

Admiral ruling 2 authorized updating only the inline documented dotnet stack's mutation row in
`DynaDocs.Tests/Features/testing-facade.feature` (mirroring DYD-96's docs hop `c9ffeb16`), whose C#
step `AssertConcreteManifestShape` (`TestingFacadeSteps.cs:210-222`) deep-compares it with the
active `gap_check.json` `stacks[0]`. The row is now the configured adapter row; no other line of
the feature changed and no `TestingFacadeSteps.cs` copy exists (the step receives the JSON as its
step argument). Recorded in `owned-paths.json` and the spec's `Ruled — 2026-09-12` entry for
DYD-113/DYD-91.

## Gate 3 — full isolated .NET suite

`$env:PYTHON="$P"; $P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal`
→ exit 0: Passed 2484, Skipped 2, Total 2486, Duration 11 m 18 s. The previous single failure
(the `Schema 1 has one small concrete manifest and result shape` scenario) is cleared.

Frozen candidate after commit: see the hop SHA below.
