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
amended by the dydo admiral the same day and on 2026-11-04 (the target pin, see the Amendment).

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
   `dydo merge <pr> --match-head-commit <sha40> --match-base-commit <sha40> [--dry-run]`. A merge
   into `master`, `main` or the default branch is the user's click.
2. **Refusals in code, no override.** The command takes no repository, base-selection or override
   option; `--match-base-commit` only adds a refusal. The repository is the one the checkout's
   `origin` names, and the target is the PR's base. That base's tip, read from its branch ref and
   never from the PR's `base.sha`, must equal the pin; the pin reaches a write only as the merge's
   first parent and as the tip the base must still hold for the two-parent ref move. The base must match a fixed `feature/*` pattern and git's branch-name rule, and is
   refused when it is GitHub's default branch, read fresh, or the local `origin/HEAD` name. The PR
   head must equal the reviewed SHA, which is exactly what merges. A closed PR is refused unless its
   SHA is already in its feature base (reconcile-first, exit 0 `already integrated`, comparing no
   pin, since the run's own merge moved the tip). Draft and fork PRs are refused.
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
7. **Guard hard blocks on the other routes; shipped in
   [DYD-425](https://linear.app/bodnar-balazs/issue/DYD-425).** The guard refuses these paths for
   every caller, top-level or sub-agent: raw `gh pr merge` into any base, since it cannot read a PR's
   base offline; `gh api` on `pulls/<n>/merge`, `merge-async` or `merges`, and GraphQL
   `mergePullRequest`, `enablePullRequestAutoMerge` or `mergeBranch`; a `git push` that updates
   `master`, `main` or the branch the local `origin/HEAD` names; and default-branch changes
   (`gh repo edit --default-branch`, a `gh api` PATCH of the repository with `default_branch`). Each
   refusal names `dydo merge`. The exact reading and its residuals are in
   [Guard System](../../understand/guard-system.md#also-enforced).
8. **Claude/Codex parity is a requirement; its gate, delivered in
   [DYD-426](https://linear.app/bodnar-balazs/issue/DYD-426), passed in the dydo repository.** Both
   hosts must get the same privileges and the same allow or block outcome for the same command.
   `dydo init` and `--join` write `Bash(dydo merge:*)` and `PowerShell(dydo merge:*)` for Claude
   and a `prefix_rule` for `dydo` and `dydo.exe merge` for
   Codex. The gate runs the command matrix through a Codex session and a Claude session, in the main
   checkout and in a fresh T3 worktree; any mismatch is a blocker. It was planned with Codex in
   PowerShell; Balazs moved it to Linux, both hosts in bash
   ([DYD-572](https://linear.app/bodnar-balazs/issue/DYD-572)), and Windows PowerShell stays covered
   by the DYD-425 guard tests and the Windows CI job. Run r1 on 2026-11-05 matched on every row: the
   harness and the verbatim rows are in `DynaDocs.Tests/HostCanaries/merge-parity/`. Neither the
   allow-rule delivery nor the shipped blocks of decision 7 establish this parity on their own.
   Merging on one host only is not an outcome.

## Consequences

- Guards stop accidents, not rogue agents. Accepted residuals:
  - **A forged PR body.** Any agent with the gh token can edit it. Hardening would need a distinct
    reviewer identity, such as a GitHub App submitting an approving review.
  - **Wrapped or opaque shell payloads.** The shipped blocks read a wrapper's command string
    (`bash -c`, `eval`, `pwsh -Command`, `cmd /c`, `env -S`, `ssh`, `watch`) and refuse it for the
    route it holds. They remain a text-reading limitation: an `-EncodedCommand` or nesting past eight
    wrappers is not decoded, a GraphQL query or API body read from a file or stdin
    (`-F query=@file`, `--input`) is not read, and aliases, functions, scripts and unrecognised
    programs are not followed. A bare `git push`, or one naming only its remote, is followed to the
    current branch's upstream (`branch.<name>.merge`) and refused when that is the default branch; a
    destination only a `remote.<name>.push` refspec or `push.default` sets is not followed.
  - **A workflow that registers late**, after every other check has finished green. A listed inert
    app registering a real suite before its runs exist is also ignored when its status is `queued`,
    `in_progress`, `pending`, `requested` or `waiting` and `latest_check_runs_count` is present as a
    valid integer zero; an unlisted app's unfinished suite refuses even with zero runs.
  - **The default-branch-switch race:** an admin switching the default branch to the feature branch
    during a run. The command re-reads `default_branch` after merging and warns when it changed. The
    guard refuses an agent's `gh repo edit --default-branch` and `gh api` PATCH with `default_branch`,
    but it sees only agent tool calls: a switch outside them still passes. So does one by a path it
    does not read: a body read from a file or stdin, an alias, function, script or unrecognised
    program, anywhere; an `-EncodedCommand` or nesting past eight wrappers only outside a git tree,
    since inside one the PR-ready check refuses them unread.
  - **The `POST /merges` window.** GitHub cannot make that merge conditional on a tip. The command
    re-reads the ref just before the call and checks the merge's first parent after it, so a branch
    that moves inside that window still takes the merge: the command exits `2` naming both SHAs, and
    the merge review judges its actual parents. The two-parent path has no window: it moves the
    base atomically, only if it is still at the pin (GraphQL `updateRefs` with `beforeOid` the pin),
    without force. An unforced `PATCH git/refs` would not do, since it accepts any fast-forward, and
    a tip another merge moved into the SHA's history still fast-forwards to the merge commit.
- The merge SHA is the candidate for the combined gates and the merge review. A conflicted merge
  stays the Merge code-writer's local merge.
- The command's stdout line, which names the pinned tip, and the merge commit's `Target-Tip` trailer
  are the merge evidence. Git and the PR timeline are the record; there is no local log.
- A repository whose CI does not run on PRs into `feature/*` gets every merge refused until it does.
- The stopgap `autoMode.allow` entry and the user-level `Bash(gh pr merge:*)` allow rule go only
  after the DYD-426 parity gate passes in LC; the user removes them. The gate passed in the dydo
  repository on 2026-11-05; its LC run is not recorded here.

## Amendment - 2026-11-04: the target pin (DYD-523)

The DYD-521 merge receipt trusted the PR's `baseRefOid`, a tip the feature branch had left hours
before, so the reviewed target and the merged one differed
([DYD-523](https://linear.app/bodnar-balazs/issue/DYD-523)). Balazs delegated the call to the
admiral on 2026-11-04, who chose a required `--match-base-commit`: decisions 1 and 2 read as above,
and the POST-path window joins the accepted residuals. The pin keeps decision 2's guarantee because
it can only refuse; the target stays the PR's base, and the command's own output, not PR metadata,
records the tip it merged onto.

## Related

- [CLI Commands](../../reference/dydo-commands.md#dydo-merge) - The command's exact contract
- [Configuration](../../reference/configuration.md#hook-configuration) - The allow rules init writes
- [Working-Tree Contract](../../guides/working-tree-contract.md) - Who integrates a Project ticket
- [DR 056 - Claude Guard in Committed Settings](./056-claude-guard-in-committed-settings.md)
