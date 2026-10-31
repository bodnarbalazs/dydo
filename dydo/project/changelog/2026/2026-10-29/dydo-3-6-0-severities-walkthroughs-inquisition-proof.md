---
area: general
type: changelog
date: 2026-10-29
---

# dydo 3.6.0 - Review severities, milestone walkthroughs, inquisition and proof

<!-- Draft. Items marked `pending: DYD-xxx` describe work not yet on master; confirm each against
its merged PR, then drop the marker, or drop the item if it misses the release. Before tagging:
bump the version pins listed in .github/workflows/release.yml, point DynaDocs.csproj
PackageReleaseNotes at this entry, and add the "Align runtime and npm versions" line under Changed. -->

## Summary

Process release built from a retro of ten days of dydo and LC agent sessions. A reviewer now grades
every finding `blocker`, `should` or `nit`, and only a blocker fails a review: a should becomes a
ticket, a nit a line on the record, and none is dropped. A Project is cut into Linear Milestones,
the first one a walking skeleton, and the user walks each Milestone as it lands instead of seeing
the work only at the end. The inquisition is rebuilt: each round draws a fresh area map, runs Read,
Break and Drive on every area, triages what it finds by severity, and repeats until a round finds
no blocker and no should. A captain watches its PR through CI and review bots rather than reading
the checks once. `update-dydo` recognises the merges of an earlier update and activates only the
checkout it runs in. The `wizard` skill checks each stage's result without seeing a secret.
<!-- pending: DYD-397 --> Under T3, an officer dispatches captains, crew and reviewers with
`delegate_task`.
<!-- pending: DYD-401 --> Proof gets one definition: the crew member hands up a recipe and the
captain reruns it.
<!-- pending: DYD-406 --> The guard stops refusing ordinary commands the retro caught it on, and
refuses a Python program fed on stdin.
<!-- pending: DYD-409 --> Severity becomes a Linear label group, and `update-dydo` compares the
team's Linear setup with the standard.
Existing projects take the v3.6.0 `update-dydo` skill before running it; see Upgrade steps.

## Added

- Review severities ([DR 058](../../../decisions/058-review-severities-blocker-should-nit.md),
  DYD-398). The `reviewer` skill labels every finding:
  - `blocker`: the intended case is wrong. A review with one or more blockers is a FAIL.
  - `should`: a real bug callers will hit, inside the ticket's intent.
  - `nit`: anything else, however serious it looks: another code path, a rare case, a small
    performance cost, extra compatibility, style.

  A PASS may list its shoulds and nits under `Deferred`. The old rule "there is no PASS with notes"
  is gone; in its place, every finding is answered (fixed, ticketed, recorded, or relabelled with
  evidence) and never dropped. The review block forms in the Linear Workspace Standard carry
  `severity:` on each finding and an optional `Deferred` list on a PASS.
