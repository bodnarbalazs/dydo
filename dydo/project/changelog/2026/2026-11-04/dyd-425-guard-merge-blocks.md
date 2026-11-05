---
area: general
type: changelog
date: 2026-11-04
---

# dydo 3.6.0 - Guard blocks raw merges (unreleased)

## Summary

The guard now hard-blocks the agent routes around `dydo merge`
([DR 061](../../../decisions/061-agents-merge-only-into-feature-branches.md) decision 7, DYD-417,
DYD-425). Until now only Claude's classifier stopped them. The blocks apply to every caller, top-level
and sub-agent, under Claude's Bash and PowerShell tools and Codex's argv arrays. Each refusal names
`dydo merge`.

## Added

- A raw PR merge is refused into any base, because the guard cannot read a PR's base offline:
  - `gh pr merge`, including `--auto`, `gh -R o/r pr merge` and `gh.exe`;
  - `gh api` on `pulls/<n>/merge`, `merge-async` or `merges`, as a path or a URL;
  - `gh api graphql` with `mergePullRequest`, `enablePullRequestAutoMerge` or `mergeBranch`.
- A `git push` that updates `master`, `main` (in any case) or the branch the local `origin/HEAD`
  names is refused, and the refusal says the default branch is the user's. The pushed branch is the
  refspec's destination, `HEAD` stands for the current branch, and a push with no refspec goes to the
  current branch's upstream. Git state is read offline in the call's directory. State git cannot read
  never blocks on its own.
- A default-branch change is refused: `gh repo edit --default-branch`, or a `gh api` PATCH of
  `repos/<o>/<r>` with `default_branch`.

`gh pr view`, a commit message, heredoc body or grep pattern that mentions a merge, `git push origin
feature/x` and `dydo merge` in the worker lane all still pass. A conflicted merge's push to its
feature branch passes. The blocks run with the other hard blocks, before every nudge.

## Changed

- `git push --force-with-lease origin main` passes the dangerous patterns as before, but is now
  refused as a push to the default branch.

## Residuals

Guards stop accidents, not rogue agents. These routes are not read:

- an `-EncodedCommand`, or nesting deeper than eight wrappers (inside a git tree the PR-ready check
  refuses them unread);
- a GraphQL query or API body read from a file or stdin;
- aliases, functions, scripts and push destinations that only git config sets.

## Related

- [Guard System](../../../../understand/guard-system.md#also-enforced)
- [Working-Tree Contract](../../../../guides/working-tree-contract.md#who-does-what)
