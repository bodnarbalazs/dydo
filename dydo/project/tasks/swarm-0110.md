---
area: general
name: swarm-0110
status: pending
created: 2026-07-12T15:41:37.4369021Z
assigned: Emma
needs-human: false
---

# Task: swarm-0110

CODEX swarm fix ROUND 2 — issue 0110. Your round-1 MECHANICS are SOUND and VERIFIED: a Claude reviewer traced every branch and confirmed the release gate fails CLOSED (git failure/timeout/unparseable/nonzero-exit/unparseable-stdout all BLOCK — no fail-open path), scoping is correct (only worktree + requires-commit role; non-worktree/other roles untouched), cleanup refuses on dirty/ahead/git-error BEFORE removing markers, the release path never auto-forces `--force`, repeated cleanup is a safe no-op (#0108 closed), and take-role accepts the new constraint without gating. KEEP all that logic EXACTLY — do NOT change the fail-closed direction, the scoping, or the cleanup ordering. Three fixes needed. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

FIX 1 — TESTS (the core gap; ALSO fixes a failing coverage gate). The combined coverage gate FAILED on `Services/RoleDefinitionService.cs` (CRAP 32.0, need <=30) — your new requires-commit validation branch is undercovered. Add these tests (they pin the safety-critical directions AND raise coverage to clear CRAP):
  a. FAIL-CLOSED release gate — THE most important (this is issue #0110's central data-loss concern, and it currently has NO test): using `ReleaseGitCaptureOverride`, assert that a git FAILURE `(128, "fatal: bad revision")` AND an unparseable `(0, "garbage")` BOTH BLOCK release. Without this pin, a later "just unblock the user" refactor (`if (ExitCode != 0) return false`) would silently reintroduce 0110's silent data loss with every existing test still green.
  b. Cleanup refusal branches (only the dirty branch is tested now): clean status + rev-list `(0, "2\n")` (ahead-of-base) => REFUSED mentioning unmerged commits; git-failure `(1, ...)` status => REFUSED; both SUCCEED with `--force`. Use the cleanup git-capture override the same way WorktreeCommandTests already does.
  c. Explicit `requires-commit` acceptance tests for RoleConstraintEvaluator + RoleDefinitionService, MIRRORING the existing `requires-dispatch` analogues at `DynaDocs.Tests/Services/RoleConstraintEvaluatorTests.cs:490` and `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs:435` (coverage is asymmetric right now). These directly raise RoleDefinitionService coverage → clear the CRAP gate.

FIX 2 — ERROR MESSAGE (the gate is HIGH-severity; the message must not mislead). `RequiresCommitBeforeRelease` emits ONE message for BOTH "0 ahead" and "git check failed" — it's garbled ("uncommitted or uncommitted-and-unpushed"; "unpushed" is irrelevant to worktree branches) and FACTUALLY FALSE in the git-failure branch (says "not ahead" when the check simply couldn't run). Split into two distinct messages, mirroring how `CheckCleanupSafety` distinguishes "has pending changes" from "could not check":
  (i) 0-ahead (git succeeded, aheadCount == 0): clean actionable text — "You have uncommitted work in {worktreePath} (branch is not ahead of {baseBranch}). Run: git add -A && git commit -m '<message>' before releasing."
  (ii) git-failure / nonzero-exit / unparseable: "Could not verify commits in {worktreePath} (git check failed). Resolve the worktree state before releasing." (still fail-CLOSED — both still block; only the wording differs.)

FIX 3 — DEAD CONTRACT DATA. The `requires-commit` constraint's declared `message` (emitted by your round-1 WriteBaseRoleDefinitions in RoleDefinitionService) is never read (the code hardcodes its error), so a project customizing that message silently changes nothing. Wire `constraint.Message` into the 0-ahead branch (FIX 2 case i) as the message, falling back to the hardcoded text when the constraint message is unset/empty. (Leave the git-failure message hardcoded.) Look up the requires-commit constraint on the role the same way the gate already checks `role.Constraints.Any(c => c.Type == "requires-commit")`. Keep your round-1 WriteBaseRoleDefinitions change (emitting requires-commit WITH its message) so the regenerated role file carries it.

CRITICAL — DO NOT TOUCH the on-disk `dydo/_system/roles/code-writer.role.json`. It has been temporarily reverted to its pre-0110 state because a new constraint type in that runtime-read file BRICKS the currently-installed dydo binary (it rejects unknown constraint types), which disabled all code-writer dispatch. Adele re-adds the constraint at LAND time, AFTER the new binary (which accepts requires-commit) is rebuilt+installed. Your tests must NOT depend on the on-disk role file — construct role definitions with the requires-commit constraint IN-CODE (as the RoleConstraintEvaluatorTests:490 / RoleDefinitionServiceTests:435 analogues do), and build/run via `dotnet test` against source (where your round-1 .cs already supports the type). If any existing test reads the live on-disk role file and now fails because the constraint is absent there, tell Adele in your report instead of re-adding it to the JSON.

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` passes; run `dotnet test` filtered to AgentRegistryTests + WorktreeCommandTests + RoleConstraintEvaluatorTests + RoleDefinitionServiceTests — all green including your new tests. Do NOT run the python coverage gate (0282) — the reviewer re-runs it and confirms RoleDefinitionService CRAP is back <=30.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0110-r2` with: the tests added (esp. the fail-closed override cases + the RoleConstraintEvaluator/RoleDefinitionService requires-commit tests), the message split, how you wired constraint.Message, build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch ONLY `Services/AgentRegistry.cs` (message split + wire constraint.Message inside RequiresCommitBeforeRelease — do NOT alter the fail-closed logic, scoping, or rev-list test), `DynaDocs.Tests/Services/AgentRegistryTests.cs`, `DynaDocs.Tests/Services/RoleConstraintEvaluatorTests.cs`, `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`, `DynaDocs.Tests/Commands/WorktreeCommandTests.cs`. Your round-1 changes to `Services/RoleConstraintEvaluator.cs` and `Services/RoleDefinitionService.cs` (accepting the requires-commit type + emitting it from WriteBaseRoleDefinitions) STAY — do not revert them. Do NOT touch the on-disk `dydo/_system/roles/code-writer.role.json` (see CRITICAL note above). Do NOT touch other swarm agents' files (GuardCommand.cs, Sync/, Rules/, Utils/RuleSkipPaths.cs, gap_check.py). Do NOT weaken the fail-closed direction. (Out-of-scope residuals the reviewer noted — prune/WorkspaceCleaner dirty-check, legacy-marker skip, 0-ahead no-op-task wedge, the `dydo roles reset` migration note — are follow-ups Adele will file; do NOT address them here.)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)