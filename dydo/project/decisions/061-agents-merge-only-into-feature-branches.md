---
area: general
type: decision
status: accepted
date: 2026-10-29
---

# 061 - Agents Merge Only into Feature Branches, Only through dydo merge

An agent merges a reviewed PR into a `feature/*` branch through `dydo merge`, whose refusals are code
with no override flag. A merge into the default branch stays the user's click. Designed in
[DYD-417](https://linear.app/bodnar-balazs/issue/DYD-417), ruled on by Balazs on 2026-10-29 and
amended by the dydo admiral the same day.

## Context

Overnight mode's standing order 3 let admirals merge into their feature branch, never master, but
neither host could grant only that. On 2026-10-29 Claude Code's auto-mode classifier denied a
reviewed, green `gh pr merge` into an LC feature branch as "Merge Without Review", and Balazs merged
it by hand. The stopgap, a plain-English `autoMode.allow` entry, works by the classifier's judgment,
so nothing enforced "never master", and it covered only Claude and only LC. dydo itself blocked no
raw merge: only Claude's classifier had stopped agent merges.

Where the guarantee lives:

- **A guard rule on `gh pr merge`.** A hook sees only text: it cannot learn a PR's base without a
  network call in the hot path, nor stop the base changing between its check and gh's merge.
- **A dydo command** that owns the whole operation, behaves the same under both hosts and takes one
  narrow allow rule. Chosen.

Where the reviewer's PASS is read:

- **The PR body's `## Independent review`**, where doctrine already puts it verbatim; the same
  `GET pulls/{n}` returns the base, the head and the state. Chosen.
- **Linear:** a second network dependency and a Linear key in every agent environment, and no
  harder to forge, since the same agents write those comments.
- **A local receipt:** per machine, invisible to the user, and as writable.

A GitHub ruleset on each default branch would have closed the default-branch-switch race
server-side. Balazs ruled it unnecessary.

## Decision

1. **One path.** Agents merge only into `feature/*` branches, and only through
   `dydo merge <pr> --match-head-commit <sha40> [--dry-run]`. A merge into `master`, `main` or the
   default branch is the user's click.
2. **Refusals in code, no override.** The command takes no repository, base or override option; the
   repository is the one the checkout's `origin` names. The base must match a fixed `feature/*`
   pattern and git's branch-name rule, and is refused when it is GitHub's default branch, read fresh,
   or the local `origin/HEAD` name. The PR head must equal the reviewed SHA, which is exactly what
   merges. A closed PR is refused unless its SHA is already in its feature base (reconcile-first,
   exit 0 `already integrated`). Draft and fork PRs are refused.
3. **Merge commits only.** `POST /merges`, or an explicit two-parent commit where GitHub would
   fast-forward. No squash or rebase path exists.
4. **The PASS comes from the PR body, bound to the SHA.** The `## Independent review` section must
   hold a `code`, `docs` or `merge` PASS whose candidate is the SHA, an abbreviation counting only
   when GitHub resolves it to exactly that SHA, and no FAIL for it. A PASS that names a base needs
   that base inside the target.
5. **CI is green, with a fixed inert-app exception.** Every check suite, latest check run and
   legacy status on the SHA must finish green, and every check the target requires must be present.
   The in-code inert-app list, keyed by GitHub App slug, contains only `blacksmith-sh`: it leaves a
   queued suite with zero runs on every dydo and LC commit. Its suite is ignored only when its
   status is `queued`, `in_progress`, `pending`, `requested` or `waiting` and
   `latest_check_runs_count` is present as a valid integer zero. Unknown, missing or malformed
   status and missing or invalid count never establish the exception, even for `blacksmith-sh`.
   Every non-exempt suite is judged normally; any unfinished suite then refuses, whatever its run
   count. Unknown states always refuse.
   A suite refusal names the app slug and status, for example
   `refused: checks on <sha12> are not green: circleci-checks queued`.
   With no suite left after the exception, the command refuses `no checks reported`.
   Adding an app to the list is a code change with review.
6. **Crew may run it.** The Merge Sub-ticket's code-writer runs it, then a fresh merge reviewer
   judges its merge SHA. The guard allows `merge` in its worker lane: it cannot tell roles apart,
   and doctrine gives the merge to that crew. Decided by the admiral; reversible.
7. **Planned guard hard blocks; delivery pending in
   [DYD-425](https://linear.app/bodnar-balazs/issue/DYD-425).** The guard is to block these other
   paths for agents: raw `gh pr merge` into any base, since it cannot read a PR's base offline;
   `gh api` merge paths (`pulls/<n>/merge`, `merge-async`, `merges`) and GraphQL merge mutations;
   `git push` to the default branch; and default-branch changes. These blocks are decided, not shipped.
8. **Claude/Codex parity is a requirement; its gate is planned, delivery pending in
   [DYD-426](https://linear.app/bodnar-balazs/issue/DYD-426).** Both hosts must get the same
   privileges and the same allow or block outcome for the same command. `dydo init` and `--join`
   write `Bash(dydo merge:*)` and `PowerShell(dydo merge:*)` for Claude and a `prefix_rule` for
   `dydo` and `dydo.exe merge` for
   Codex. The planned gate must run the command matrix through a Codex session in PowerShell and
   a Claude session in bash, in the main checkout and in a fresh T3 worktree; any mismatch is a
   blocker. The allow-rule delivery does not establish this parity. Merging on one host only is
   not an outcome.

## Consequences

- Guards stop accidents, not rogue agents. Accepted residuals:
  - **A forged PR body.** Any agent with the gh token can edit it. Hardening would need a distinct
    reviewer identity, such as a GitHub App submitting an approving review.
  - **Wrapped or opaque shell payloads** (`bash -c`, `eval`, a GraphQL body in a file) remain a
    text-reading limitation of the planned DYD-425 guard blocks; those blocks are not yet shipped.
  - **A workflow that registers late**, after every other check has finished green. A listed inert
    app registering a real suite before its runs exist is also ignored when its status is `queued`,
    `in_progress`, `pending`, `requested` or `waiting` and `latest_check_runs_count` is present as a
    valid integer zero; an unlisted app's unfinished suite refuses even with zero runs.
  - **The default-branch-switch race:** an admin switching the default branch to the feature branch
    during a run. The command re-reads `default_branch` after merging and warns when it changed;
    blocking agent default-branch changes is planned, delivery pending in DYD-425.
- The merge SHA is the candidate for the combined gates and the merge review. A conflicted merge
  stays the Merge code-writer's local merge.
- The command's stdout line is the merge evidence. Git and the PR timeline are the record; there is
  no local log.
- A repository whose CI does not run on PRs into `feature/*` gets every merge refused until it does.
- The stopgap `autoMode.allow` entry and the user-level `Bash(gh pr merge:*)` allow rule go only
  after the planned DYD-426 parity gate passes in LC; the user removes them. That gate remains pending.

## Related

- [CLI Commands](../../reference/dydo-commands.md#dydo-merge) - The command's exact contract
- [Configuration](../../reference/configuration.md#hook-configuration) - The allow rules init writes
- [Working-Tree Contract](../../guides/working-tree-contract.md) - Who integrates a Project ticket
- [DR 056 - Claude Guard in Committed Settings](./056-claude-guard-in-committed-settings.md)
