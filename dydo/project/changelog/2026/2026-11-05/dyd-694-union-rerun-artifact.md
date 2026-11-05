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
  `needs.<leg>.result == success` first.
- The union then reads the run's job record for its own attempt
  (`actions/runs/<id>/attempts/<attempt>/jobs`), plus each attempt's `run_started_at`, into
  `coverage-platforms/run-jobs.jsonl` and `run-attempts.jsonl`. This needs `actions: read`, so both
  union jobs declare `permissions: actions: read, contents: read`. A rerun copies each leg it does
  not rerun into the new attempt. The copy has a new job ID and the new `run_attempt`, but it keeps
  the original `started_at`. So a leg ran in the latest attempt that had started by its
  `started_at`. Run 42050664030 shows this: its attempt-2 record lists the copied Linux legs with
  `run_attempt: 2` and the attempt-1 start time.
- `coverage_artifacts.py select` requires each leg's latest job to have succeeded. It then requires
  exactly the artifact `<stack>-coverage-<platform>-attempt-<N>`, where N is the attempt in which
  that leg ran. A newest upload that is missing fails; selection never falls back to an older
  attempt. The identity must name the commit that the union passes as `--expected-commit`, and
  so must the report's `candidate.commit`. For python that commit is `github.sha`. For dotnet it is
  `github.event.pull_request.head.sha || github.sha`, which is what each leg checks out. The
  selected artifact moves to the path the union step already reads, and the step writes
  `coverage-platforms/artifact-selection.json`.
- Selection fails closed with exit 2 and a named message in these cases: a missing, failed or
  ambiguous leg job, an incomplete attempt record, a missing artifact for the attempt in which a
  leg ran, an artifact newer than that attempt, an unexpected artifact, a duplicate leg attempt, an
  identity or report that names another leg, attempt or candidate, or an occupied destination.

## Proof

- `DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py`: tests for the job record, selection
  and the command. Regressions cover a missing newest upload, matching identities of the wrong
  candidate, and a report that measured another candidate. Run 42050664030's own attempt-2 record
  is a fixture. A `ci.yml` structure test rejects a broad pattern, a bare `name:` download, an
  `artifact-ids:` download, an upload name without an attempt, a missing `actions: read`, the
  wrong expected commit and a job record that is not tied to the union's attempt.
- A local simulation runs on the incident zips and the incident run's own attempt-2 job record. The
  old selection takes Windows 13424016494, and the union exits 2. The new selection takes
  `windows-attempt-2` and `linux-attempt-1`, and the union exits 0. A replay of both review probes
  exits 2 with a named message.
- Hosted run 42050664030 (push of `78d6eca0`, highest-attempt selection) forced attempt 1's Windows
  legs to fail, then ran `gh run rerun --failed`. Both unions passed in attempt 2. The python union
  consumed `python-coverage-linux-attempt-1` 13428431528 and `python-coverage-windows-attempt-2`
  13429494768, and skipped `python-coverage-windows-attempt-1` 13427640877 and attempt 1's
  `python-coverage-union` 13428261741. The dotnet union consumed `dotnet-coverage-linux-attempt-1`
  13426743730 and `dotnet-coverage-windows-attempt-2` 13428337650, and skipped
  `dotnet-coverage-windows-attempt-1` 13427265924. Its attempt-2 job record is the fixture
  `HOSTED_RERUN`.
- Hosted run 42070066159 on PR #281 (head `da92b64d`, the switch on top of this selection) did the
  same through the `pull_request` event. Attempt 1's Windows legs and both unions failed. After
  `gh run rerun --failed`, both unions passed in attempt 2. Each union's own attempt-2 job record
  listed the copied Linux legs with `run_attempt: 2` and `started_at` 16:59:07Z, before attempt 2's
  `run_started_at` of 17:15:11Z, and the rerun Windows legs as starting after it. The python union
  expected merge commit `98342b78` and consumed `python-coverage-linux-attempt-1` 13437162743 and
  `python-coverage-windows-attempt-2` 13436649801. It skipped `python-coverage-windows-attempt-1`
  13436285695, and its pattern did not match attempt 1's `python-coverage-union` 13435419857. The
  dotnet union expected head `da92b64d` and consumed `dotnet-coverage-linux-attempt-1` 13437236154
  and `dotnet-coverage-windows-attempt-2` 13435818734. It skipped `dotnet-coverage-windows-attempt-1`
  13436390428, which has the larger ID.

## Files Changed

`.github/workflows/ci.yml` (only the python-coverage and dotnet-coverage stamp and upload steps, and
the union jobs' permissions, download, job record and selection steps),
`DynaDocs.Tests/coverage/coverage_artifacts.py`,
`DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py`, `DynaDocs.Tests/coverage/test-associations.json`,
`dydo/reference/coverage-tools.md`, and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
