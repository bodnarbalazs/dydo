---
area: project
type: changelog
date: 2026-11-06
---

# DYD-717 Every CI job checks out the run's own commit

## Summary

Every job in `ci.yml` now checks out `github.sha`, the run's own commit
([DYD-717](https://linear.app/bodnar-balazs/issue/DYD-717)). A pull_request run takes `ci.yml`
from the merge ref, but `dotnet-coverage`, `dotnet-coverage-union` and `guard-windows` checked out
the PR head. A helper or lock that `ci.yml` started to name was then missing from every older PR
head, and those jobs failed with no defect in the PR. This happened three times on 2026-11-05:
`dotnet_platform.py` (DYD-558), `DynaDocs.Tests/coverage/pnpm-lock.yaml` (DYD-553) and
`coverage_artifacts.py` (DYD-694).

## Changed

- Every `actions/checkout` step sets `ref: ${{ github.sha }}`: on a pull request the merge ref
  whose `ci.yml` is running, on a push the pushed commit. Jobs that used the default checkout
  (`python-coverage`, `python-coverage-union`, `assurance-cancellation`, `viewer`, `build`,
  `full-stack`) already got this commit and now name it explicitly. No job checks out
  `github.event.pull_request.head.sha`.
- Hosted pull-request runs of `dotnet-coverage`, `dotnet-coverage-union` and `guard-windows` now
  measure the merge ref, the tree that would land, not the PR head. Push runs are unchanged.
  `python-coverage` already measured the merge ref.
- The dotnet union's `--expected-commit` is `${{ github.sha }}`, the same as each leg's checkout and
  the python union's.
- The `dotnet-coverage` preflight asserts that the candidate equals `MERGE_REF_SHA` (`github.sha`).
  It still records both `pr_head_sha` and `merge_ref_sha` in `preflight.json`.
- DR 048 has a dated note on the .NET amendment. Ratification of the measurement change is pending
  in [DYD-744](https://linear.app/bodnar-balazs/issue/DYD-744). `testing-strategy.md` and
  `coverage-tools.md` now say the jobs measure the merge ref.

## Proof

- `DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py` `WorkflowTests`:
  `test_every_job_checks_out_the_runs_own_commit`,
  `test_each_legs_checkout_is_the_unions_expected_candidate` and
  `test_dotnet_preflight_asserts_the_run_commit_and_records_both_shas` fail on base `5d3275c2`, at
  the default and head checkout refs and the head-accepting preflight, and pass with this change.
- `test_a_job_checking_out_another_ref_is_rejected` mutates each of the nine checkout jobs to the
  head ref, the head-only ref and the default ref, and adds a `git checkout` step to
  `guard-windows`; `test_a_leg_and_union_that_disagree_are_rejected` and
  `test_a_dotnet_preflight_that_accepts_the_head_or_drops_a_sha_is_rejected` mutate the legs, unions
  and preflight. Each mutation is rejected.

## Files Changed

`.github/workflows/ci.yml` (checkout refs, the dotnet union's `--expected-commit`, the dotnet
preflight assertion and comments), `DynaDocs.Tests/coverage/tests/test_coverage_artifacts.py`,
`dydo/guides/testing-strategy.md`, `dydo/reference/coverage-tools.md`, DR 048, and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
