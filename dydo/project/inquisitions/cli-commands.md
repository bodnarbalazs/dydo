## 2026-04-09 — Brian

### Scope
- **Entry point:** Feature investigation — CLI command system
- **Files investigated:** Program.cs, Commands/InitCommand.cs, Commands/ValidateCommand.cs, Commands/CompletionsCommand.cs, Commands/CompleteCommand.cs, Commands/CleanCommand.cs, Commands/GuardLiftCommand.cs, Commands/AgentCommand.cs, Commands/TaskCommand.cs, Commands/TaskCompactHandler.cs, Commands/GuardCommand.cs, Commands/IssueCommand.cs, Commands/RolesCommand.cs, Commands/InquisitionCommand.cs, Commands/WorktreeCommand.cs, Commands/WatchdogCommand.cs, Commands/QueueCommand.cs, Commands/TemplateCommand.cs, Commands/MessageCommand.cs, Services/CompletionProvider.cs, Services/ValidationService.cs, Services/ShellCompletionInstaller.cs
- **Docs cross-checked:** dydo/reference/dydo-commands.md, Program.cs help text
- **Tests cross-checked:** DynaDocs.Tests/Commands/HelpCommandTests.cs, CompleteCommandTests.cs, CompletionsCommandTests.cs, ValidateCommandTests.cs, CommandDocConsistencyTests.cs, CommandSmokeTests.cs, ValidationServiceTests.cs, ShellCompletionInstallerTests.cs
- **Scouts dispatched:** 3 reviewers (Charlie, Dexter, Emma), 1 test-writer (Frank)

### Findings

#### 1. CompletionProvider.TopLevelCommands array is stale — 10 commands missing
- **Category:** bug
- **Severity:** high
- **Type:** tested
- **Evidence:** `Services/CompletionProvider.cs:7-12` lists 17 top-level commands. Program.cs registers 27 commands (lines 7-31). Missing from completion: `message`, `wait`, `issue`, `inquisition`, `template`, `roles`, `validate`, `watchdog`, `worktree`, `queue`. The `message` command also has alias `msg` (MessageCommand.cs:38). Users typing `dydo m<TAB>` will not see `message`. Confirmed by test: `CompletionProviderTests.GetCompletions_TopLevelCommands_Missing` (12 Theory cases all pass, proving absence).
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/CompletionProvider.cs (lines 7-12), Program.cs (lines 7-41, 43-153)
- **Independent verification:** Counted all 28 commands registered in Program.cs (lines 7-32 plus version at line 34 and help at line 43). CompletionProvider.TopLevelCommands has 17. Diff confirms 10 missing: message, wait, issue, inquisition, template, roles, validate, watchdog, worktree, queue. Also note `complete` is absent but this is reasonable as it's internal plumbing.
- **Alternative explanations considered:** Could be intentional omission of newer commands — but no comment or documentation indicates this. The missing commands include core workflow commands (message, issue, worktree) that users would expect tab completion for.
- **Issue:** #0049

#### 2. CompletionProvider.Roles array is stale — 3 roles missing
- **Category:** bug
- **Severity:** medium
- **Type:** tested
- **Evidence:** `Services/CompletionProvider.cs:14-15` lists 6 roles. 9 base roles exist in `dydo/_system/roles/` (9 `.role.json` files). Missing: `orchestrator`, `inquisitor`, `judge`. This cascades to `OptionValueHandlers["--role"]` (line 51) and `ArgCompletions[("agent","role")]` (line 38), which both reference the same stale array. `dydo agent role i<TAB>` won't suggest inquisitor. Confirmed by test: `CompletionProviderTests.Roles_MissingRole` (3 Theory cases pass).
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/CompletionProvider.cs (lines 14-15), dydo/_system/roles/ directory (9 .role.json files listed)
- **Independent verification:** Listed all 9 role JSON files: co-thinker, code-writer, docs-writer, inquisitor, judge, orchestrator, planner, reviewer, test-writer. CompletionProvider.Roles has 6. Missing: orchestrator, inquisitor, judge — exactly as reported. Verified the cascade to OptionValueHandlers["--role"] (line 51) and ArgCompletions[("agent","role")] (line 38), which both reference the same Roles array.
- **Alternative explanations considered:** Could be intentional to hide oversight roles from completion — but these roles are documented, available in the role system, and agents need to set them via `dydo agent role`.
- **Issue:** #0049

