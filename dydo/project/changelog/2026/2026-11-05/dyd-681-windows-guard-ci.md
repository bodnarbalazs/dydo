---
area: project
type: changelog
date: 2026-11-05
---

# DYD-681 hosted Windows CI runs the guard's tests

## Summary

Hosted CI now runs the guard's analyzer and hook tests on Windows
([DYD-681](https://linear.app/bodnar-balazs/issue/DYD-681)). Before, they ran only on Linux. The
`dotnet-coverage` Windows leg runs only the tests selected for the statechart members that
`dotnet-platforms.json` names, so `DangerousPatternCultureTests`, which start dydo guard under
tr-TR and az-Latn-AZ, never ran on the platform they were written for.

## Changed

- A new `guard-windows` job in `ci.yml` runs on `windows-latest`, in parallel with the other jobs,
  on every trigger that runs `dotnet-coverage`. Like `dotnet-coverage`, it sets
  `core.autocrlf false`, checks out the candidate commit (the PR head on a pull request, the
  pushed commit on a push) and provisions .NET 10.0.303 with the patched runtime. It then restores
  locked packages and builds the solution with `--warnaserror`.
- The job runs `DangerousPatternCultureTests`, `DangerousPatternOracleTests`,
  `DangerousPatternBudgetTests`, `BashCommandAnalyzerTests` and `GuardSecurityTests` without
  instrumentation, with a TRX logger. The job-level `GUARD_CLASSES` variable is the one list of
  these classes: the test step builds its `FullyQualifiedName~<class>.` filter from it, and the
  check step requires each class.
- A following step reads the TRX even when `dotnet test` failed. It fails the job when the test
  step did not succeed, when the TRX is missing or has no results, when any of the five classes has
  no results, or when any result is not Passed. It also fails the job when the TRX has no
  `ResultSummary/Counters`, when the counters' total differs from their passed count or from the
  number of results, or when the number of test definitions differs from the number of results.
  A skipped test or a truncated TRX therefore fails the job.
- The job uploads the TRX as `guard-windows-attempt-<run_attempt>`, which neither coverage union's
  download pattern matches. Its job name doesn't match either union's leg names in the job record.
- `dotnet-platforms.json`, the coverage selection, the union and the 80% line / 60% branch /
  HCRAP 20 thresholds are unchanged. The guard tests add no points to either coverage report.
- The release workflow test that pins which `ci.yml` jobs set up .NET now lists `guard-windows`, so
  it also checks that the job provisions the patched runtime.

## Proof

- Locally on Linux, the job's restore and build commands exit 0 with no warnings. Its test step,
  taken from `ci.yml` and run under pwsh 7.6.9 the way Actions runs it, builds a filter
  byte-identical to the earlier literal and passes 1573 of 1573 with none skipped:
  GuardSecurityTests 575, BashCommandAnalyzerTests 937, DangerousPatternBudgetTests 32,
  DangerousPatternCultureTests 24, DangerousPatternOracleTests 5.
- The TRX step's script, run the same way, passes that TRX. It fails when the test step failed or
  was skipped. It fails on doctored copies with one NotExecuted result, with
  DangerousPatternOracleTests removed, with no results, with passed one short of total, with the
  counters removed, and with one result kept per class while the counters still claim 1573.
- Windows can't run locally. Hosted CI on the PR head must show `guard-windows` green, a nonzero
  passed count with zero skips in its log, and the coverage jobs still green.

## Files Changed

`.github/workflows/ci.yml` (the new `guard-windows` job and the comment above `dotnet-coverage`),
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs` (the setup-dotnet job list),
`dydo/guides/testing-strategy.md` (.NET platform coverage), and this entry.

## Related

- [Testing Strategy](../../../../guides/testing-strategy.md#net-platform-coverage)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
