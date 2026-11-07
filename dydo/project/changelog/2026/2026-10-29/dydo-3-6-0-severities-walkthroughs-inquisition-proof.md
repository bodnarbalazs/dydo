---
area: general
type: changelog
date: 2026-10-29
---

# dydo 3.6.0 - Review severities, safe feature merges, statecharts and guard fixes

<!-- Release candidate, refreshed for the 2026-11-07 landing against feature/dydo-3-6-0. Items
marked `pending: DYD-xxx` describe work still landing before the 17:30Z freeze; drop the marker
once it is merged, or drop the item if it moves to 3.7.0. Before tagging: bump the version pins
listed in .github/workflows/release.yml, point DynaDocs.csproj PackageReleaseNotes at this entry,
and add the "Align runtime and npm versions" line under Changed. -->

## Summary

A process and guard release built from a retro of ten days of dydo and LC agent sessions
(DYD-405).

- **Reviews and Projects.** A reviewer grades every finding `blocker`, `should` or `nit`, and only
  a blocker fails a review. A Project is cut into Linear Milestones, the first one a walking
  skeleton, and the user walks each Milestone as it lands. The inquisition is rebuilt around an area
  map and rounds that repeat until one finds no blocker and no should.
- **Safe feature merges.** Agents merge a reviewed, green PR into a `feature/*` branch through
  `dydo merge`, and the guard refuses the raw routes around it: `gh pr merge`, the merge APIs, a
  push to the default branch and a default-branch change, with the gaps listed under
  [Known guard gaps](#known-guard-gaps). A merge into the default branch stays the user's click.
- **Statecharts.** The user-invoked `statechart` skill and `dydo statechart` command check a bounded
  TLA+ model with TLC and draw the result, installing their locked prerequisites on first use.
- **Guard.** It stops refusing the ordinary commands the retro caught, reads a bare carriage
  return, typographic quotes and a caret in PowerShell as pwsh 7.6.9 does, bounds how long and how
  much memory reading one command can take, and refuses a Python program on stdin. A new default
  nudge warns before an agent installs a library or tool, so the install waits for the user's
  permission. Known gaps the 3.6.0 inquisition found are listed under Known guard gaps.
- **Officers.** Under T3, officers dispatch captains, crew and reviewers with `delegate_task`. A
  captain watches its PR through CI and review bots. The admiral keeps a lane ledger with a
  20-minute check-in, and `dydo survey` prints a read-only digest of a Project's lanes.

The .NET tool now needs the Microsoft.NETCore.App runtime 10.0.14 or later: the earlier pin let
NuGet resolve a vulnerable AOT compiler (NU1903, GHSA-p3jp-9v83-76w9). npm and the downloaded
NativeAOT binaries embed the patched runtime. Mutation testing is **UNAVAILABLE** for this release
(see Assurance below). Existing projects take the v3.6.0 `update-dydo` skill before running it, and
upgrade the .NET runtime before the tool; see Upgrade steps.

## Added

### Reviews, Milestones and the inquisition

- Review severities ([DR 058](../../../decisions/058-review-severities-blocker-should-nit.md),
  DYD-398). The `reviewer` skill labels every finding:
  - `blocker`: the intended case is wrong. A review with one or more blockers is a FAIL.
  - `should`: a real bug callers will hit, inside the ticket's intent.
  - `nit`: anything else, however serious it looks: another code path, a rare case, a small
    performance cost, extra compatibility, style.

  A PASS may list its shoulds and nits under `Deferred`. Every finding is answered (fixed,
  ticketed, recorded, or relabelled with evidence) and none is dropped. The ticket captain sends
  only blockers to a fix hop. Each should becomes its own ticket that natively blocks the Project's
  landing Merge Ticket; each nit is one line on the ticket record, listed again at the landing
  walkthrough. A merged two-reviewer block fails only if either reviewer found a blocker.
- The Linear Workspace Standard adds a `Severity` label group with `blocker` (`#E5484D`), `should`
  (`#FFB224`) and `nit` (`#8B8D98`). Only a ticket born from a finding carries one, and Priority
  never carries severity. The landing check is a query: no open `blocker` or `should` ticket in the
  Project unless the user deferred it (DYD-409, DYD-418, DYD-419, DYD-420).
- Milestone walkthroughs ([DR 060](../../../decisions/060-milestone-walkthroughs-and-the-walking-skeleton.md),
  DYD-399). The map holder places a few Linear Milestones, the walking skeleton first and the
  landing last, and every delivery ticket belongs to one. When a Milestone's last ticket merges, the
  admiral offers its Walkthrough Ticket; the user walks it with `walkthrough` or skips it. Only the
  skeleton's Walkthrough Ticket blocks the next Milestone's tickets. The `walkthrough` skill walks
  the product at a Milestone's SHA, newest changes first, and lists each deferred should and, at the
  landing, every unaddressed nit.
- Rebuilt inquisition ([DR 059](../../../decisions/059-inquisition-rebuilt-area-map-three-phases-rounds-until-clean.md),
  DYD-400, DYD-447). Each round the captain draws an area map from the landing diff and runs one
  `inquisitor` per area per phase: Read and Drive change nothing, Break commits proof tests on
  `proof/<KEY>-<area>-<n>`. Findings go to `inquisition-register.md` on the audit branch; blockers
  and shoulds become Bug tickets, the round's nits one nit ticket. A proven blocker or should can get
  its fix prepared mid-round on the Bug's own branch. The next round waits until every blocker and
  should fix is `Done`, and the Project lands only after a round with neither. The inquisitor gains
  `read.md`, `break.md`, `drive.md` and `context-size.md`; the last gives a Node script that prints
  the agent's own context size on Claude Code and Codex.
- `maintain-verification-skill` gains a `## Proof` section, the one place dydo says what proof is:
  evidence observed in this session, such as command output, a reproduction or a screenshot. The
  crew member hands up a proof recipe, the captain reruns it before offering the PR, and that rerun
  is the proof of record. `code-writer`, `reviewer`, `ticket-captain` and every new `verify-<app>`
  skill point there (DYD-401, DYD-411). The skill also sorts a verification gap an inquisition found
  as a harness gap, doc drift or product gap (DYD-400).

### Officers and planning

- Dispatch under T3 follows the child's lifetime
  ([DR 057](../../../decisions/057-t3-dispatch-follows-child-lifetime.md), DYD-397). A native
  subagent only when the dispatcher waits for it inside its own turn; `delegate_task` for every
  other child that reports back, and for any agent of another vendor. A native child that hits T3's
  `parent_not_active` refusal hands the dispatch up.
- The ticket captain watches its PR instead of reading `gh pr checks` once (DYD-402). It handles
  existing checks and comments first, then watches with `watch_pull_request` in T3, never a sleep
  loop. A delegated captain returns `PR open <key>, checks pending: <url>, head <SHA>` and ends its
  turn; the admiral watches that PR and wakes the captain on news (DYD-410, DYD-412). Adapted from
  tstack's `babysit-pr` (MIT).
- The admiral keeps a `## Lane ledger` in the Project description, one row per lane with its owner,
  state and next action, and runs a check-in every 20 minutes through T3 `schedule_task`: a cheap
  surveyor reads the board and returns actions (DYD-520).
- The admiral reconciles the board on every user wake, captain return or approved check, before it
  yields: each pickable item is dispatched, or has a specific blocker, owner and clearance route
  (DYD-477).
- The admiral's `scaffold-project` recipe: every scaffolded Project gets a working spec under
  `dydo/reference/working-specs/`, committed first on its feature branch, and the first ticket is
  wired as a native Linear blocker of every other first lane before any commission (DYD-570,
  DYD-724). `to-project`, `to-ticket` and `wayfinder` link one reachable shared anchor, a reviewed
  plan or feature spec, before tickets rely on it, and keep live holds in Linear rather than in the
  spec (DYD-443, DYD-444, DYD-445).
- Overnight mode gains the chief of staff's `muster`, `night-survey` and `morning-brief`
  resources, a survey-only night watch (DYD-427), and `overnight-mode`'s `transition-preflight`.
  Before `dydo mode set overnight on` or `off` the chief of staff prepares the warning and
  same-command retry, waits for the setter's result, and only then runs `dydo mode show` in a
  separate tool call (DYD-433, DYD-673). An executing `dydo mode set` now warns once on every host
  and the same command's retry passes.
- The `catch-up` skill, renamed from `ketchup` (DYD-449), reports a project as an inverted pyramid
  with a closed set of item cues, and scopes itself to the project the agent holds (DYD-439,
  DYD-440, DYD-442, DYD-581).
- `verify-dydo`, an executable verification skill that drives the DynaDocs CLI and guard in owned
  disposable projects, with a feature map and real officer-transition evidence for overnight on and
  off (DYD-430, DYD-435).

### `dydo merge`

- `dydo merge <pr> --match-head-commit <sha40> --match-base-commit <sha40> [--dry-run]`
  ([DR 061](../../../decisions/061-agents-merge-only-into-feature-branches.md),
  [CLI Commands](../../../../reference/dydo-commands.md#dydo-merge), DYD-417, DYD-424, DYD-523). It
  merges exactly the reviewed SHA into the PR's base with a merge commit, and refuses, with no
  override flag, unless:
  - the PR is in the checkout's `origin` repository, open, not a draft and not from a fork;
  - its base is a `feature/*` branch, not the default branch nor the local `origin/HEAD` name;
  - its head is the pinned head SHA, and the base branch's tip, read from the branch ref, is the
    pinned base SHA;
  - the PR body's `## Independent review` holds a `code`, `docs` or `merge` PASS for that SHA and
    no FAIL;
  - every check on the SHA has finished green and every required check is present.

  Refusals exit 1 with one `refused:` line and write nothing. After a good merge the report reads
  `PR marked merged: yes`, `pending` or `unverified`, never `no` while GitHub catches up (DYD-535).
  Details are in [Safe merge command](./dydo-3-6-0-safe-merge-command.md) and
  [Merge target pin](../2026-11-04/dyd-523-merge-target-pin.md).
- `dydo init` and `--join` write `Bash(dydo merge:*)` and `PowerShell(dydo merge:*)` into
  `.claude/settings.local.json` and a `prefix_rule` for `dydo` and `dydo.exe merge` into
  `.codex/rules/dydo.rules`. The guard's sub-agent lane allows `dydo merge`.
- `dydo merge` and `dydo pr-ready --body` read the PASS line with one grammar:
  `PASS - rubric/type: code; candidate <SHA>; base <SHA>`, with optional colons after `candidate`
  and `base` and an optional base. `pr-ready` now refuses a prose-only PASS line with the expected
  form (DYD-618, DYD-624).

### Statechart

- The user-invoked `statechart` skill, adopted from Cursor's pstack (MIT)
  ([DR 062](../../../decisions/062-statechart-invocation-manages-prerequisites.md), DYD-415). It runs
  TLC on a bounded TLA+ model and writes a Mermaid chart, shortest bug paths, a report and the raw
  checker output; Pele renders charts to SVG. See [DYD-415](./dyd-415-statechart.md).
- `dydo statechart SPEC.tla` checks a model on Windows and Linux. When Node, Java, TLC or Pele is
  missing it saves the request and exits `3` with a locked plan; `dydo statechart resume --request`
  installs the locked versions through the host's normal approval and continues the same chart.
  `dydo statechart install-skill --root <project>` installs the matching skill, and
  `dydo statechart doctor` checks readiness on Windows without downloading anything.

### Other commands

- `dydo survey <project>` prints a read-only digest of one Linear Project's lanes, read once with
  the user's `LINEAR_API_KEY`: it writes nothing and never polls
  ([DR 063](../../../decisions/063-dydo-survey-read-only-lane-digest.md), DYD-530).
- `dydo validate` warns when a project records the Claude integration but its committed
  `.claude/settings.json` does not run the dydo guard
  ([DYD-501](../2026-11-05/dyd-501-claude-guard-wiring.md)).

## Changed

### Process skills

- `update-dydo` (DYD-404, DYD-409):
  - A file's base can be proved by the record of an earlier update: the commit that set
    `dydoVersion` and the commit message or PR text listing the file as merged or kept. A re-run no
    longer treats files the last update merged as having no base.
  - A deletion an Upgrade step of a later release settles is followed without asking and listed.
  - `dydo init <host> --join` and `node setup-skills.mjs` run only in the update's own checkout;
    every other checkout is listed with what to run there once its branch carries the update.
  - The final report hands the user a commit message and PR text listing every file merged, kept,
    skipped or left to them. That text is the record the next update reads.
  - Through the session's Linear connection, it compares the team's label groups, labels, ticket
    statuses and templates, and the Project statuses the user reads back, with the standard. It
    creates a missing label group or label only with the user's OK, and lists missing or misordered
    ticket statuses, Project statuses and templates for the user to set up. Templates the
    connection cannot read are listed for the user to check.
- The `wizard` skill gives each stage an outcome the agent can check without a secret. Only where
  the user would otherwise paste output back, the script may log it, never a secret, to a temp file
  the agent reads and deletes (DYD-408).
- The captain cleans the worktrees it or its crew made, with `dydo clean --dry-run <worktree>`
  then `dydo clean <worktree>`, and leaves its own thread's worktree to T3. `dydo clean` now says
  `skipped <path>: T3 owns worktrees under <root>; leave them to T3` (DYD-407).
- The `pr` skill and the ticket captain write a PR body to the ignored
  `dydo/agents/workspace/pr-body-<KEY>.md`, run `dydo pr-ready --body <file>` as its own command,
  and open the PR with a lone `gh pr create --body-file <file>` (DYD-746).
- The testing strategy is feature-first, replacing the full changed stack on every hop: each
  committed hop runs the affected features' Light tests, and the final candidate runs own-feature
  Heavy and ticket-scoped coverage once. A portable copy ships as
  `Scaffold/dydo/guides/testing-strategy.md` (DYD-413). Reviewers check HCRAP against coverage evidence at the declared gate (DYD-441).
- `inquisitor` severities are `blocker`, `should` and `nit`, replacing `high`, `medium` and `low`.
  The Walkthrough template has `## Checkpoint` and `## Verdict` in place of `## What landed`; the
  Inquisition template has `## Scope`, `## Area map`, `## Findings` and `## Record`. The glossary
  defines Severity, Milestone, walking skeleton, Round, Area, Area map, Phase, Findings register,
  Sighting, Verification gap, Patch lane, Nit ticket and Group Sub-ticket (DYD-398, DYD-399,
  DYD-400).
- `what-would-balazs-do` counts the user's walkthrough verdicts, findings and skips as his own
  words (DYD-399). The code-writer and admiral advance completed work and test a review's strongest
  counterexample against a fix (DYD-438).

### Guard

- A new default `warn` nudge, the install nudge, fires when an agent installs or adds a library or
  tool: `pnpm`, `npm`, `yarn` or `bun` `add` or `install <package>`, `npx -y`, `uv add`, `pip
  install`, `pipx`, `dotnet add` or `dotnet tool install`, and `apt`, `brew`, `cargo` or `gem
  install`, among others. It also fires behind `sudo`, `env` or leading assignments, through a path
  to `pip` or `uv`, and after options such as `pnpm -C <dir>`. Restoring locked dependencies (`pnpm install --frozen-lockfile`, `npm ci`,
  `uv sync --frozen`, `dotnet restore`, a bare `pnpm install` or `npm install`) never triggers it.
  The agent retries the same command only with the user's permission, given in the conversation or
  in the approved plan or ticket that names the install; otherwise it stops and asks. It is
  best-effort, not a lock. `dydo init` ships it, and `dydo adopt` adds it once to an existing project
  at 3.6.0 (DYD-499, [Guard System](../../../../understand/guard-system.md#layer-3-nudges)).
- A command nudge no longer fires on a comment or a literal heredoc or here-string body, or on four
  recorded quoted search patterns (`rg -n '...'`, `git grep -n -E '...'`, `grep -iE "..."` and a
  `Where-Object` `-match` operand on `$_.CommandLine`). Every other unquoted or unknown quoted match
  still counts (DYD-406, DYD-723).
- A command warn's retry marker is keyed by the command, so only the same command, retried, passes;
  a different command matching the same nudge is warned on its own (DYD-499).
- The review-block nudge fires behind leading assignments and a bare `env` with its common options
  (DYD-736), and reads a lone `gh pr create`'s `--body-file` or `-F` file as that call's body, so a
  reviewed body file no longer warns (DYD-639). See
  [Guard System](../../../../understand/guard-system.md#layer-3-nudges).
- The guard's shared shell reader reads three characters in PowerShell as pwsh 7.6.9 does: a bare
  carriage return, typographic quotes and a caret. The bypass checks, the literal-body relief, the
  dangerous-pattern readings, the nudges, the dydo-command rules, the PR-ready check and the
  merge-route blocks read a line through it
  ([DYD-641](../2026-11-05/dyd-641-shellreader-pwsh.md)). File-operation extraction still ignores
  typographic quotes (DYD-661, under Moved to 3.7.0). A backtick before a CRLF continues the line
  only between words, as pwsh does (DYD-665).
- `git push --force-with-lease origin main` is now refused as a push to the default branch
  ([DYD-425](../2026-11-04/dyd-425-guard-merge-blocks.md)).

### Assurance and testing

These change dydo's own repository and CI, not what a project runs.

- **Mutation testing is UNAVAILABLE for 3.6.0.** The changed-code mutation gate has no installed
  mechanism on any of the five stacks and returns exit 2, which is not a pass (DYD-773). The landing
  records it as unavailable under the ratified disposition DYD-774; it is not reported as passed.
- The guard's repeated numerical performance proofs are now an opt-in suite. No ordinary
  `dotnet test`, filtered run, CI job or required check runs them; run them with
  `release_proof.py run` or the `guard-performance-proofs.yml` workflow dispatch when a change to
  how the guard reads commands calls for it (DYD-859,
  [Testing Strategy](../../../../guides/testing-strategy.md#dormant-guard-performance-proofs)).
- Toolchain pins: Node 26, .NET SDK 10.0.303 and Python 3.14
  ([DYD-504](../2026-11-04/dyd-504-toolchain.md)). The coverage tools lock with pnpm, as the viewer
  does ([DYD-553](../2026-11-05/dyd-553-pnpm-everywhere.md), DYD-643). The static-gate Python
  environment moved to the repo-root `.static-gates/python`
  ([DYD-539](../2026-11-04/dyd-539-static-gate-env.md)).
- The testing facade caps the memory of every operation that runs children: a systemd scope that
  joins the caller's slice on Linux, a Job object on Windows (default 8G, `DYDO_MEMORY_MAX` may
  lower it). A run that hits the cap fails with a finding that names the scope limit
  ([DYD-722](../2026-11-05/dyd-722-shared-host-memory-cap.md),
  [DYD-729](../2026-11-06/dyd-729-slice-aware-memory-cap.md),
  [DYD-730](../2026-11-06/dyd-730-windows-job-memory-cap.md),
  [DYD-800](../2026-11-07/dyd-800-scope-cap-reason.md)).
- CI runs on pushes to `feature/**`, every job checks out the run's own commit, and coverage unions
  take each leg's latest attempt after a rerun ([DYD-698](../2026-11-05/dyd-698-feature-push-ci.md),
  [DYD-717](../2026-11-06/dyd-717-merge-ref-ci.md),
  [DYD-694](../2026-11-05/dyd-694-union-rerun-artifact.md)).
- Hosted Windows CI runs the guard's analyzer and hook tests, native Windows Python coverage at full
  thresholds, the statechart Windows tests and every declared Windows .NET test
  ([DYD-681](../2026-11-05/dyd-681-windows-guard-ci.md), DYD-518, DYD-622, DYD-718,
  [DYD-805](../2026-11-06/dyd-805-windows-guard-expansion.md),
  [DYD-806](../2026-11-06/dyd-806-windows-directory-link-cleanup.md)). The new
  `statechart-config-oracle` job runs the 4000-config TLC comparison
  ([DYD-784](../2026-11-06/dyd-784-config-fuzz-oracle.md)).
- Statechart measurement, coverage and tests (DYD-428, DYD-526, DYD-529, DYD-548, DYD-549,
  DYD-558, DYD-656, DYD-726, DYD-740, DYD-780), per-ticket coverage scoped to the Light gate's
  feature selection (DYD-522, DYD-712), and runner, reaping and interrupt fixes in the coverage
  campaign (DYD-507, DYD-514, [DYD-516](../2026-11-05/dyd-516-missing-producer-campaign-isolation.md),
  DYD-554, DYD-593, DYD-597, [DYD-605](../2026-11-05/dyd-605-escalation-cleanup-deadline.md),
  DYD-638, DYD-645, DYD-654, DYD-657, DYD-658, DYD-664, DYD-693, DYD-704, DYD-706, DYD-728,
  DYD-788, DYD-811). The test runner refuses a filter placed after the runsettings
  separator (DYD-258), and the host canaries pre-warm the Claude guard hook
  ([DYD-545](../2026-11-05/dyd-545-canary-fingerprint.md)).
- The viewer's refresh tests wait for the `refresh-removed` marker (DYD-378).

## Fixed

### Guard

- The retro's false refusals (GS-01 to GS-06, DYD-406):
  - Writing literal text passes: `cat > notes.md <<'EOF'` with `$` or backticks in the body, a
    PowerShell `@'...'@` here-string, and `cat > run.sh <<'EOF' ... && bash run.sh`. Expanding
    bodies (`<<EOF`, `@"..."@`) and dynamic destinations are still refused.
  - The password-store rule matches `pass` and, newly, `gopass` only as a command, so a Python
    `pass` statement or a PR body saying "pass" goes through.
  - PowerShell `$p = Get-Content notes.md` (and `gc`, `cat`, `type`, `Select-String`) reads as a
    read of that path; the off-limits check still applies.
  - `gh pr create --help` is no longer taken for a PR call by the PR-ready check.
- A single `sed`, `printf` or `echo` command that writes single-quoted data is no longer refused as
  a bypass: ``sed -i 's/`x`/y/' f`` and `printf '%s' 'first line<newline>$HOME is literal' > notes.md`
  write their literal destinations, which are still path-checked. A line that chains `git` stays
  refused (DYD-422, DYD-659; see
  [Single-quoted literal data](../2026-11-05/dyd-422-guard-single-quoted-literals.md)).
- The PR-ready check no longer refuses a command with no gh word just because it could not finish
  reading it, such as `arch="$(case "$(uname -m)" in ...)"`
  ([DYD-515](../2026-11-05/dyd-515-pr-ready-without-gh.md)).
- A long benign command is no longer refused as "Command too complex to verify safely" because one
  of the 17 command-shaped dangerous-pattern rules ran past its 250 ms budget; those rules now match
  in time linear in the command ([DYD-610](../2026-11-05/dyd-610-guard-regex-budget.md)). Two other
  rules can still run past it on long benign input and refuse it (DYD-679, under Moved to 3.7.0).
  Reading one command is now bounded in
  time and memory: command text over 1,048,576 characters is always refused, and an unclosed `$(`
  no longer blows up
  ([DYD-723](../2026-11-05/dyd-723-unclosed-substitution-bound.md)).
- `dydo guard --command "@'...'@"` reads a leading `@` as text and gives it a verdict, never a
  response file (DYD-675).
- From a bash line, `git -C C:\...\wt push` into a worktree on `main` is refused on Windows, as
  every other host form already was
  ([DYD-739](../2026-11-06/dyd-739-windows-git-c-push.md)).
- A file-descriptor redirection such as `1>` or `2>` now extracts a write, so writing a protected
  file that way is refused instead of only warned
  ([DYD-640](../2026-11-05/dyd-640-fd-redirect-write.md)).
- A here-string whose word is a backtick substitution at the end of a command no longer crashes
  the 3.6.0 shell reader. Before the fix, the crash reached the guard's catch-all, which allowed
  the call, so a protected delete or a push to the default branch inside that substitution ran.
  Now the reader reads the substitution, the guard judges the command inside it, and an unclosed
  backtick fails the reading instead of throwing (DYD-881).
- <!-- pending: DYD-887 --> From a ticket branch, one line that switches to the default branch,
  merges and runs a bare `git push` is refused. Before, the guard judged that push by the branch
  checked out before the switch, so it allowed a push to the default branch.

### Guard refusals that were missing

- A Python program on stdin is refused like `python -c`: `python -`, `py -`, or `python` with no
  script while a heredoc, here-string, pipe or `<` feeds it (DYD-406). The same holds when a
  runner's `run` subcommand starts that Python: `uv run python - <<'EOF'`, `poetry run python3 -`
  and `printf ... | uv run python` are refused, while `uv run python script.py` passes
  ([DYD-509](../2026-11-05/dyd-509-python-stdin-run.md)).
- The guard hard-blocks raw merges for every caller, under Claude's Bash and PowerShell tools and
  Codex's argv arrays: `gh pr merge` in any form, `gh api` on the merge endpoints, the GraphQL merge
  mutations, a `git push` that updates `master`, `main` or the `origin/HEAD` branch, and a
  default-branch change. Each refusal names `dydo merge`
  ([DYD-425](../2026-11-04/dyd-425-guard-merge-blocks.md), DYD-576). A Codex session and a Claude
  session got the same allow or block outcome for the merge command matrix (DYD-426). Two push
  routes to `main` still pass; see [Known guard gaps](#known-guard-gaps).

### CLI

- `dydo init claude` and `--join` no longer warn that `CLAUDE.md` hides `AGENTS.md` when its first
  line is `@AGENTS.md`. `dydo init codex` leaves `.codex/rules/dydo.rules` alone when only its line
  endings differ, so a CRLF checkout no longer shows it modified (DYD-403).
- A `dydo mode set` whose mode store cannot be written prints one stderr line naming the store path
  and the cause and exits 2, leaving the stored state as it was, instead of a .NET stack trace
  (DYD-644).
- Packing the tool from source fails with viewer build instructions when the map viewer bundle is
  missing, so a source build no longer ships a CLI without the viewer (DYD-687; see
  [Install from source](./dydo-3-6-0-safe-merge-command.md#install-from-source)).
- A Release build in a worktree whose branch ref is only packed resolves its commit for SourceLink
  (`Microsoft.SourceLink.GitHub` 10.0.403, DYD-603).

### Statechart

- The POSIX statechart socket stays within the `AF_UNIX` path limit when `TMPDIR` is long
  (DYD-574). When the socket's fallback directory cannot be created, the error names the directory
  and the remedy, removing it or setting a shorter `TMPDIR`, instead of a bare `Win32Exception`
  (DYD-601).
- The POSIX group drain reads a task the kernel is releasing as no member instead of aborting
  ([DYD-678](../2026-11-05/dyd-678-posix-drain-releasing-task.md)). The skill's `.gitignore` covers
  the Java build's `__pycache__`, so a coverage run leaves the tree clean
  ([DYD-560](../2026-11-05/dyd-560-statechart-java-pycache-ignore.md)).
- Two damaged installs now recover. A managed Node or Java whose executable lost its execute bit
  counts as damaged, so the run returns the saved, approval-required repair plan instead of failing
  with `Permission denied` every time (DYD-510). An asset extraction whose receipt names this
  payload but whose content fails verification is extracted again, offline, from the bytes embedded
  in dydo; a directory without this payload's receipt is still refused untouched, naming its path
  and the remedy (DYD-511). The first is a dependency repair that waits for approval; the second
  re-extracts dydo's own assets with no download.

### Viewer

- Refresh, and the `R` key, stay busy until the canvas has drawn the new map. Before, they came
  free while the old map was still drawn, and a second refresh then compared the new map with
  itself and reported no changes (DYD-575).
- Expanding or collapsing a plate centres it on the layout the toggle produces, so it no longer
  ends up off screen (DYD-561).

## Removed

- The inquisition's lenses (correctness, coverage, security, dead code, doc drift, seams),
  read-only inquisitors, the separate record-delivery Feature and its `released <key>: record
  delivery` loop (DYD-400).
- The `ketchup` skill folder; the skill is `catch-up` now (DYD-449).

## Moved to 3.7.0

These were in 3.6.0's scope and did not land by the freeze. They continue in the 3.7.0 Project.

- Guard reading, analyzer and PowerShell:
  - A quote opened inside a `#` comment is taken as a real quote, hiding the lines between
    (DYD-676).
  - A line that writes git config or hooks and then runs git is allowed when the payload needs no
    `$` (DYD-642).
  - Single-quoted literal relief on lines that chain git (DYD-628).
  - File-operation extraction ignores PowerShell typographic quotes (DYD-661).
  - Here-string relief takes a mid-word `@'` as a here-string opener (DYD-662).
  - The command string `iex` or `Invoke-Expression` runs isn't checked for file operations
    (DYD-666).
  - Three older PowerShell misreads: a block comment before `| iex`, `--%` stop-parsing, and a
    backslash before a quote (DYD-680).
- Guard tests and timeouts:
  - Two guard patterns, `DdKnownDiskWrite` and `PowerShellRecursiveDelete`, still time out on long
    benign inputs; they fail closed (DYD-679).
  - Test gaps around `@`-text guard commands: verdict pinning and a Codex-shaped payload (DYD-696,
    DYD-697).
  - The hosted Windows guard job doesn't run `ShellReaderTests` (DYD-732).
- Other:
  - The statechart Java build helper tests don't assert its reproducibility flags and identity
    guards (DYD-569).
  - Sharpen the Python platform coverage tests against surviving mutants (DYD-542).
  - An optional confirmation popup for overnight mode transitions (DYD-543).

## Known guard gaps

The 3.6.0 inquisition (DYD-431) found these gaps in what the guard reads. 3.6.0 ships with them;
each has a fix planned for 3.7.0, and none of those fixes is in this release. The guard stops
accidents, not a determined agent. Balazs marked DYD-865, DYD-879 and DYD-882 as must-fix for
3.7.0.

- A git global option placed before `push`, or a combined short flag, can hide a force push or a
  remote branch deletion (DYD-865).
- Shell file operations are resolved against the guard's own directory instead of the hook's
  working directory (Claude's `cwd`, Codex's `workdir`), so a relative delete of a protected file
  can be allowed (DYD-882).
- Gaps in 3.6.0's new merge-route protection, which earlier releases did not have at all, so
  neither is a regression:
  - A push run after a `cd` that comes mid-line or inside a subshell, into a worktree on `main`, is
    allowed and updates `main` (DYD-879).
  - A push whose destination names `main` in a shortened ref form updates `main` without a refusal
    (DYD-880).
- Protected-file deletes and writes the guard does not see:
  - A delete inside compound shell syntax: conditionals, loops, subshells, brace groups, a
    background `&` or PowerShell blocks (DYD-866).
  - A delete behind a wrapper command (`env`, `command`, `timeout`, `nohup`, `xargs`) or through an
    absolute path to `rm` (DYD-867).
  - In-place `sed` over several files checks only the last file (DYD-868).
  - `find` with a delete action is read as a read (DYD-869).
  - A write through a PowerShell .NET file API call (DYD-870).
  - `dd`'s output-file operand is not read as a path, so a protected file can be overwritten
    (DYD-871).
  - In PowerShell: renaming a protected file, a block comment before a delete, and `.\`-prefixed or
    backslash paths on Linux pwsh (DYD-883).
  - A delete inside an escaped backtick substitution nested in another backtick substitution
    (DYD-888).

## Upgrade steps

1. Before updating the .NET tool, make sure the machine has the Microsoft.NETCore.App runtime
   10.0.14 or later in a compatible 10.x version (`dotnet --list-runtimes`). A host with only
   10.0.0 to 10.0.13 cannot launch the 3.6.0 tool; the SDK version alone does not tell you. npm and
   the downloaded NativeAOT binaries embed the patched runtime and need nothing here
   ([Distribution runtime requirements](../../../../reference/coverage-tools.md#distribution-runtime-requirements)).
2. Install the 3.6.0 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
3. Take `skills/productivity/update-dydo/SKILL.md` from the v3.6.0 tag before running it. The
   3.5.1 skill runs `--join` in every worktree, editing committed hooks on branches you do not own,
   and finds no base for files the previous update merged. This replaces 3.5.1's step 2.
4. A project whose last update left no record listing the files it merged gets more files with an
   unknown base on this run, and reconciles them by hand. Keep the commit message and PR text the
   skill hands you at the end: the next update reads them.
5. Merge the release's `skills/` and `Scaffold/dydo/` changes into the project, keeping its edits,
   then run `node setup-skills.mjs` from its root.
   - Renamed skill: `ketchup` is now `catch-up`. Move `skills/productivity/ketchup/` to
     `skills/productivity/catch-up/` and take the release's text; a project's own mentions of
     `/ketchup` become `/catch-up`.
   - New skills: `statechart` (user-invoked) and `verify-dydo`. New resources: the inquisitor's
     `read.md`, `break.md`, `drive.md` and `context-size.md`; the admiral's `check-in.md` and
     `scaffold-project.md`; the chief of staff's `muster.md`, `night-survey.md` and
     `morning-brief.md`; `overnight-mode`'s `transition-preflight.md`; `catch-up`'s `example.md`.
   - Scaffold files: `guides/testing-strategy.md` (new), `guides/working-tree-contract.md`,
     `project/_project.md`, `reference/about-dynadocs.md`, `reference/dydo-commands.md`,
     `reference/dydo-glossary.md`, `reference/feature-spec-convention.md`,
     `reference/linear-workspace-standard.md`, and `files-off-limits.md` (step 6).
   - Copy `THIRD-PARTY-NOTICES.md` along with `skills/`: it now credits tstack (MIT) for the adapted
     PR-watch and proof text, and its Cursor pstack (MIT) section adds `statechart`.
6. `files-off-limits.md` changes only its prose: the Protected Patterns paragraph now says the
   guard refuses inline interpreter code and a Python program on stdin, also when a runner's `run`
   subcommand starts it. The guard reads the pattern blocks, not this paragraph, so the update is
   optional. The file is protected: `update-dydo` shows the diff and a person applies it. A new
   `dydo init` writes the new text. In the dydo repository itself, the maintainer applies the same
   change to `dydo/files-off-limits.md`.
7. Run `dydo init <host> --join` for each wired host, then `node setup-skills.mjs`, in the update's
   own checkout only. This overrides 3.5.1's step 4, which ran them in every checkout. In each other
   checkout, run them once its branch carries the update, when
   `git -C <checkout> show HEAD:dydo.json` records `dydoVersion` 3.6.0 or later. For Claude Code,
   `--join` adds the two `dydo merge` allow entries to `.claude/settings.local.json`, which stays
   uncommitted, so each checkout needs its own run. For Codex it rewrites `.codex/rules/dydo.rules`:
   commit it, restart Codex and re-trust the project, since Codex loads project rules only from a
   trusted `.codex/` layer.
8. Hooks: no 3.6.0 change alters the hook entries `dydo init` writes. After step 7,
   `git diff --stat -- .claude/settings.json .codex/hooks.json` should list nothing; if it lists
   `.codex/hooks.json`, commit it and re-trust the hooks in Codex. A CRLF `.codex/rules/dydo.rules`
   no longer shows modified after `--join` when only its line endings differ.
9. Check that the project's CI runs on PRs into `feature/*` branches. Without it `dydo merge`
   refuses every merge with `no checks reported`.
10. A machine carrying a stopgap for agent merges, such as a free-text `autoMode.allow` merge entry
    or a user-level `Bash(gh pr merge:*)` allow rule, keeps it until the user has seen `dydo merge`
    and the guard's merge blocks give Claude and Codex the same outcome in that project
    ([DR 061](../../../decisions/061-agents-merge-only-into-feature-branches.md) decision 8). Then
    the user removes it; agents cannot edit their own permissions.
11. Linear: the team needs a `Severity` label group with `blocker` (`#E5484D`), `should`
    (`#FFB224`) and `nit` (`#8B8D98`). With a Linear connection in the session, `update-dydo`
    compares the team with the Linear Workspace Standard and lists each difference. It creates a
    missing label group or label only with your OK. It lists missing or misordered ticket statuses,
    Project statuses and templates for you to set up in Linear, and lists the standard's templates
    for you to check when the connection cannot read them. Without a connection, create the group by
    hand in the team's label settings.
12. From the next Project you chart: place Linear Milestones, the walking skeleton first and the
    landing last, with one Walkthrough Ticket each, and wire the skeleton's as a native blocker of
    the next Milestone's tickets. Write Walkthrough tickets with `## Checkpoint` and `## Verdict`.
    In a Project already in flight, make its landing Merge Ticket natively blocked by every open
    should ticket. Neither DR 059 nor DR 060 asks for any other migration of in-flight Projects or
    old inquisition records.
13. Before the next inquisition: a project with a runnable surface needs a verification skill
    (`create-verification-skill`), and the context-size check needs Node and crew dispatched as
    their own T3 sessions; without them the inquisitor records its context as `unavailable`. Under
    T3, officers dispatch through `delegate_task`, so the T3 MCP tools must be available to them. An
    existing generated `verify-<app>` skill does not gain the pointer to
    `maintain-verification-skill`'s Proof; regenerate it or add the line by hand.
14. Run `dydo adopt 3.6.0` last, once `dydo check` exits 0 and the update is proven. It adds the
    library-install warn nudge to `dydo.json` once, skipping a project whose `dydo.json` already
    holds the same pattern.

### Merge normalisations

1. `ketchup` becomes `catch-up`, and `Ketchup` becomes `Catch-up` (DYD-449). Matching rule: the
   whole word `ketchup` or `Ketchup` where it names the dydo skill or its folder. Scope:
   `Scaffold/dydo/reference/dydo-glossary.md`, `skills/productivity/README.md`, the `name:` line
   and the `# Ketchup` H1 of `skills/productivity/catch-up/SKILL.md`, and the path column of
   `THIRD-PARTY-NOTICES.md` and `npm/THIRD-PARTY-NOTICES.md`. Exceptions: the upstream skill name
   `ketchup` in the notices' first column and the `pstack/ketchup` attribution comment, which name
   Cursor's skill. The folder move itself is carried by the merge.

## Files Changed

By area; each linked entry above lists its own files.

- CLI: `Commands/` (`MergeCommand.cs`, `SurveyCommand.cs`, `StatechartCommand.cs`, `InitCommand.cs`,
  `ModeCommand.cs`, `GuardCommand.cs`, `AdoptCommand.cs`), `Services/Merge/`, `Services/Survey/`,
  `Services/Statechart/`, `Resources/Statechart/`, guard services (`BashCommandAnalyzer.cs`,
  `ShellReader.cs`, `PrCallScanner.cs`, `PrCallScanner.MergeRoutes.cs`, `MergeRoute.cs`,
  `CommandPosition.cs`, `ConfigFactory.cs`, `ClaudeGuardWiring.cs`, `ValidationService.cs`),
  `Services/ModeState.cs`, `Services/WorktreeCleaner.cs`, `Utils/PathUtils.cs`, `Program.cs`,
  `DynaDocs.csproj`, `ViewerPackaging.targets`, `.codex/rules/dydo.rules`.
- Viewer: `viewer/src/app/App.tsx`, `viewer/src/map/MapCanvas.tsx`.
- Skills: `skills/engineering/` (new `statechart` and `verify-dydo`; `maintain-verification-skill`,
  `create-verification-skill`, `pr`, `wizard`), `skills/productivity/` (`catch-up` replacing
  `ketchup`, `overnight-mode`, `to-project`, `to-ticket`, `update-dydo`, `walkthrough`,
  `wayfinder`, `what-would-balazs-do`), `skills/roles/` (`code-writer`, `docs-writer`,
  `inquisitor`, `reviewer`, `admiral`, `chief-of-staff`, `ticket-captain` and their resources).
- Docs: `Scaffold/dydo/` (step 5), `dydo/project/decisions/` (new 057 to 063; banners and
  amendments on 031, 041, 044, 045, 047, 048, 052 and 053), `dydo/guides/`, `dydo/reference/`,
  `dydo/understand/`, `THIRD-PARTY-NOTICES.md`, `npm/THIRD-PARTY-NOTICES.md`, this entry and its
  changelog hub.
- Assurance: `DynaDocs.Tests/`, `.github/workflows/` (`ci.yml`, `release.yml`,
  `guard-performance-proofs.yml` and the provisioning scripts), the viewer's tests and e2e specs.

<!-- pending: version bump --> `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`.

## Related

- [dydo 3.6.0 - Safe merge command](./dydo-3-6-0-safe-merge-command.md)
- [DYD-415: checked statecharts](./dyd-415-statechart.md)
- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [DYD-504 toolchain modernization](../2026-11-04/dyd-504-toolchain.md)
- [DYD-413 - Canonical feature-first testing workflow](../2026-10-30/dyd-413-canonical-testing-workflow.md)
- [dydo 3.5.1 - Downstream update fixes](../2026-10-27/dydo-3-5-1-downstream-update-fixes.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