- The ticket captain sends only blockers to a fix hop. Each should becomes its own ticket (a Bug
  by default, AFK, `Backlog`, in the source's Project, linked to the review), and it natively blocks
  the Project's landing Merge Ticket. Each nit is one line on the ticket record, listed again at the
  landing walkthrough. A fixing `code-writer` or `docs-writer` checks each label against the source
  and records any relabel with evidence. A merged two-reviewer block fails only if either reviewer
  found a blocker. The PR body's `## Independent review` gains a `**Deferred:**` line. The admiral
  files a plan review's shoulds as `Backlog` tickets and lands a Project only once every should
  ticket is `Done` or closed with a reason.
- Milestone walkthroughs ([DR 060](../../../decisions/060-milestone-walkthroughs-and-the-walking-skeleton.md),
  DYD-399). When charting a Project, the map holder places a few Linear Milestones, each a chunk of
  work where drift from the user's intent would show. The first is the walking skeleton, the rough
  foundation the rest is built on; the landing is the last. Every delivery ticket belongs to one
  Milestone, and `to-ticket` and `wayfinder` ask for it. When a Milestone's last ticket merges into
  the feature branch, the admiral offers its Walkthrough Ticket. The user walks it with
  `walkthrough` or skips it, and a skip means go on. Only the skeleton's Walkthrough Ticket blocks:
  it is a native blocker of the next Milestone's tickets, so nothing is built on the skeleton before
  the user has looked at it or skipped it. In overnight mode the admiral posts `DECIDED` on it and
  carries on. `chief-of-staff` lists prepared Walkthrough Tickets in the user's queue, the
  skeleton's first.
- The `walkthrough` skill walks the whole product as it stands at a Milestone's SHA, or at main on
  landing, newest changes first, with the parts that need the user's careful look marked. It lists
  each deferred should and its ticket; at the landing it also lists every unaddressed nit and every
  open inquisition nit ticket. The verdict or the skip goes on the Walkthrough Ticket.
- Rebuilt inquisition ([DR 059](../../../decisions/059-inquisition-rebuilt-area-map-three-phases-rounds-until-clean.md),
  DYD-400):
  - Commissioning it, the admiral asks for a provider and model for each phase, the swarm width,
    the Codex cap and the crew context limit (110k tokens by default). A project with a runnable
    surface and no verification skill gets one first, through `create-verification-skill`.
  - Each round, the captain draws an area map from the landing diff and the file layout, as a
    table on the Inquisition ticket. One file can be an area; a seam between areas is its own area.
  - One `inquisitor` per area per phase. Read and Drive change nothing; Break commits proof tests
    on `proof/<KEY>-<area>-<n>`. Areas run in parallel up to the swarm width. The inquisitor's new
    resources are `read.md`, `break.md`, `drive.md` and `context-size.md`. The last gives a Node
    script that prints the agent's own context size on Claude Code and on Codex, so an agent past
    its limit hands the rest of its area back.
  - Findings go to `inquisition-register.md` on the audit branch `inquisition/<slug>`; the same bug
    seen twice is one finding with two sightings. Blockers and shoulds become Bug tickets, the
    round's nits one nit ticket, judgment calls a Question.
  - A proven blocker or should can get its fix prepared mid-round on the Bug's own branch. The Bug
    stays with the inquisition captain until the patch is attached, and the Bug's captain then
    verifies and reviews that patch instead of rebuilding it.
  - Above about 10 areas the map splits into group Sub-tickets, each with its own captain and its
    own audit branch.
  - Each round starts from zero. The next waits until every blocker and should fix is `Done`, and
    the Project lands only after a round with no blocker and no should. Only the user can defer an
    inquisition's should.
  - The inquisition writes its own record at `dydo/project/inquisitions/<YYYY-MM-DD>-<slug>.md`
    through a docs-writer and its Merge Sub-ticket, and closes once.
- `maintain-verification-skill` takes a verification gap an inquisition found, sorts it as a
  harness gap, doc drift or product gap, and drives the fix live before it ships (DYD-400).
- The ticket captain watches its PR instead of reading `gh pr checks` once (DYD-402). It handles
  existing checks and comments first, then watches with `watch_pull_request` in T3, or one
  long-running watcher elsewhere, never a sleep loop. In T3 the top-level officer owns the watch, so
  a delegated captain returns `PR open <key>, checks pending: <url>, head <SHA>` and ends its turn.
  A wake is news, not a merge: the captain acts only on checks and comments newer than the latest
  push, grades each bot finding by severity, tells repository failures from infrastructure flakes,
  and rechecks the gate itself before `Ready to Merge`. Adapted from tstack's `babysit-pr`
  (MIT), credited in `THIRD-PARTY-NOTICES.md`.
- <!-- pending: DYD-410 --> The admiral watches a delegated captain's PR on that pending return and
  wakes the captain on news, in place of its own one-shot `gh pr checks`.
- <!-- pending: DYD-401 --> `maintain-verification-skill` gains a `## Proof` section, the one place
  dydo says what proof is: evidence you observed yourself in this session, such as command output,
  a reproduction or a screenshot. Green mocked tests do not prove the user experience works. The
  crew member hands up a proof recipe, the exact commands and the evidence each showed. The captain
  reruns it before offering the PR, and that rerun is the proof of record; the admiral does not
  repeat it. `code-writer`, `reviewer`, `ticket-captain` and every new `verify-<app>` skill point
  there. The captain's Offer step gets a five-item merge gate. A reviewer with no Linear access gets
  the contract pasted into its brief. Adapted from tstack's `full-send` (MIT).
- <!-- pending: DYD-409 --> The Linear Workspace Standard adds a third label group, `Severity`,
  with `blocker` (`#E5484D`), `should` (`#FFB224`) and `nit` (`#8B8D98`). Only a ticket born from a
  finding carries one; Priority never carries severity. The landing check becomes a query: no open
  `blocker` or `should` ticket in the Project unless the user deferred it. `update-dydo` reads the
  team's label groups, labels, statuses and templates through the session's Linear connection,
  lists every difference from the standard, and creates a missing one only with the user's OK.

## Changed

- `update-dydo` (DYD-404):
  - A file's base can now be proved by the record of an earlier update to tag T: the commit that
    set `dydoVersion` to T and the commit message or PR text that lists the file as merged or kept.
    A re-run no longer treats files the last update merged as having no base. A revision the
    update wrote but whose record does not prove it, or a missing or contradictory record, leaves
    the base unknown.
  - A deletion an Upgrade step of a later release settles (it retires, moves or deletes the file)
    is followed without asking and listed.
  - `dydo init <host> --join` and `node setup-skills.mjs` run only in the update's own checkout.
    Every other checkout is listed with what to run there once its branch carries the update.
    `--join` writes the committed `.claude/settings.json`, so running it on a branch without the
    update edited that branch.
  - The reference install runs `cd <empty temp folder> && dydo init all` as one command, outside
    any folder holding a `dydo.json`.
  - The final report hands the user a commit message and PR text listing every file merged, kept,
    skipped or left to them. That text is the record the next update reads.
- The `wizard` skill gives each stage an outcome the agent can check without a secret, and checks
  it after the user runs the stage. Only where the user would otherwise paste output back, the
  script may log it, never a secret, to a temp file the agent reads and deletes (DYD-408).
- `inquisitor` severities are `blocker`, `should` and `nit`, replacing `high`, `medium` and `low`
  (DYD-400).
- The Walkthrough ticket template replaces `## What landed` with `## Checkpoint` and adds
  `## Verdict`. The Inquisition template has `## Scope`, `## Area map`, `## Findings` and
  `## Record`. The glossary defines Severity, Milestone (no longer optional), walking skeleton,
  Round, Area, Area map, Phase, Findings register, Sighting, Verification gap, Patch lane, Nit ticket
  and Group Sub-ticket (DYD-398, DYD-399, DYD-400).
- `what-would-balazs-do` counts the user's walkthrough verdicts, findings and skips as his own
  words (DYD-399).
- <!-- pending: DYD-397 --> Dispatch under T3 follows the child's lifetime (DR 057, reversing the
  3.5.0 rule that a same-vendor child is a native subagent). A native subagent only when the
  dispatcher waits for it inside its own turn; `delegate_task` for every other child that reports
  back, captains, crew and reviewers alike, and any agent of another vendor. A new top-level
  thread only for work the user takes over, or when the user asks. A native child cannot call
  `delegate_task`: on T3's `parent_not_active` refusal it hands the dispatch up. Every brief that
  sends Codex agents names a cap, starting near 8 and backing off on a 429. An officer that needs
  the user while work runs asks through the host's question tool; crew never ask the user
  directly. <!-- link DR 057 once PR #198 lands: ../../../decisions/057-t3-dispatch-follows-child-lifetime.md -->