#### 3. CompletionProvider.SubcommandLists is stale — 7 missing entries, 2 incomplete
- **Category:** bug
- **Severity:** high
- **Type:** tested
- **Evidence:** `Services/CompletionProvider.cs:22-32` has entries for 8 commands. Missing entries: `issue` (create/list/resolve — IssueCommand.cs:12-14), `inquisition` (coverage — InquisitionCommand.cs:18), `roles` (reset/list/create — RolesCommand.cs:15-17), `template` (update — TemplateCommand.cs:36), `worktree` (cleanup/merge/init-settings/prune — WorktreeCommand.cs:39-70), `queue` (create/show/cancel/clear — QueueCommand.cs:13-16), `watchdog` (start/stop/run — WatchdogCommand.cs:40-42). Existing entries incomplete: `agent` missing "tree" (AgentCommand.cs:20), `task` missing "compact" (TaskCommand.cs:20). Confirmed by test: `CompletionProviderTests.GetSubcommandCompletions_CommandNotInSubcommandLists` (11 Theory cases pass).
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/CompletionProvider.cs (lines 22-32), Program.cs (lines 98-135 for help text listing subcommands)
- **Independent verification:** Cross-referenced SubcommandLists entries against Program.cs help text. Confirmed missing entries for issue (create/list/resolve at help lines 121-123), inquisition (coverage at line 126), roles (list/create/reset at lines 99-102), template (update at line 108), worktree (cleanup at line 135), queue (create/show/cancel/clear at lines 129-132). Watchdog subcommands confirmed via report citation. Agent "tree" confirmed at help line 66 but absent from SubcommandLists["agent"]. Task "compact" confirmed at help line 116 but absent from SubcommandLists["task"].
- **Alternative explanations considered:** Could be that newer commands were added without updating CompletionProvider — this is the most likely explanation and represents a systemic maintenance gap.
- **Issue:** #0049

#### 4. CompletionProvider.OptionValueHandlers missing --subject handler
- **Category:** missing-feature
- **Severity:** low
- **Type:** tested
- **Evidence:** `Services/CompletionProvider.cs:50-58` defines 6 option handlers. The `--subject` option on the message command (MessageCommand.cs) should complete with task names (same source as `--task`), but has no entry. Confirmed by test: `CompletionProviderTests.OptionValueHandlers_SubjectOption_ReturnsNull`.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/CompletionProvider.cs (lines 50-58), Commands/MessageCommand.cs (line 27)
- **Independent verification:** Grepped MessageCommand.cs for `--subject` — confirmed the option exists at line 27. OptionValueHandlers has 6 entries (--role, --task, --area, --status, --action, --to) but no --subject. The --subject option would logically complete with task names (same source as --task).
- **Alternative explanations considered:** Could be intentionally omitted if --subject completions aren't useful — but --subject takes task names and --task already has a handler for this exact data source, so the omission is inconsistent.
- **Issue:** #0049

#### 5. HelpCommandTests tests a copied help text, not the actual Program.cs implementation
- **Category:** antipattern
- **Severity:** high
- **Type:** obvious
- **Evidence:** `DynaDocs.Tests/Commands/HelpCommandTests.cs:22-100` contains a private `PrintHelp()` method that is a hardcoded copy of the help handler from Program.cs:44-152. `CaptureHelpOutput()` (line 16-19) calls this local copy, not the actual help command. All 9 test methods validate against this copy. The tests can never detect regressions in Program.cs — if the production help text changes, the tests continue passing against their stale copy.
- **Judge ruling:** CONFIRMED
- **Files examined:** DynaDocs.Tests/Commands/HelpCommandTests.cs (lines 16-100), Program.cs (lines 44-152)
- **Independent verification:** Read both files end-to-end. HelpCommandTests.PrintHelp() (lines 22-100) is a standalone method that reproduces help text locally. CaptureHelpOutput() (line 17) calls this local method. No reference to Program.cs or the actual help command handler. All 9 test methods validate against this local copy. This is a structural antipattern — the tests are unfalsifiable against the production code.
- **Alternative explanations considered:** Could be a deliberate design choice for test isolation — but testing a copy of the code rather than the code itself defeats the purpose of regression testing entirely. No comment or documentation justifies this approach.
- **Issue:** #0050

#### 6. HelpCommandTests copy is massively stale
- **Category:** bug
- **Severity:** medium
- **Type:** obvious
- **Evidence:** The `PrintHelp()` copy in HelpCommandTests.cs is missing entire sections that exist in Program.cs: `agent tree` (Program.cs:66), Role Commands section (Program.cs:98-103), Validation Commands section (Program.cs:105-106), `task compact` (Program.cs:116), Issue Commands section (Program.cs:120-123), Inquisition Commands section (Program.cs:125-126), Queue Commands section (Program.cs:128-133), Worktree Commands section (Program.cs:134-135). This staleness is invisible because finding #5 means the test can't catch it.
- **Judge ruling:** CONFIRMED
- **Files examined:** DynaDocs.Tests/Commands/HelpCommandTests.cs (lines 22-100 for PrintHelp copy, lines 44-47 for agent section), Program.cs (lines 60-68 for actual agent section)
- **Independent verification:** Compared the PrintHelp copy's agent section (lines 37-47) with Program.cs (lines 60-68). The copy is missing "agent tree" (Program.cs:66), Role Commands section (Program.cs:98-103), Validation Commands section (Program.cs:104-106), task compact (Program.cs:116), Issue Commands (Program.cs:120-123), Inquisition Commands (Program.cs:125-126), Queue Commands (Program.cs:128-133), Worktree Commands (Program.cs:134-135). This staleness is a direct consequence of Finding #5.
- **Alternative explanations considered:** None — this is straightforward drift between the copy and the source. The copy was never updated when new commands were added to Program.cs.
- **Issue:** #0050

