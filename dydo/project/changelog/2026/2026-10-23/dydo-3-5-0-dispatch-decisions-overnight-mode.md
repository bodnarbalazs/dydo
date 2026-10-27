---
area: general
type: changelog
date: 2026-10-23
---

# dydo 3.5.0 - Dispatch rule, decision skill and overnight mode

## Summary

Minor release adding overnight mode (a machine mode flag, a `dydo mode` command and a guard nudge on
it), a decision skill that answers "what would balazs do", `dydo clean` for removing worktrees
without losing work, an `update-dydo` skill with `dydo adopt` to record the adopted release, a
dispatch channel rule, formal verification skills, a rule that wizards never carry decisions,
`dydo pr-ready` with a guard refusal on opening a PR from unpushed work, and a soft notice for new
Markdown outside `dydo/`.
`AGENTS.md` becomes the only entry file, "ticket" and "the user" become the preferred terms
(`issue-captain` and `to-issue` are renamed), officers dispatch crew as children, and seven
productivity skills become model-invoked. It removes the no-op Stop hook, the dead dydo 2 audit
trail and comments that only restated the code. Existing checkouts must rerun
`dydo init <host> --join`; see Upgrade steps.

## Added

- Overnight mode (DYD-351): the `overnight-mode` skill runs the chief of staff's muster, the
  standing orders and per-Project night orders every admiral obeys, the night watch, and the
  morning brief that ends it. `dydo mode show` lists the machine's mode flags and
  `dydo mode set <flag> <value>` stores one in a per-user file (`dydo/mode.json` under the local
  application data), so every project and agent on the machine reads the same values. One flag
  ships: `overnight`, `on` or `off`, default `off`.
- A built-in guard nudge on `dydo mode set` (DYD-351). On a Claude Code hook that carries the
  new `--host claude` marker, in the `default`, `acceptEdits`, `plan` or `auto` permission modes,
  an agent's first try is blocked with a reminder that only the chief of staff sets the mode, and
  the rerun gets an `ask` decision. Everywhere else (Codex, `bypassPermissions`, `dontAsk`, an
  unmarked hook) every try is denied, with no reminder, and the user runs the command in his own
  terminal. The nudge lives in code, so `dydo.json` cannot drop or weaken it. The guard reads
  the command with the rules of the shell that runs it (bash, PowerShell, `cmd /c`), so quotes,
  here-strings, `$(...)` substitutions and wrappers such as `env` or `sudo` are seen, while a
  commit message that only mentions `dydo mode set` is not. `dydo init claude --join` rewrites an
  older Claude Code hook to carry the marker. The nudge stops accidents, not a deliberate bypass.
- The `what-would-balazs-do` decision skill and its tribunal (DYD-350, DR 054): a trigger map
  decides a weighty call or prepares it as a Question, with a ballot and a judge brief. Control-flow
  section 6.1 sends a call above the officer's say through it when the call is DR-grade or
  irreversible, or when the user is away. Only a call the tribunal decides while he is away stays
  unwired, for him to ratify or overturn; every other call gets a prepared Question wired to its
  waiters.