- <!-- pending: DYD-407 --> The captain cleans the worktrees it or its crew made, with
  `dydo clean --dry-run <worktree>` then `dydo clean <worktree>`, and leaves the worktree its own
  thread runs in to T3. `dydo clean` now says `skipped <path>: T3 owns worktrees under <root>;
  leave them to T3`; it used to advise archiving the thread, which does not remove the worktree.
- <!-- pending: DYD-412 --> The shipped working-tree contract carries the pending-PR handoff: a
  delegated captain returns `PR open <key>, checks pending: <url>, head <SHA>`, and its officer
  watches the PR and wakes it.

## Fixed

- <!-- pending: DYD-406 --> The guard (retro items GS-01 to GS-06; absorbs DYD-379 and DYD-294):
  - A command nudge matches only where a command runs, not inside quoted arguments, comments or
    heredoc bodies. An `rg` pattern or a commit message naming `dotnet test` or `gh pr create` no
    longer triggers one. A project's own nudge whose pattern starts at an argument no longer fires.
  - Writing literal text passes: `cat > notes.md <<'EOF'` with `$` or backticks in the body, a
    single-quoted `printf` or `sed -i`, a PowerShell `@'...'@` here-string, and
    `cat > run.sh <<'EOF' ... && bash run.sh`. Expanding bodies (`<<EOF`, `@"..."@`) and dynamic
    destinations are still refused.
  - The password-store rule matches `pass` and, newly, `gopass` only as a command, so a Python
    `pass` statement or a PR body saying "pass" goes through.
  - PowerShell `$p = Get-Content notes.md` (and `gc`, `cat`, `type`, `Select-String`) reads as a
    read of that path; the off-limits check still applies.
  - `gh pr create --help` is no longer taken for a PR call by the PR-ready check.
  - A Python program on stdin is refused like `python -c`: `python -`, `py -`, or `python` with no
    script while a heredoc, here-string, pipe or `<` feeds it. A script file still runs; the guard
    does not follow its own file operations.
