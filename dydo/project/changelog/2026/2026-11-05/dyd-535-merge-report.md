---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Merge report waits for GitHub (unreleased)

## Summary

After a good merge write, `dydo merge` no longer reports `PR marked merged: no`
([DYD-535](https://linear.app/bodnar-balazs/issue/DYD-535)). GitHub marks a PR merged
asynchronously, so the single reread right after the write could read `merged:false` for a merge
that had landed, as on PR #228 through the two-parent path.

## Changed

- The receipt ends `PR marked merged: yes|pending|unverified`
  ([CLI Commands](../../../../reference/dydo-commands.md#dydo-merge)). The command rereads the PR; while
  GitHub answers `200` with `merged:false` it rereads up to three more times, pausing one, one and two
  seconds, then reports `pending`: GitHub had not yet marked the PR merged. `no` is gone.
- A reread that fails (non-`200` or no answer) reports `unverified` at once. Exit stays `0` for all three.
- The poll only reads. A dry run and the already-integrated (`204`) path do not poll.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Services/Merge/PullRequestMerge.cs`, `DynaDocs.Tests/Merge/` (`MergeScenario.cs`,
`PullRequestMergeTests.cs`), `dydo/reference/dydo-commands.md`,
`Scaffold/dydo/reference/dydo-commands.md`, and this entry.

## Related

- [dydo 3.6.0 - Merge target pin](../2026-11-04/dyd-523-merge-target-pin.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