- `dydo clean` (DYD-362, PR #163): removes this repository's linked worktrees, named by path or
  branch, or all of them with `--all`; `--dry-run` shows what would happen and changes nothing.
  Before removing a tree it archives the tree's uncommitted work (a tracked-changes patch,
  untracked files, ignored evidence under the dydo root's `agents/`, and its links) under
  `<git common dir>/dydo/clean-archive/`, and removes nothing if the archive fails. `--archive`
  lists the archive. It unlinks junctions and symlinks before deleting, so a link's target
  survives, and handles Windows paths over 260 characters. It never touches the main checkout,
  the tree holding the current directory, a locked or busy tree, a tree under `~/.t3/worktrees`, or
  one whose submodule holds unpushed or dirty work. The local branch is deleted only when origin's
  default branch already holds its work; otherwise it is kept and the line says so. Archive
  entries older than 90 days are pruned, then the oldest first past `clean.archiveSizeCapMb`
  (default 1024). Agents may run it: the guard allows it, and `dydo init` writes
  `Bash(dydo clean:*)` and `PowerShell(dydo clean:*)` into Claude Code's allow list and a
  `prefix_rule` into `.codex/rules/dydo.rules` for Codex.
- `dydo clean` recognises squash- and rebase-merged branches (DYD-363, PR #164): a branch whose
  changes origin's default branch already holds, found by a `git merge-tree` in a scratch
  repository, is deleted like a merged one. The archive skips build output (`bin`, `obj`,
  `node_modules`, `.vs`) under `agents/` and counts what it skipped.
- The `create-formal-verification-skill` and `maintain-formal-verification-skill` skills
  (DYD-344), adapted from the cursor/plugins pstack at 5dba0b8d (MIT), registered in the skill
  READMEs, the control-flow table, the glossary and the third-party notices.
- The `update-dydo` skill (DYD-360, PR #171) updates an installed dydo project to the latest
  release and keeps the project's own edits. Run in the user's own top-level session, it updates
  the CLI, applies each release entry's Upgrade steps, merges `skills/`, `setup-skills.mjs`,
  `THIRD-PARTY-NOTICES.md`, `Scaffold/dydo/` and the entry point three-way, rewires the hosts,
  proves the update took effect and records the new version. It does not install dydo into a
  project that lacks it. It is user-invoked and version-agnostic: it finds what to compare in the
  project, the release tags and a fresh `dydo init`, so it holds no checklist to update per
  release; release work comes from each entry's Upgrade steps.
- The `dydoVersion` field in `dydo.json` and `dydo adopt <version>` (DYD-360, PR #171).
  `dydoVersion` names the release whose skills and documents the project adopted. A fresh
  `dydo init` writes it, `--join` leaves it alone, and `dydo adopt 3.5.0` (or `v3.5.0`) sets it.
  Only the top-level session may run `dydo adopt`. Each release entry now carries an Upgrade steps
  section, which the skill reads.
- `dydo pr-ready [<path>] [--body <file>]` (DYD-374, PR #176) checks that the tree has no staged,
  unstaged or untracked files and that HEAD equals its upstream; with `--body`, that the PR body
  names a ticket key and has an `## Independent review` section holding a `PASS - ` block. It exits
  1 listing every problem, otherwise prints `ready:`. It is on the sub-agent allow-list.
- A soft notice for new Markdown outside the dydo tree (DYD-375, PR #175). A write tool creating a
  new `.md` there gets "NOTICE: Docs usually live under `dydo/`. If this is a pointer README, or it
  belongs here, carry on." and is never blocked. The root `README.md`, `CLAUDE.md`, `AGENTS.md`,
  `CHANGELOG.md` and `THIRD-PARTY-NOTICES.md`, and anything under the root `dydo/`, `skills/`,
  `.claude/`, `.agents/`, `.codex/` and `.github/`, get no notice; editing an existing file never
  fires it. It is a new-file nudge with severity `notice` in the `dydo.json` defaults: `dydo init`
  includes it, and `dydo adopt` adds it once, if missing, when crossing to 3.5.0 (a recorded
  `dydoVersion` below 3.5.0 or none), so a later edit or removal stands. `--join` is unchanged.
- A proposal for an overnight chief of staff, recorded in PR #153 and now marked `promoted` into
  the shipped overnight mode above (DYD-351).

## Changed

- `AGENTS.md` is the only entry file (DYD-368, PR #167). `dydo init` writes it in every mode and
  no longer writes `CLAUDE.md`; Claude Code 2.1.281 or later reads `AGENTS.md`. With `claude` or
  `all`, `dydo init` and `--join` remove a `CLAUDE.md` that is dydo's own generated copy: one equal
  to `AGENTS.md` or to the entry template, or one an older dydo release generated (DYD-372,
  PR #170), recognised by its text below the title line, so a copy titled with another folder
  name is removed too. A `CLAUDE.md` the user wrote is kept, with a warning that it hides
  `AGENTS.md`; `CLAUDE.local.md` and `.claude/CLAUDE.md` are never removed and each gets its own
  warning.
- "Ticket" is the preferred term for Linear's Issue (DYD-361, PR #162). The `issue-captain` skill
  is now `ticket-captain` and `to-issue` is now `to-ticket`. The glossary is the one place that
  pairs the two words. The map viewer says "tickets" and "sub-ticket(s)", and future-feature
  frontmatter refuses `ticket` and `sub-ticket` as well as `issue` and `sub-issue`.
- "The user" is the preferred term for the person running dydo (DYD-367, PR #165), replacing "the
  human" in the entry files, Scaffold, guides, reference, understand and future-features. The
  glossary term Human command is now User command.
- Seven productivity skills are model-invoked (DYD-359, PR #161): `handoff`, `ketchup`,
  `in-your-own-words`, `teach`, `walkthrough`, `to-ticket` and `to-project`, so a `/name` typed
  mid-sentence loads them. Each description says to load it only when the user asks; `handoff`
  may also load when an officer briefs an agent it dispatches, and `walkthrough` at the admiral's
  landing step. The admiral now loads `walkthrough` itself at landing instead of asking the user to
  invoke it (DYD-363, PR #164).
- Officers dispatch crew as children of their own turn (DYD-369, PR #166): a native subagent for
  the same vendor, T3 `delegate_task` for the other, never a new top-level thread. On
  `parent_not_active` a captain hands the dispatch to its officer and the chief of staff asks the
  user; the admiral dispatches the captain's crew for it. The working-tree contract opens a
  top-level thread only for HITL work or when the user asks for one.
- `what-would-balazs-do` judges by the spirit of his words, not just by matching case law (DYD-371,
  PR #169). Case law is evidence of his intent: the tribunal decides in that spirit with or without
  a precedent, and a recorded decision that goes against it or no longer fits loses to it. The
  position names that decision and why, and the call becomes DR-grade, or at least flagged. A
  citation carries the reason behind his words; an inferred reason is marked inferred and names
  the DRs or docs it rests on. DR 054 records the amendment.
- The admiral's memory notes moved into short lines across the skills and guides (DYD-384,
  PR #181).
- Code comments keep only the why (DYD-366, PR #168): a sweep across the hand-written source
  removed comments that restated the code, recorded history or duplicated another. The Claude
  guard matcher no longer names `AskUserQuestion`. CLI messages say "the user" where they meant
  the person running dydo.
- The guard blocks `git worktree remove --force` and a recursive delete of a worktree root, and
  points to `dydo clean --dry-run <worktree>` then `dydo clean <worktree>`, run from outside the
  tree (DYD-362). The skills and guides use `dydo clean` in place of `git worktree remove`,
  `git branch -d` and `git fetch --prune`.
- The wizard skill bans decisions (DYD-343): settle each in conversation before authoring. A
  wizard script asks the user only for operational confirmations (y/N or Enter to continue) and
  captured values (a secret, an identifier, a path).
- A dispatch channel rule and a two-vendor first review round, with the reviewer counts and
  human-in-the-loop threads aligned to the working-tree contract (DYD-346).
- `dydo guard` accepts `--host claude`, which `dydo init` writes into the Claude Code hook. It is
  a marker, not a diagnostic argument: the call still reads its payload from stdin.
- The decision-record hub states when to write a record (all three of hard to reverse, surprising
  without context, a real trade-off) and documents the `ratification: pending` frontmatter for a
  `proposed` record a tribunal decided while the user was away (DYD-350).
- `diagnosing-bugs` and `code-writer` treat the machine as shared: a race is forced with fake
  timers or a forced interleaving, never with synthetic CPU load, and any load a reproduction adds
  stays small and cleans up after itself.
- The `README.md` Commands table lists `dydo adopt`, `dydo mode` and `dydo clean`, and it and
  `files-off-limits.md` say that `dydo index` overwrites `dydo/index.md` with a fixed navigation
  page. The guard-system page names `adopt` among the top-level-only commands.
- The guard refuses `gh pr create`, `gh pr new` and `gh pr ready` from a tree whose work is not
  committed and pushed (DYD-374, PR #176). It runs the tree half of `dydo pr-ready` on the
  payload's working directory or the project root, and its deny decision points to
  `dydo pr-ready`. It stops accidents: wrappers, `eval`, aliases and `gh -R x pr` are accepted
  misses. The `pr` and `ticket-captain` skills each say to write the PR body outside the tree and
  run `dydo pr-ready --body <file>` before opening the PR. The guard's shared command splitting
  now treats a backslash inside single quotes as literal.
- Native agent memory is off and guarded (DYD-383, DR 055). `dydo init` and `--join` write
  `"autoMemoryEnabled": false` into `.claude/settings.json` and `memories = false` under
  `[features]` in `.codex/config.toml`, keeping the rest of each file; an existing `true` is set to
  false and the output says so. The guard blocks agent writes, by file tool or shell, on the main
  and sub-agent lanes, to every host memory folder: Claude's `projects/*/memory/` and
  `agent-memory*/` under `~/.claude`, any `~/.claude*` profile and `CLAUDE_CONFIG_DIR`, the
  `autoMemoryDirectory` folder named in the project or user settings, the project's
  `.claude/agent-memory*/`, and Codex's `$CODEX_HOME/memories/` (default `~/.codex/memories/`).
  Reads stay allowed. The block names the path and points to DR 055: a lesson goes into a skill,
  a Decision Record or a doc. It replaces the guard's old exemption for
  `~/.claude/projects/*/memory/`, and amends DR 038.
- A README outside `dydo/` only points to the dydo doc that owns its folder (DYD-375, PR #175): the
  convention is in both coding-standards copies and the `docs-writer` skill.
- Reviewers run file-editing probes in their own detached worktree at the candidate SHA, removed
  after, and the ticket captain seats parallel reviewers in separate worktrees when either may run
  probes (DYD-380).
- Fixed: the viewer's App refresh-in-place tests are deterministic (DYD-364).
- Fixed: the `dydo clean` dry-run test compares the git object set instead of counting files under
  `.git/objects`, and test repositories disable auto maintenance, so `git maintenance run --auto`
  holding `maintenance.lock` no longer flakes it on Linux (DYD-376, PR #174).
- Fixed: the viewer fitting tests wait on React Flow's public `onMoveEnd` instead of spying on the
  App's internal `ProjectVisit.prototype.ready`; the `:841` mismatch was a source edit in a shared
  reviewer worktree, not a race (DYD-377, PR #177).
- Fixed: warn nudges run only after every hard block on the call, so a refused command (`gh pr create`
  on a dirty tree, a cd chain, `dydo mode set` where the host cannot ask) gets the same refusal on
  every try instead of alternating with the warning (DYD-386).
- Align runtime and npm versions to 3.5.0 and permit publication only on the exact v3.5.0 tag.

## Removed

- The Stop hook and `dydo guard --stop` (DYD-366, PR #168), a no-op since DR 041. `dydo init` and
  `--join` remove only dydo's own Stop entry, keep any other Stop hook, and drop the Stop array
  only when dydo's entry was its last. The repository's `.codex/hooks.json` loses its Stop entry.
- The repository's own `CLAUDE.md` (DYD-368, PR #167), which was identical to `AGENTS.md`.
- The README's Rant section (DYD-369, PR #166).
- The dead dydo 2 audit trail (DYD-340, PR #152): `dydo/_system/audit`, the audit-large test
  fixture, `GetAuditPath`, the audit-system reference page and the `_system/audit/` scan
  exclusion. A stale `_system/audit/` entry in a project's `dydo.json` is left alone by `check`
  and `fix`.

## Upgrade steps

1. Install the 3.5.0 CLI: `npm install -g dydo` or `dotnet tool update -g dydo`.
2. Merge the release's `skills/`, `setup-skills.mjs`, `THIRD-PARTY-NOTICES.md`, `Scaffold/dydo/` and
   `Scaffold/entry-point.md` into the project's `skills/`, same-named files, `dydo/` and dydo entry
   file, keeping the project's edits. The dydo entry file is whichever of `AGENTS.md` and
   `CLAUDE.md` derives from the 3.4.x entry point (its first heading and most of its lines); with
   neither, render `Scaffold/entry-point.md`, `{{PROJECT_NAME}}` set to the project name. Write the
   result to `AGENTS.md`. When both derive, merge `AGENTS.md`, then merge `CLAUDE.md`'s edits into
   that result. An `AGENTS.md` that does not derive is the user's: put the result at its
   top and keep their text below it; `--join` leaves an existing `AGENTS.md` alone. The guard blocks
   agent writes to `dydo/index.md`, `dydo/files-off-limits.md` and the other paths under its
   Protected Patterns, so a person applies those merges.
3. Two skills are renamed: `issue-captain` to `ticket-captain` and `to-issue` to `to-ticket`
   (DYD-361). Move each, with the project's edits, to the new path. Rename each live reference to
   the old names (project instructions, skill text, the entry file) to the new ones; keep
   historical records and quotations as they are.
4. For each wired host, run `dydo init <host> --join`; the Claude Code hook then carries
   `--host claude`, and native memory is turned off in `.claude/settings.json` and
   `.codex/config.toml` (DR 055).
5. `AGENTS.md` is the only entry file. Step 4's `--join` removes `CLAUDE.md` only when it equals
   the new `AGENTS.md` or the 3.5.0 template, or an earlier dydo release generated it. Delete by
   hand a `CLAUDE.md` that step 2 merged into `AGENTS.md`, whether or not Claude is wired. Keep a
   user-written one and put `@AGENTS.md` at its
   top (`@../AGENTS.md` in `.claude/CLAUDE.md`).
6. Run steps 4 and 10 in every checkout of the project, since the Claude Code wiring in
   `.claude/settings.local.json` and the skill links in `.claude/skills/` and `.agents/skills/`
   are machine-local. Each checkout needs the 3.5.0 CLI first: an older one fails on the hook's
   `dydo guard --host claude`. `--join` also writes the `dydo clean` allow rules and removes
   dydo's Stop hook, which otherwise fails on the removed `--stop` option at every turn end. The
   3.5.0 CLI removes it from `.claude/settings.local.json` and `.codex/hooks.json` only. Remove a
   copy anywhere else, such as one committed in `.claude/settings.json`, by hand, or rerun
   `--join` with a later CLI that also removes dydo's own entries there (DYD-392).
7. If `--join` warns about `CLAUDE.local.md`, put `@AGENTS.md` at its top, or set Claude Code's
   Project instructions setting to `claude-md-and-agents-md`.
8. Update Claude Code to 2.1.281 or later; earlier versions may read only `CLAUDE.md`.
9. Re-trust the Codex hooks. Codex pins hook trust by hash and `.codex/hooks.json` changed, so an
   untrusted hook leaves Codex sessions unguarded.
10. Run `node setup-skills.mjs` from the project root.
11. Empty each host memory folder that holds notes for this project (DR 055): Claude's
    `<profile>/projects/<project>/memory/`, the `autoMemoryDirectory` folder, `.claude/agent-memory*/`
    and Codex's `$CODEX_HOME/memories/`. Triage each note into a skill, a Decision Record or a doc,
    facts about the user into the user's own global `CLAUDE.md` or `AGENTS.md`, or drop it, then
    delete the folder. The guard blocks agent writes there, so a person does a delete it refuses.
12. After step 4, check by hand what no step sets (DR 055). `autoMemoryEnabled` is not `true` in
    `.claude/settings.local.json` or in any Claude profile's `settings.json`, and
    `CLAUDE_CODE_DISABLE_AUTO_MEMORY` does not turn memory back on. In the user's
    `$CODEX_HOME/config.toml` (default `~/.codex/config.toml`), `[features] memories` is not `true`;
    it is off by default and is the switch. Note any `permissions.deny` rule covering a memory
    folder as found. `dydo guard --action write --path ~/.claude/projects/p/memory/x.md` exits 2.
13. Run `dydo adopt 3.5.0`. It also adds the new-file nudge for Markdown outside `dydo/` to
    `dydo.json` when missing (DYD-375).

### Merge normalisations

Apply them in this order.

1. "issue" to "ticket" (DYD-361, PR #162), for Linear's Issue. Matching: plural and possessive
   kept, the article with it ("an Issue" to "a ticket"). A generic use becomes lowercase (`Issue`
   and `issue` to `ticket`) unless it starts a sentence, heading or table cell, sits in a
   title-case title (Linear Ticket Lifecycle) or names a heading (the Ticket labels section). A
   qualifier that is not a Type goes with it, a hyphen dropped (Atomic Issue, Atomic-Issue,
   Project-Issue, Linear Issue to atomic ticket, Project ticket, Linear ticket). A named kind
   stays capitalised: Issue Captain and each `<Type> Issue` (Merge, Question, Inquisition,
   Wayfinding, say) to their Ticket forms. Compounds follow the generic rule (Issue-review,
   issue-relevant, multi-Issue, cross-Issue to ticket-review, ticket-relevant, multi-ticket,
   cross-ticket); Sub-issue to Sub-ticket keeps its case; so do the `<issue-template>` tag and
   the skill names `issue-captain` and `to-issue` (step 3). Scope: live text in `skills/`, the
   entry files, `README.md`, `Scaffold/`, and the docs root's guides, reference, understand,
   future-features, `glossary.md` and hubs. dydo's own code, tests and viewer changed too,
   identifiers kept; no project holds them. Exceptions, left as they are: Linear tool and API
   names and `/issue/` URLs; code identifiers such as `MapIssue*` and `issueId`; placeholders
   such as `~/work/<issue>`; the future-feature frontmatter values `issue` and `sub-issue`, which
   stay refused; GitHub issues; "issue" meaning a problem, and the verb (issue, issued); history
   (changelog, Decision Records, inquisitions, plans, migrations, reviews, the agent workspace,
   viewer fixtures), the file names links to it carry, and quotations; the glossary's Ticket
   entry, the one place that pairs the two words ("Linear calls it an Issue"); in its Retired
   PM terms, the retired names (the observed-problem Issue, run-issues, Issue planner, the
   Issue-resolution plan), while the section's live references are normalised ("an Issue Captain
   owns one Issue" to "a Ticket Captain owns one ticket"). Not mechanical, so the merge carries
   them: Ticket leaves that section's retired list, the glossary's Ticket entry gains its pairing
   sentence, and the refused frontmatter list gains `ticket` and `sub-ticket`.
2. "human" to "user" (DYD-367, PR #165), wherever it names the person running dydo. Matching:
   case kept, in every form: "the human" and its possessive; "a human" and "Humans" to "the
   user" ("a user" where any user is meant); a bare noun takes the article ("human owns"
   to "the user owns"); bare labels, such as the diagrams' `human: an idea`, the tables'
   `| human |` and a chain's `→ human`; modifiers and compounds, such as the glossary term
   and role label Human command, human approval, human decision, human judgment, human input,
   human takeover, human-owned, human-confirmed, human-origin and Human-only, to their user forms
   ("The user's approval" where the person's own is meant). Scope: the entry files, `README.md`,
   `Scaffold/`, the docs root's guides, reference, understand, future-features, project hubs
   and `files-off-limits.md`, the `wizard` skill's `SKILL.md` and its line in the skills README
   (3.2.0 changed "the human" in the other skills). dydo's own tests changed too; no project holds
   them. Exceptions: human as opposed to agent (`writing-for-humans`, "human-readable", "human
   entry point", "human navigation", HITL's "live human participation"); "a human" in `wizard`'s
   template; external names (HumanLayer, Linear's `Waiting for Human` status); identifiers
   such as the mermaid style class (`classDef human`, `:::human`); quoted CLI text (the guard's
   "human-owned ... ask the human" refusal); history and quotations. Not mechanical, so the merge
   carries them: rewordings such as "before human harmonization" to "before the user harmonizes
   it" and "one human judgment that remains" to "one judgment left to the user".

## Files Changed

`Commands/` (`ModeCommand.cs`, `CleanCommand.cs`, `AdoptCommand.cs`, `PrReadyCommand.cs`,
`ReleasedEntryPoints.cs`, `GuardCommand.ModeSet.cs`, `GuardCommand.PrReady.cs`, `GuardCommand.cs`,
`GuardCommand.Refusal.cs`, `InitCommand.cs`, `HelpCommand.cs`, and comments in the check, fix and
graph handlers), `Models/` (`HookInput.cs`, `CleanConfig.cs`, `DydoConfig.cs`, comments elsewhere),
`Program.cs`, `Rules/` (`FutureFeatureRule.cs`, `OffLimitsRule.cs`), `Serialization/`, `Services/`
(`ModeState.cs`, `ShellReader.cs`, `DydoInvocationScanner.cs`, `PrReadyCheck.cs`,
`ConfigFactory.cs`, `ConfigService.cs`, `IConfigService.cs`, `BashCommandAnalyzer.cs`,
`CompletionProvider.cs`, `ScaffoldTree.cs`, the `dydo clean` services `WorktreeCleaner.cs`,
`MergedBranchJudge.cs`, `CleanArchive.cs`, `ArchivePlan.cs`, `ArchiveLink.cs`, `LinkSafeDelete.cs`,
`GitRunner.cs`, `GitResult.cs`, `SubmoduleWork.cs`, `WorktreeEntry.cs`, `WorktreeProcessProbe.cs`,
`ProbeStrandedException.cs`, `Map/`, comments elsewhere), `Utils/` (`DydoVersion.cs`, comments),
`Scaffold/entry-point.md`, `Scaffold/dydo/` (guides, reference, understand, glossary, project hubs,
files-off-limits), `AGENTS.md`, `CLAUDE.md` (removed), `README.md`, `npm/README.md`,
`dydo/index.md`, `dydo/glossary.md`, `dydo/files-off-limits.md`, `dydo/guides/`, `dydo/reference/`,
`dydo/understand/`, `dydo/project/decisions/054-what-would-balazs-do-tribunal.md`,
`dydo/project/decisions/038-auto-memory-policy.md`,
`dydo/project/decisions/055-native-agent-memory-off-and-guarded.md`,
`dydo/project/decisions/_decisions.md`, `dydo/project/future-features/`, `dydo/_system/audit/`
(removed), `dydo.json`, `.codex/hooks.json`, `.codex/rules/`, `skills/` (overnight-mode,
update-dydo, what-would-balazs-do, the two formal verification skills, wizard, the seven
model-invoked productivity skills, to-ticket (was to-issue), wayfinder, admiral, ticket-captain (was
issue-captain), reviewer, docs-writer, chief-of-staff, code-writer, engineering skills including
diagnosing-bugs and pr, READMEs), `setup-skills.mjs`, `THIRD-PARTY-NOTICES.md`,
`npm/THIRD-PARTY-NOTICES.md`, `npm/` (`install.js`, `bin/`, `lib/` comments), `viewer/src/`
(including `app/App.test.tsx`), `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/` (adopt command in
`Integration/AdoptCommandTests.cs`, `Utils/DydoVersionTests.cs`, released CLAUDE.md texts in
`Integration/ReleasedEntryTexts.cs`, help, completion, mode command, mode state, invocation scanner
in `Services/DydoInvocationScannerTests.cs`, guard mode-set, worker lane, host memory and PR-ready, pr-ready
command and `Integration/PushedRepository.cs`, new-file nudge (command and integration), clean
command, init, fix, check, validate, config and config factory, doc scanner, off-limits, command
smoke, entry-point parity, the `machine-mode.feature` scenarios and steps, canonical skill
assertions and steps, host canaries, coverage associations and `coverage/run_tests.py`, the removed
audit-large fixtures, and comment removals across the suite),
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`,
`npm/test/install.test.cjs`, this entry and its changelog hub.

## Related

- [dydo 3.4.1 - Guard refusals as a deny decision](../2026-10-18/dydo-3-4-1-guard-deny-decision.md)
