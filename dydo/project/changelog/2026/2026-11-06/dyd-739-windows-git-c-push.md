---
area: project
type: changelog
date: 2026-11-06
---

# dydo 3.6.0 - A bash push's Windows -C tree is read as written (unreleased)

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

- A `git -C` word of a push that bash runs, written with a backslash, is also taken as written:
  the push takes the tree bash reads and the tree as written. This includes a push inside a
  substitution or a wrapper's `bash -c` string. The push is refused when either tree is on a
  default branch.
- The bash reading is otherwise unchanged. Its commands, routes and push destinations are the same
  as before, and nothing is refused less. A backslash elsewhere on the line stays bash's: an escaped
  quote in a commit message or `printf` text, an escaped `;`, and a backslash-newline that continues
  a command add no merge, default-branch change or push. A tree that neither spelling resolves still
  blocks nothing on its own, as before.
- Lines read only as PowerShell, lines read both ways already, and bare argv arrays are read as
  before. The PR-ready check's readings are unchanged.
- Hosted CI's `guard-windows` job now also runs `GuardMergeRouteTests`.

## Proof

- Red, before the first fix, on Linux: `PrCallScannerBackslashTreeTests` and the new
  `GuardMergeRouteTests.PushOfHead_ThroughGitDashCIntoABackslashTreeOnMain_IsDenied` failed 7 of
  10. The scanner returned only `C:Usersmewt`. The hook allowed `git -C w\t push origin HEAD` and
  `git -C <test dir>/w\t push` under claude-bash and codex-bash, the two envelopes read only as bash
  on Linux. They pass with the fix. With the fix reverted they fail again.
- Review round 1 found that the first fix read the whole line as written as well, which refused
  ordinary bash lines. The second fix keeps the as-written spelling to the `-C` word alone. Its red,
  on the first fix: `BashLine_BackslashInTextOrAContinuation_TakesNoRoute` (five lines),
  `BashLine_PushContinuedOnTheNextLine_TakesOnlyTheBashRoute`,
  `BashLine_WindowsTreePushInsideEscapedQuotes_TakesNoRoute` and the hook test
  `GuardMergeRouteTests.BashLine_BackslashInTextOrAContinuation_IsAllowed` failed 8 of 19, the 11
  earlier DYD-739 cases passing. All 19 pass with the second fix.
- The second fix also drops the extra reading's cost: unquoted `$(...)` nested 20 deep on a line
  with a backslash took 899 ms on the first fix and 0 ms on the second.
- The hosted proof is the `guard-windows` job on the PR head. That job runs the Windows-path case
  `PushOfHead_ThroughGitDashCIntoAWorktreeOnMain_IsDenied` unchanged.

## Residuals

Guards stop accidents, not rogue agents.

- Git Bash itself would hand git `C:Users...wt`, which fails to enter a tree. The refusal covers
  the tree the agent wrote, not only the one bash would enter.
- An unmarked Bash call on Windows is still read both ways, as before this change, so an escaped
  quote there can still read as a command.
- A drive-relative `-C C:dir` is still resolved against the guard process's directory on that
  drive, not the call's directory, which is where git resolves it.

## Files Changed

`Services/PrCallScanner.MergeRoutes.cs`, `DynaDocs.Tests/Services/PrCallScannerBackslashTreeTests.cs`,
`DynaDocs.Tests/Integration/GuardMergeRouteTests.cs` (two added tests),
`DynaDocs.Tests/coverage/test-associations.json`, `.github/workflows/ci.yml` (`GUARD_CLASSES`),
and this entry.

## Related

- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [DYD-681 hosted Windows CI runs the guard's tests](../2026-11-05/dyd-681-windows-guard-ci.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