- <!-- pending: DYD-403 --> `dydo init claude` and `--join` no longer warn that `CLAUDE.md` or
  `CLAUDE.local.md` hides `AGENTS.md` when the file's first line is `@AGENTS.md` (`@../AGENTS.md`
  for `.claude/CLAUDE.md`). `dydo init codex` and `--join` leave `.codex/rules/dydo.rules` alone
  when only its line endings differ, and rewrite a stale one with the line endings it had, so a
  CRLF checkout no longer shows it modified.
- <!-- pending: DYD-411 --> The control-flow map lists `maintain-verification-skill` as the place
  proof is defined, reached from `code-writer`, `reviewer`, `ticket-captain` and `verify-<app>`.
- <!-- pending: DYD-378 --> Two viewer refresh tests wait for the `refresh-removed` marker instead
  of racing React Flow. Test-only.

## Removed

- The inquisition's lenses (correctness, coverage, security, dead code, doc drift, seams),
  read-only inquisitors, the separate record-delivery Feature and its `released <key>: record
  delivery` loop. The two release reasons an inquisition still has are
  `released <key>: verification skill first` and `released <key>: groups <keys>` (DYD-400).

## Upgrade steps

1. Install the 3.6.0 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
   <!-- pending: DYD-406, DYD-403, DYD-407 --> It carries the guard, `init --join` and
   `dydo clean` fixes; this release's other changes are skill and doc text.
2. Take `skills/productivity/update-dydo/SKILL.md` from the v3.6.0 tag before running it. The
   3.5.1 skill runs `--join` in every worktree, editing committed hooks on branches you do not own,
   and finds no base for files the previous update merged. This replaces 3.5.1's step 2.
3. A project whose last update left no record listing the files it merged gets more files with an
   unknown base on this run, and reconciles them by hand. Keep the commit message and PR text the
   skill hands you at the end: the next update reads them.
4. Merge the release's `skills/` and `Scaffold/dydo/` changes into the project, keeping its edits,
   and copy `THIRD-PARTY-NOTICES.md` along with `skills/`: it now credits tstack (MIT) for the
   adapted PR-watch and proof text. Four files are new:
   `skills/roles/crew/inquisitor/resources/{read,break,drive,context-size}.md`. No skill is
   renamed. The Scaffold files are `guides/working-tree-contract.md`,
   `reference/linear-workspace-standard.md`, `reference/dydo-glossary.md`,
   `reference/about-dynadocs.md` and `project/_project.md`;
   <!-- pending: DYD-407 --> also `reference/dydo-commands.md`.
5. <!-- pending: DYD-406 --> `files-off-limits.md` changes only its prose: the Protected Patterns
   paragraph now says the guard refuses inline interpreter code and a Python program on stdin. The
   guard reads the pattern blocks, not this paragraph, so the update is optional. The file is
   protected: `update-dydo` shows the diff and a person applies it. A new `dydo init` writes the new
   text. In the dydo repository itself, the maintainer applies the same change to
   `dydo/files-off-limits.md` after the guard fix lands, from the patch the guard ticket left.
6. Run `dydo init <host> --join` for each wired host, then `node setup-skills.mjs`, in the update's
   own checkout only. This overrides 3.5.1's step 4, which ran them in every checkout. In each
   other checkout, run them once its branch carries the update, when
   `git -C <checkout> show HEAD:dydo.json` records `dydoVersion` 3.6.0 or later.
7. Hooks: no 3.6.0 change alters the hook entries `dydo init` writes. On a project already on 3.5.1,
   `git diff --stat -- .claude/settings.json .codex/hooks.json` after step 6 should list nothing; if
   it lists `.codex/hooks.json`, commit it and re-trust the hooks in Codex.
   <!-- pending: DYD-403 --> A CRLF `.codex/rules/dydo.rules` no longer shows modified after
   `--join`; a copy restored by hand after 3.5.1 needs nothing more.
8. <!-- pending: DYD-409 --> Linear: the team needs a `Severity` label group with `blocker`
   (`#E5484D`), `should` (`#FFB224`) and `nit` (`#8B8D98`). With a Linear connection in the
   session, `update-dydo` lists every label, group, status and template the team lacks and offers
   to create each with your OK. Without one, it lists the category and skips it; create the group
   by hand in the team's label settings.
