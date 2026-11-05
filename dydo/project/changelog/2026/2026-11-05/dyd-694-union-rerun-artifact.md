---
area: project
type: changelog
date: 2026-11-05
---

# DYD-694 coverage unions take each leg's latest attempt after a rerun

## Summary

After `gh run rerun --failed`, the CI `python-coverage-union` and `dotnet-coverage-union` jobs now
consume each platform leg's latest-attempt artifact
([DYD-694](https://linear.app/bodnar-balazs/issue/DYD-694)). Before, run 42038108870 reran its
failed Windows python leg, and the leg passed. The attempt-2 union still downloaded the failed
attempt-1 Windows artifact, then failed on its missing `adapters/python-coverage.json`.

`download-artifact@v4` keeps one artifact per name. It picks the largest artifact ID, and IDs are
not time-ordered: the stale artifact was 13424016494, the rerun's 13423254639. The pinned
`@actions/artifact` 2.3.2 applies this choice to `name:` downloads (`getArtifact`), to `pattern:`
downloads and to `artifact-ids:` downloads (`listArtifacts({latest: true})`). So no download input
fixes a duplicate name. `upload-artifact`'s `overwrite: true` deletes on a best-effort basis and
fails silently.

## Changed

- Each coverage leg names its artifact `<stack>-coverage-<platform>-attempt-<run_attempt>`. Before
  uploading, it stamps `artifact-identity.json` with its stack, platform, run ID, run attempt and
  checked-out commit, using `DynaDocs.Tests/coverage/coverage_artifacts.py stamp`.
- Each union downloads `pattern: <stack>-coverage-*-attempt-*` into `results/coverage-legs`. That
  pattern no longer matches a union's own artifact. The union still requires
  `needs.<leg>.result == success`. Then `coverage_artifacts.py select` takes each platform's highest
  attempt. A rerun reruns only failed legs, and a passing leg always uploads, so that highest
  attempt is the leg's latest passing one. The selected artifact moves to the path the union step
  already reads, and the step writes `coverage-platforms/artifact-selection.json`.
- Selection fails closed with exit 2 and a named message in these cases: a missing leg, an
  unexpected artifact, a duplicate leg attempt, an attempt newer than the run, a missing or
  mismatched identity, legs from different commits, or an occupied destination.

## Proof

- `DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py`: tests for selection and the command.
  It also has a `ci.yml` structure test that rejects a broad pattern, a bare `name:` download, an
  `artifact-ids:` download and an upload name without an attempt.
- A local simulation runs on the incident zips. The old selection takes Windows 13424016494, and the
  union exits 2. The new selection takes `windows-attempt-2` and `linux-attempt-1`, and the union
  exits 0.

## Files Changed

`.github/workflows/ci.yml` (only the python-coverage and dotnet-coverage stamp, upload, download and
selection steps), `DynaDocs.Tests/coverage/coverage_artifacts.py`,
`DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py`, and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
