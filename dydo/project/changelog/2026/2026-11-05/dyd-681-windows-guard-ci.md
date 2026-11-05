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
  `core.autocrlf false`, checks out the PR head and provisions .NET 10.0.303 with the patched
  runtime. It then restores locked packages and builds the solution with `--warnaserror`.
- The job runs `DangerousPatternCultureTests`, `DangerousPatternOracleTests`,
  `DangerousPatternBudgetTests`, `BashCommandAnalyzerTests` and `GuardSecurityTests` without
  instrumentation, with a TRX logger.
- A following step reads the TRX even when `dotnet test` failed. It fails the job when the test
  step did not succeed, when the TRX is missing or has no results, when any of the five classes has
  no results, when any result is not Passed, or when the TRX counters' total differs from their
  passed count. A skipped test therefore fails the job.
- The job uploads the TRX as `guard-windows-attempt-<run_attempt>`, which neither coverage union's
  download pattern matches. Its job name doesn't match either union's leg names in the job record.
- `dotnet-platforms.json`, the coverage selection, the union and the 80% line / 60% branch /
  HCRAP 20 thresholds are unchanged. The guard tests add no points to either coverage report.
- The release workflow test that pins which `ci.yml` jobs set up .NET now lists `guard-windows`, so
  it also checks that the job provisions the patched runtime.

## Proof

- Locally on Linux, the job's restore and build commands exit 0 with no warnings, and its test
  command passes 1573 of 1573 with none skipped in about 23 s: GuardSecurityTests 575,
  BashCommandAnalyzerTests 937, DangerousPatternBudgetTests 32, DangerousPatternCultureTests 24,
  DangerousPatternOracleTests 5.
- The TRX step's script, taken from `ci.yml` and run under pwsh 7.6.9 the way Actions runs it,
  passes that TRX. It fails when the test step failed or was skipped, and fails on doctored copies
  with one NotExecuted result, with DangerousPatternOracleTests removed, with no results, and with
  passed one short of total.
- Windows can't run locally. Hosted CI on the PR head must show `guard-windows` green, a nonzero
  passed count with zero skips in its log, and the coverage jobs still green.

## Files Changed

`.github/workflows/ci.yml` (the new `guard-windows` job and the comment above `dotnet-coverage`),
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs` (the setup-dotnet job list),
`dydo/guides/testing-strategy.md` (.NET platform coverage), and this entry.

## Related

- [Testing Strategy](../../../../guides/testing-strategy.md#net-platform-coverage)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
