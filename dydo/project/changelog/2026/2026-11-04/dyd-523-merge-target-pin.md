---
area: general
type: changelog
date: 2026-11-04
---

# dydo 3.6.0 - Merge target pin (unreleased)

## Summary

`dydo merge` now requires `--match-base-commit <sha40>`, the target branch tip the caller pinned
([DYD-523](https://linear.app/bodnar-balazs/issue/DYD-523),
[DR 061](../../../decisions/061-agents-merge-only-into-feature-branches.md) amendment 2026-11-04).
The tip is read from the branch ref, never from the PR's `base.sha` or `baseRefOid`, and a target at
any other tip is refused with nothing written. The pin only refuses: the target is still the PR's
base, and every earlier check holds.

## Changed

- `dydo merge <pr> --match-head-commit <sha40> --match-base-commit <sha40> [--dry-run]`
  ([CLI Commands](../../../../reference/dydo-commands.md#dydo-merge)). A missing, short or non-hex
  pin is refused before any network call; the head pin is checked first. A base whose tip is not the
  pin, dry run or real, refuses with nothing written:
  `refused: <base> is at <tip12>, not the pinned <pin12>; nothing merged - re-pin the target and re-contract`.
- The two-parent path writes parents pin and SHA and moves the ref without force. The `POST /merges`
  path re-reads the ref just before the call and refuses on a mismatch; after it, a merge whose first
  parent is not the pin exits `2` with an `error:` line naming both SHAs.
- The output names the pin: `would merge <sha12> into <base> at <pin12> (PR #<n>)` and
  `merged PR #<n> (<sha12>) into <base> at <pin12> as <merge12>; PR marked merged: ...`. The merge
  commit gains the trailer `Target-Tip: <pin40>` after `Reviewed-Candidate`.
- A closed PR already in its feature base still exits `0` with `already integrated`, whatever valid
  pin a rerun passes.
- The Codex `prefix_rule` match examples `dydo init` writes, the help line and the code-writer's
  `merge` resource carry the new argument. The resource pins the target from the branch ref and takes
  the command's own output line as the merge receipt.

## Upgrade steps

dydo 3.6.0 is unreleased. After its release, every caller of `dydo merge` adds
`--match-base-commit` with the target tip read from the branch ref, for example
`git ls-remote origin refs/heads/<base>`. Rerun `dydo init codex --join` to refresh the rule's match
examples; the rule's pattern is unchanged, so the Claude allow entries need no change.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Services/Merge/PullRequestMerge.cs`, `Commands/MergeCommand.cs`, `Commands/HelpCommand.cs`,
`Commands/InitCommand.cs`, `.codex/rules/dydo.rules`, `DynaDocs.Tests/Merge/` (`MergeCommandTests.cs`,
`MergeScenario.cs`, `PullRequestMergeTests.cs`), `DynaDocs.Tests/Integration/InitCommandTests.cs`,
`DynaDocs.Tests/Integration/GuardWorkerLaneMergeTests.cs`, `dydo/reference/dydo-commands.md`,
`Scaffold/dydo/reference/dydo-commands.md`,
`dydo/project/decisions/061-agents-merge-only-into-feature-branches.md`,
`skills/roles/crew/code-writer/resources/merge.md`, and this entry.

## Related

- [DR 061 - Agents Merge Only into Feature Branches](../../../decisions/061-agents-merge-only-into-feature-branches.md)
- [dydo 3.6.0 - Safe merge command](../2026-10-29/dydo-3-6-0-safe-merge-command.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