#### 7. HelpCommandTests.Help_ListsAllAgentSubcommands missing 'agent tree' assertion
- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:** `HelpCommandTests.cs:128-142` asserts for 10 agent subcommands but omits `Assert.Contains("agent tree", output)`. AgentCommand.cs:20 confirms the `tree` subcommand exists. Program.cs:66 lists it in the actual help text.
- **Judge ruling:** CONFIRMED
- **Files examined:** DynaDocs.Tests/Commands/HelpCommandTests.cs (lines 128-142)
- **Independent verification:** Lines 128-142 assert 10 agent subcommands: claim, release, status, list, role, new, rename, remove, reassign, clean. No assertion for "agent tree". Program.cs:66 confirms `agent tree` exists. However, this gap is moot due to Finding #5 — even if the assertion were added, it would pass or fail against the local PrintHelp copy, not the actual command.
- **Alternative explanations considered:** Could be intentional if "agent tree" was added after the test was written — but that's exactly the kind of regression the test should catch. The test name says "ListsAllAgentSubcommands" but doesn't.
- **Issue:** #0050

#### 8. CommandSmokeTests.RootCommand_CanBeBuilt missing WorktreeCommand
- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:** `DynaDocs.Tests/Commands/CommandSmokeTests.cs:60-87` builds a root command with 26 subcommands but omits `WorktreeCommand.Create()`. `WorktreeCommand.Create` IS present in the `AllCommands_CanBeInstantiated` test (line 43) and is registered in Program.cs:31. The count assertion `Assert.True(rootCommand.Subcommands.Count >= 26)` (line 91) is loose enough to not catch this.
- **Judge ruling:** CONFIRMED
- **Files examined:** DynaDocs.Tests/Commands/CommandSmokeTests.cs (lines 55-92), Program.cs (line 31)
- **Independent verification:** Read CommandSmokeTests.cs:60-88 — the RootCommand_CanBeBuilt method lists 26 commands but omits WorktreeCommand.Create(). Confirmed WorktreeCommand IS present in AllCommands_CanBeInstantiated (line 43) and registered in Program.cs:31. The assertion `Assert.True(rootCommand.Subcommands.Count >= 26)` at line 91 passes with 26 commands, masking the omission.
- **Alternative explanations considered:** Could be an intentional exclusion if WorktreeCommand had initialization side effects — but it's in the AllCommands test, so instantiation is safe. Simple omission.
- **Issue:** #0051

