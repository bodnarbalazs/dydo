---
area: project
type: changelog
date: 2026-11-06
---

# dydo 3.6.0 - A bash line's Windows -C tree is read as written (unreleased)

## Summary

The guard now refuses `git -C C:\Users\...\wt push` from a bash line when the named tree is a
worktree on `main` ([DYD-739](https://linear.app/bodnar-balazs/issue/DYD-739)). Before this change,
hosted Windows run 42127530403 allowed it under Codex's `["bash", "-lc", line]` envelope. That
envelope declares bash, so the guard read the line only as bash would. Bash drops an unquoted
backslash, so it read the tree as `C:Users...wt`, which doesn't exist. The guard resolved no branch
there and let the push through. Claude's Bash and PowerShell tools and Codex's pwsh and bare-argv
forms already refused it: an unmarked Bash call on Windows is read both ways, and PowerShell keeps
backslashes.

## Changed

- When a line read as bash holds a backslash, the merge-route scan also reads it as written, and
  the line takes the routes of both readings. This includes a substitution's text and a wrapper's
  `bash -c` string. A push is refused when the tree under either reading is on a default branch.
- The bash reading's routes are all kept, so nothing is refused less than before. A tree that
  neither reading resolves still blocks nothing on its own, as before.
- Lines read only as PowerShell, lines read both ways already, and bash lines without a backslash
  are read as before. The PR-ready check's readings are unchanged.
- Hosted CI's `guard-windows` job now also runs `GuardMergeRouteTests`.

## Proof

- Red, before the fix, on Linux: `PrCallScannerBackslashTreeTests` and the new
  `GuardMergeRouteTests.PushOfHead_ThroughGitDashCIntoABackslashTreeOnMain_IsDenied` failed 7 of
  11. The scanner returned only `C:Usersmewt`. The hook allowed `git -C w\t push origin HEAD` and
  `git -C <test dir>/w\t push` under claude-bash and codex-bash, the two envelopes read only as bash
  on Linux. They pass with the fix. With the fix reverted they fail again.
- The hosted proof is the `guard-windows` job on the PR head. That job runs the Windows-path case
  `PushOfHead_ThroughGitDashCIntoAWorktreeOnMain_IsDenied` unchanged.

## Residuals

Guards stop accidents, not rogue agents.

- Git Bash itself would hand git `C:Users...wt`, which fails to enter a tree. The refusal covers
  the tree the agent wrote, not only the one bash would enter.
- A bash line whose literal reading misplaces a command, as `echo \; git push origin main` does,
  may now be refused though bash would only echo it.
- A drive-relative `-C C:dir` is still resolved against the guard process's directory on that
  drive, not the call's directory, which is where git resolves it.

## Files Changed

`Services/PrCallScanner.MergeRoutes.cs`, `DynaDocs.Tests/Services/PrCallScannerBackslashTreeTests.cs`,
`DynaDocs.Tests/Integration/GuardMergeRouteTests.cs` (one added test),
`DynaDocs.Tests/coverage/test-associations.json`, `.github/workflows/ci.yml` (`GUARD_CLASSES`),
and this entry.

## Related

- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [DYD-681 hosted Windows CI runs the guard's tests](../2026-11-05/dyd-681-windows-guard-ci.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
