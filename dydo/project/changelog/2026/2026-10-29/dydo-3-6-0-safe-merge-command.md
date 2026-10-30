---
area: general
type: changelog
date: 2026-10-29
---

# dydo 3.6.0 - Safe merge command (unreleased)

## Summary

Agents merge a reviewed PR into a `feature/*` branch through `dydo merge`, whose refusals are code
with no override flag; a merge into the default branch stays the user's click
([DR 061](../../../decisions/061-agents-merge-only-into-feature-branches.md), DYD-417, DYD-424).
It replaces a classifier hint that Claude Code applied by judgment, for Claude and LC only.
`dydo init` and `--join` write narrow allow rules for Claude Code and Codex. Existing projects rerun
`--join` in every checkout and re-trust Codex; see Upgrade steps.

## Added

- `dydo merge <pr> --match-head-commit <sha40> [--dry-run]`
  ([CLI Commands](../../../../reference/dydo-commands.md#dydo-merge)). It merges exactly the
  reviewed SHA into the PR's base with a merge commit, and refuses unless:
  - the PR is in the repository the checkout's `origin` names, open, not a draft and not from a fork;
  - its base is a `feature/*` branch, not GitHub's default branch (read fresh) nor the local
    `origin/HEAD` name;
  - its head is the SHA;
  - the PR body's `## Independent review` holds a `code`, `docs` or `merge` PASS for that SHA and no
    FAIL;
  - every check on the SHA has finished green, subject to the inert-app exception below, and every
    required check is present.

  A closed PR whose SHA is already in its feature base reports `already integrated` and exits 0.
  Refusals exit 1 with one `refused:` line; tool errors exit 2. The fixed in-code inert-app list,
  keyed by GitHub App slug, contains only `blacksmith-sh`. Its suite is ignored only when its status
  is `queued`, `in_progress`, `pending`, `requested` or `waiting` and `latest_check_runs_count` is
  present as a valid integer zero. Unknown, missing or malformed status and missing or invalid count
  never establish the exception, even for `blacksmith-sh`. Every non-exempt suite is judged normally;
  any unfinished suite then refuses regardless of run count. Unknown states always refuse.
  A suite refusal names the app slug and status. With no suite left, `no checks reported` refuses.
- `dydo init` and `--join` write `Bash(dydo merge:*)` and `PowerShell(dydo merge:*)` into
  `.claude/settings.local.json`, and a `prefix_rule` for `dydo` and `dydo.exe merge` into
  `.codex/rules/dydo.rules` ([Configuration](../../../../reference/configuration.md#hook-configuration)).
- The guard's worker lane allows `dydo merge`, which a Merge Sub-ticket's code-writer runs.

## Changed

- Doctrine names the command. A conflict-free merge of a reviewed PR into its feature branch is
  `dydo merge`, and its merge SHA is the candidate for the combined gates and merge review: the
  working-tree contract, the code-writer's `merge` resource, `ticket-captain` step 7 and `admiral`
  step 4. In `overnight-mode`, standing order 3 routes feature merges through the captain's Merge
  chain and `dydo merge`; genuine Claude/Codex merge and approval parity remains pending in DYD-426.

Guard hard blocks for raw `gh pr merge`, `gh api` merge paths, GraphQL merge mutations, pushes to the
default branch and default-branch changes are planned, delivery pending in DYD-425. The genuine
Claude bash/Codex PowerShell parity gate is planned, pending in DYD-426; allow-rule delivery does not
satisfy it.

## Upgrade steps

dydo 3.6.0 is unreleased. Follow these steps only after its release.

1. Install the 3.6.0 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
2. Merge the release's `skills/` and `Scaffold/dydo/reference/dydo-commands.md` changes into the
   project, keeping its edits, then run `node setup-skills.mjs` from its root.
3. In every checkout `git worktree list` shows, run `dydo init <host> --join` for each wired host.
   For Claude Code it adds the two `dydo merge` allow entries to `.claude/settings.local.json`, which
   stays uncommitted. For Codex it rewrites `.codex/rules/dydo.rules`: commit it, restart Codex and
   re-trust the project, since Codex loads project rules only from a trusted `.codex/` layer.
4. Check that the project's CI runs on PRs into `feature/*` branches; without it `dydo merge`
   refuses every merge with `no checks reported`.
5. A machine carrying a stopgap for agent merges, such as a free-text `autoMode.allow` merge entry or
   a user-level `Bash(gh pr merge:*)` allow rule, keeps it until the Claude/Codex parity gate
   (planned, pending in DYD-426) passes in that project. Then the user removes it; agents cannot edit
   their own permissions.
6. Run `dydo adopt 3.6.0` last, once `dydo check` exits 0 and the update is proven.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Commands/MergeCommand.cs`, `Services/Merge/` (`CommitChecks.cs`, `CommitSha.cs`,
`FeatureBranch.cs`, `GhApi.cs`, `GitHubJson.cs`, `GitHubReply.cs`, `IGitHubApi.cs`,
`IndependentReview.cs`, `MergeCheckout.cs`, `MergeStop.cs`, `OriginRepository.cs`,
`PullRequestMerge.cs`, `ReviewVerdict.cs`), `Program.cs`, `Commands/HelpCommand.cs`,
`Commands/InitCommand.cs`, `Commands/GuardCommand.cs`, `Services/CompletionProvider.cs`,
`.codex/rules/dydo.rules`, `DynaDocs.Tests/` (`Merge/`, `Commands/CommandSmokeTests.cs`,
`Integration/GuardWorkerLaneMergeTests.cs`, `Integration/InitCommandTests.cs`,
`coverage/test-associations.json`), `dydo/reference/dydo-commands.md`,
`Scaffold/dydo/reference/dydo-commands.md`, `dydo/reference/configuration.md`,
`dydo/project/decisions/061-agents-merge-only-into-feature-branches.md`,
`dydo/guides/working-tree-contract.md`, `skills/productivity/overnight-mode/SKILL.md`,
`skills/roles/crew/code-writer/resources/merge.md`, `skills/roles/officers/ticket-captain/SKILL.md`,
`skills/roles/officers/admiral/SKILL.md`, and this entry.

## Related

- [DR 061 - Agents Merge Only into Feature Branches](../../../decisions/061-agents-merge-only-into-feature-branches.md)
- [dydo 3.5.1 - Downstream update fixes](../2026-10-27/dydo-3-5-1-downstream-update-fixes.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