#### 9. Smoke test .txt files committed to Commands/ directory
- **Category:** dead-code
- **Severity:** low
- **Type:** obvious
- **Evidence:** 12 text files in `Commands/`: smoke-comp-a.txt, smoke-comp-b.txt, smoke-final-a.txt through smoke-final5-a.txt, smoke-test-v15.txt. These are one-line marker files (e.g., "Comprehensive smoke test - agent A"). Committed to git (e.g., commit 5010a08). Not in .gitignore. Test artifacts that don't belong in the source directory alongside production .cs files. They also inflate the inquisition coverage heatmap (appearing as "gap" entries under Commands/).
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/ directory (listed 12 .txt files via ls), git ls-files Commands/*.txt (confirmed all 12 tracked)
- **Independent verification:** Ran `git -C DynaDocs ls-files Commands/*.txt` — all 12 files are tracked: smoke-comp-a.txt, smoke-comp-b.txt, smoke-final-a.txt through smoke-final5-a.txt, smoke-test-v15.txt. These are one-line marker files from testing. They sit alongside production .cs files in Commands/.
- **Alternative explanations considered:** Could be intentional test fixtures — but they have no references from any test code and their naming suggests ad-hoc smoke testing artifacts. Not in .gitignore.
- **Issue:** #0052

#### 10. dydo-commands.md: validate command misplaced under "Role Commands"
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `dydo/reference/dydo-commands.md:682` places `### dydo validate` under the `## Role Commands` section (line 645). But Program.cs help text (lines 104-106) correctly categorizes it under its own "Validation Commands:" section. The validate command validates the full system (config, roles, templates, agent state), not just roles. It belongs in its own section or under "Utility Commands."
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/reference/dydo-commands.md (lines 645-694), Program.cs (lines 98-106)
- **Independent verification:** Read dydo-commands.md — `### dydo validate` appears at line 682, nested under `## Role Commands` (line 645). Program.cs help text has "Validation Commands:" as its own section (lines 104-106), separate from "Role Commands:" (lines 98-103). The validate command validates config, roles, templates, and agent state — it's a system-wide validation tool, not a role command.
- **Alternative explanations considered:** Could be considered "close enough" since validate checks role files among other things — but its scope is much broader and Program.cs explicitly separates it.
- **Issue:** #0053

#### 11. dydo-commands.md Role Permissions table incomplete for judge
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `dydo/reference/dydo-commands.md:765` shows the judge role's "Can Edit" column as `dydo/agents/{agent}/**`, `dydo/project/issues/**`. But the actual role definition at `dydo/_system/roles/judge.role.json:8` includes `dydo/project/inquisitions/**` as a writable path (required for marking rulings on findings). The docs table is missing this path, which could mislead users or agents about what the judge can edit.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/reference/dydo-commands.md (line 767), dydo/_system/roles/judge.role.json (lines 5-8)
- **Independent verification:** Read judge.role.json — writablePaths includes `dydo/agents/{self}/**`, `dydo/project/issues/**`, `dydo/project/inquisitions/**`. The doc table at line 767 shows only `dydo/agents/{agent}/**`, `dydo/project/issues/**` — missing `dydo/project/inquisitions/**`. This is a critical omission since marking rulings on inquisition reports is the judge's primary function.
- **Alternative explanations considered:** Could be a documentation lag from when the judge role was expanded — but the current role definition clearly includes inquisitions as writable.
- **Issue:** #0053

#### 12. InitCommand prints unquoted humanName in shell example
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Commands/InitCommand.cs:171` outputs `export DYDO_HUMAN={humanName}` without quoting. If a user's name contains spaces or shell metacharacters (e.g., "Mary Jane"), copy-pasting this line would cause a shell error or unexpected behavior. Should be `export DYDO_HUMAN="{humanName}"`. Low risk since names are lowercased and trimmed (line 288), but not character-validated.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/InitCommand.cs (lines 160-188, 280-289)
- **Independent verification:** Read line 172: `$"  1. Set environment variable: export DYDO_HUMAN={humanName}"` — no quotes around the interpolated value. Read line 288: `return name.Trim().ToLowerInvariant();` — name is lowercased and trimmed, but there's no validation against spaces or shell metacharacters. A name like "mary jane" would produce `export DYDO_HUMAN=mary jane`, which would be a shell error.
- **Alternative explanations considered:** Practical risk is low since most users enter single-word names and the system lowercases them. But the code doesn't enforce single-word names, so the output is technically broken for multi-word inputs.
- **Issue:** #0054

### Hypotheses Not Reproduced
- **InitCommand security vulnerabilities** — Security audit (Emma) found no exploitable vulnerabilities. File writes, hook configuration, and input handling are implemented safely. TOCTOU in ScaffoldProject is theoretical only (one-time interactive command).
- **CompletionProvider out-of-bounds crash** — Test (Frank) confirmed positions beyond array bounds are handled safely with no exceptions.
- **InitCommand case sensitivity** — `IsValidIntegration` (InitCommand.cs:310) normalizes to lowercase internally, so `Claude` and `CLAUDE` both work correctly.

### Workflow Verification
The brief asked to verify the full inquisition workflow. Results:
- **Claim/role/must-reads:** Smooth, no unexpected blocks.
- **Guard enforcement:** Working correctly — blocked reads before role was set, allowed after.
- **Scout dispatch with --wait:** Functional — 4 scouts dispatched, all completed and reported back.
- **Worktree isolation:** Working — files correctly isolated, junctions share agent registry.
- **Judge CAN write rulings:** Confirmed — `judge.role.json:8` includes `dydo/project/inquisitions/**`.
- **No release deadlocks:** Inquisitor has `requires-dispatch` constraint (inquisitor.role.json:18-28) enforcing judge dispatch before release. Constraint is `onlyWhenDispatched: true`, so direct human invocations aren't blocked.
- **No permission prompts during normal workflow.**

### Confidence: high
Core command files (Program.cs, InitCommand, ValidateCommand, CompletionProvider, CompletionsCommand, CompleteCommand) were read thoroughly. All prior-art tests were reviewed. 4 scouts provided independent verification with line-number evidence. Edge cases were tested and confirmed with actual test execution (3589/3589 tests passing). The CompletionProvider staleness is systemic and well-evidenced. Help text and doc consistency were cross-checked against actual command registrations. Security was reviewed with no findings. Remaining uncovered: deeper audit of secondary commands (Audit, Graph, Fix, Index) which were out of scope.
