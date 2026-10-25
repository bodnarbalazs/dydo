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
dispatch channel rule, formal verification skills, and a rule that wizards never carry decisions.
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
- A built-in guard nudge on `dydo mode set` (DYD-351). An agent's first try is blocked with a
  reminder that only the chief of staff sets the mode. On the rerun the guard answers an `ask`
  decision only on a Claude Code hook that carries the new `--host claude` marker, in the
  `default`, `acceptEdits`, `plan` or `auto` permission modes; everywhere else (Codex,
  `bypassPermissions`, `dontAsk`, an unmarked hook) it denies and the user runs the command in his
  own terminal. The nudge lives in code, so `dydo.json` cannot drop or weaken it. The guard reads
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
  project that lacks it.
- The `dydoVersion` field in `dydo.json` and `dydo adopt <version>` (DYD-360, PR #171).
  `dydoVersion` names the release whose skills and documents the project adopted. A fresh
  `dydo init` writes it, `--join` leaves it alone, and `dydo adopt 3.5.0` (or `v3.5.0`) sets it.
  Only the top-level session may run `dydo adopt`. Each release entry now carries an Upgrade steps
  section, which the skill reads.
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
- `what-would-balazs-do` judges by the spirit of his words, not by matching case law (DYD-371,
  PR #169). Case law is evidence of his intent: the tribunal decides in that spirit with or without
  a precedent, and a recorded decision that goes against it or no longer fits loses to it. The
  position names that decision and why, and the call becomes DR-grade, or at least flagged. A
  citation carries the reason behind his words; an inferred reason is marked inferred and names
  the DRs or docs it rests on. DR 054 records the amendment.
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
- Fixed: the viewer's App refresh-in-place tests are deterministic (DYD-364).
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
   result to `AGENTS.md`. An `AGENTS.md` that does not derive is the user's: put the result at its
   top and keep their text below it; `--join` leaves an existing `AGENTS.md` alone. The guard blocks
   agent writes to `dydo/index.md`, `dydo/files-off-limits.md` and the other paths under its
   Protected Patterns, so a person applies those merges.
3. Two skills are renamed: `issue-captain` to `ticket-captain` and `to-issue` to `to-ticket`
   (DYD-361). Move each, with the project's edits, to the new path. Project text naming the old
   names is now stale.
4. For each wired host, run `dydo init <host> --join`; the Claude Code hook then carries
   `--host claude`.
5. `AGENTS.md` is the only entry file. Step 4's `--join` removes `CLAUDE.md` only when it equals
   the new `AGENTS.md` or the 3.5.0 template, or an earlier dydo release generated it. Delete by
   hand a `CLAUDE.md` that step 2 merged into `AGENTS.md`, whether or not Claude is wired. Keep a
   user-written one and put `@AGENTS.md` at its
   top (`@../AGENTS.md` in `.claude/CLAUDE.md`).
6. Run steps 4 and 10 in every checkout of the project, since the Claude Code wiring in
   `.claude/settings.local.json` and the skill links in `.claude/skills/` and `.agents/skills/`
   are machine-local. `--join` also removes dydo's Stop hook, which
   otherwise fails on the removed `--stop` option at every turn end, and writes the `dydo clean`
   allow rules.
7. If `--join` warns about `CLAUDE.local.md`, put `@AGENTS.md` at its top, or set Claude Code's
   Project instructions setting to `claude-md-and-agents-md`.
8. Update Claude Code to 2.1.281 or later; earlier versions may read only `CLAUDE.md`.
9. Re-trust the Codex hooks. Codex pins hook trust by hash and `.codex/hooks.json` changed, so an
   untrusted hook leaves Codex sessions unguarded.
10. Run `node setup-skills.mjs` from the project root.
11. Run `dydo adopt 3.5.0`.

## Files Changed

`Commands/` (`ModeCommand.cs`, `CleanCommand.cs`, `AdoptCommand.cs`, `ReleasedEntryPoints.cs`,
`GuardCommand.ModeSet.cs`, `GuardCommand.cs`, `GuardCommand.Refusal.cs`, `InitCommand.cs`,
`HelpCommand.cs`, and comments in the check, fix and graph handlers), `Models/` (`HookInput.cs`,
`CleanConfig.cs`, `DydoConfig.cs`, comments elsewhere), `Program.cs`, `Rules/`
(`FutureFeatureRule.cs`, `OffLimitsRule.cs`), `Serialization/`, `Services/` (`ModeState.cs`,
`ShellReader.cs`, `DydoInvocationScanner.cs`, `ConfigFactory.cs`, `ConfigService.cs`,
`IConfigService.cs`, `BashCommandAnalyzer.cs`, `CompletionProvider.cs`, `ScaffoldTree.cs`, the
`dydo clean` services `WorktreeCleaner.cs`, `MergedBranchJudge.cs`, `CleanArchive.cs`, `ArchivePlan.cs`,
`ArchiveLink.cs`, `LinkSafeDelete.cs`, `GitRunner.cs`, `GitResult.cs`, `SubmoduleWork.cs`,
`WorktreeEntry.cs`, `WorktreeProcessProbe.cs`, `ProbeStrandedException.cs`, `Map/`, comments
elsewhere), `Utils/` (`DydoVersion.cs`, comments), `Scaffold/entry-point.md`, `Scaffold/dydo/`
(guides, reference, understand, glossary, project hubs, files-off-limits), `AGENTS.md`, `CLAUDE.md`
(removed), `README.md`, `npm/README.md`, `dydo/index.md`, `dydo/glossary.md`,
`dydo/files-off-limits.md`, `dydo/guides/`, `dydo/reference/`, `dydo/understand/`,
`dydo/project/decisions/054-what-would-balazs-do-tribunal.md`,
`dydo/project/decisions/_decisions.md`, `dydo/project/future-features/`, `dydo/_system/audit/`
(removed), `dydo.json`, `.codex/hooks.json`, `.codex/rules/`, `skills/` (overnight-mode,
update-dydo, what-would-balazs-do, the two formal verification skills, wizard, the seven
model-invoked productivity skills, to-ticket (was to-issue), wayfinder, admiral, ticket-captain (was
issue-captain), chief-of-staff, code-writer, engineering skills including diagnosing-bugs, READMEs),
`setup-skills.mjs`, `THIRD-PARTY-NOTICES.md`, `npm/THIRD-PARTY-NOTICES.md`, `npm/` (`install.js`,
`bin/`, `lib/` comments), `viewer/src/`, `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/` (adopt command in
`Integration/AdoptCommandTests.cs`, `Utils/DydoVersionTests.cs`, released CLAUDE.md texts in
`Integration/ReleasedEntryTexts.cs`, help, completion, mode command, mode state, invocation scanner
in `Services/DydoInvocationScannerTests.cs`, guard mode-set and worker lane, clean command, init,
fix, check, validate, config, doc scanner, off-limits, command smoke, entry-point parity, the
`machine-mode.feature` scenarios and steps, canonical skill assertions and steps, host canaries,
coverage associations and `coverage/run_tests.py`, the removed audit-large fixtures, and comment
removals across the suite), `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `npm/test/install.test.cjs`, this entry and its
changelog hub.

## Related

- [dydo 3.4.1 - Guard refusals as a deny decision](../2026-10-18/dydo-3-4-1-guard-deny-decision.md)