9. From the next Project you chart: place Linear Milestones, the walking skeleton first and the
   landing last, with one Walkthrough Ticket each, and wire the skeleton's as a native blocker of
   the next Milestone's tickets. Write Walkthrough tickets with `## Checkpoint` and `## Verdict`
   instead of `## What landed`. In a Project already in flight, make its landing Merge Ticket
   natively blocked by every open should ticket. Neither DR 059 nor DR 060 asks for any other
   migration of in-flight Projects or old inquisition records.
10. Before the next inquisition: a project with a runnable surface needs a verification skill
    (`create-verification-skill`), and the context-size check needs Node and crew dispatched as
    their own T3 sessions; without them the inquisitor records its context as `unavailable`.
    <!-- pending: DYD-397 --> Under T3, officers dispatch through T3's `delegate_task`, so the T3
    MCP tools must be available to them.
    <!-- pending: DYD-401 --> An existing generated `verify-<app>` skill does not gain the pointer
    to `maintain-verification-skill`'s Proof; regenerate it or add the line by hand.
11. Run `dydo adopt 3.6.0` last, once `dydo check` exits 0 and the update is proven.

No 3.6.0 commit renames a term mechanically, so this release has no merge normalisations.

## Files Changed

`Scaffold/dydo/` (`guides/working-tree-contract.md`, `project/_project.md`,
`reference/about-dynadocs.md`, `reference/dydo-glossary.md`,
`reference/linear-workspace-standard.md`), `THIRD-PARTY-NOTICES.md`,
`npm/THIRD-PARTY-NOTICES.md`, `dydo/guides/` (`customizing-roles.md`,
`orchestration-pitfalls.md`, `working-tree-contract.md`, `writing-good-briefs.md`),
`dydo/project/decisions/` (031, 045 and 047 banners; new 058, 059 and 060),
`dydo/project/inquisitions/_inquisitions.md`, `dydo/reference/` (`about-dynadocs.md`,
`dydo-glossary.md`, `linear-workspace-standard.md`), `dydo/understand/` (`control-flow.md`,
`task-lifecycle.md`, `work-model.md`), `skills/engineering/` (`maintain-verification-skill`, `pr`,
`wizard`), `skills/productivity/` (`README.md`, `overnight-mode`, `to-ticket`, `update-dydo`,
`walkthrough`, `wayfinder`, `what-would-balazs-do`), `skills/roles/README.md`,
`skills/roles/crew/` (`code-writer`, `docs-writer`, `inquisitor` and its four new resources,
`reviewer` and its `code.md` and `docs.md`), `skills/roles/officers/` (`admiral`,
`chief-of-staff`, `ticket-captain` and its `resources/inquisition.md`), this entry and its
changelog hub.

<!-- pending: DYD-397 --> `dydo/project/decisions/057-t3-dispatch-follows-child-lifetime.md`.
<!-- pending: DYD-401 --> `skills/engineering/README.md`, `create-verification-skill`.
<!-- pending: DYD-403 --> `Commands/InitCommand.cs`, `DynaDocs.Tests/Integration/InitCommandTests.cs`.
<!-- pending: DYD-406 --> `Services/BashCommandAnalyzer.cs`, `Services/PrCallScanner.cs`,
`Commands/GuardCommand.cs`, `Scaffold/dydo/files-off-limits.md`, `dydo/files-off-limits.md`,
`dydo/understand/guard-system.md`, `DynaDocs.Tests/` (`GuardRetroCommands.cs`,
`Commands/GuardCommandTests.cs`, `Integration/GuardPrReadyTests.cs`,
`Integration/GuardSecurityTests.cs`, `Integration/GuardWarnOrderTests.cs`,
`Services/BashCommandAnalyzerTests.cs`, `Services/PrCallScannerTests.cs`).
<!-- pending: DYD-407 --> `Services/WorktreeCleaner.cs`,
`DynaDocs.Tests/Integration/CleanCommandTests.cs`, `Scaffold/dydo/reference/dydo-commands.md`,
`dydo/reference/dydo-commands.md`.
<!-- pending: DYD-378 --> `viewer/src/app/App.test.tsx`.
<!-- pending: version bump --> `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`.

## Related

- [dydo 3.5.1 - Downstream update fixes](../2026-10-27/dydo-3-5-1-downstream-update-fixes.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
