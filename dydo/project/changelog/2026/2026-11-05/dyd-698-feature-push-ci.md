---
area: project
type: changelog
date: 2026-11-05
---

# DYD-698 CI runs on pushes to feature branches

## Summary

`ci.yml` now also runs on pushes to `feature/**`
([DYD-698](https://linear.app/bodnar-balazs/issue/DYD-698)). Before, it ran only on pull requests
and on pushes to master/main. So the merge commits that `dydo merge` makes on a feature tip got no
hosted verdict. A PR may merge while behind the tip, so the merged tree can differ from the tree
its PR CI tested. DYD-664's merge review failed for this reason, and the landing needs a hosted
verdict on the integrated feature tip.

## Changed

- `on.push.branches` is `[master, main, 'feature/**']`. `pull_request` is unchanged. A feature-tip
  push runs every existing job unchanged; no job, leg or matrix entry was added.
- A workflow-level `concurrency` block with `cancel-in-progress: true`. A first-attempt push to
  `feature/**` joins the group `ci-<ref>`, so a newer push to the same feature branch cancels the
  superseded run. Every other run, a master/main push, a pull request or a rerun, joins
  `ci-<run_id>`, a group of its own, so nothing cancels it and a run for another ref never cancels
  a feature-tip run. Pull requests keep their earlier behaviour, where no run cancels another.
  Group names are case-insensitive; feature branches are lowercase `feature/<project-slug>`.
- A rerun (`github.run_attempt` above 1) never joins the branch group: after push B supersedes A,
  a rerun of A neither cancels tip B nor is cancelled by it, and rerunning a tip's own failed jobs
  works as before, in that run's own group.
- On a push, `github.event.pull_request.head.sha` is empty, as on master/main pushes today. Only the
  dotnet checkouts and the dotnet union's `--expected-commit` use
  `github.event.pull_request.head.sha || github.sha`, so they resolve to the pushed commit. The
  python jobs use the default checkout ref, and the python union expects `github.sha`; on a push
  both are the pushed commit. The preflights record `PR_HEAD_SHA` as an empty string, and the
  dotnet preflight compares the candidate with `PR_HEAD_SHA or MERGE_REF_SHA`, which is
  `github.sha`.
- `release.yml` is untouched and still triggers only on `v*` tags and dispatch.

A feature branch that is also an open PR's head runs twice per push: once for `push` and once for
`pull_request`. The push run tests the branch tip. The PR run's dotnet jobs test the PR head and its python jobs
the merge ref, as for any pull request. This applies
while a feature branch has an open PR into the default branch, as at landing; the two runs are in
different groups, so neither cancels the other.

## Proof

- `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`:
  `CiWorkflow_RunsOnFeatureTipPushesAndCancelsOnlyASupersededRunOnTheSameFeatureBranch` pins the
  active `on` block, the active top-level `concurrency` block, and that no job declares its own
  `concurrency`. It fails on base `cf03a4aa`, at the missing `'feature/**'` branch, and passes with
  this change.
- `CiWorkflow_RerunOfASupersededFeatureRunTakesItsOwnConcurrencyGroup` pins that only a first
  attempt joins the branch group. It fails on the first hop's `ci.yml` (`88371d14`), whose branch
  arm lacks `github.run_attempt == 1`, and passes with this change.

## Files Changed

`.github/workflows/ci.yml` (the trigger and the new concurrency block only),
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
