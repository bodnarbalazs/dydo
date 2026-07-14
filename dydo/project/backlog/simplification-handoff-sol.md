---
area: project
type: context
name: simplification-handoff-sol
status: open
created: 2026-07-14
created-by: Adele
---

# MASTER HANDOFF — dydo 2.1.0 Simplification Campaign (for Codex 5.6 Sol)

You are orchestrating a large, deletion-heavy refactor of **dydo** (this repo, `.NET 10`, Windows). A Claude reviewer will review your work **tomorrow**, slice by slice — so your job tonight is to execute correctly AND leave a clean, reviewable trail. Read this whole doc first, then the two source-of-truth records it points to.

## 0. Read these first (they are the decision + the plan)
1. `dydo/project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md` — WHY (the pivot + resolved decisions).
2. `dydo/project/backlog/simplification-campaign-plan.md` — HOW (the finalized cut order + Phase-0 map results). **This is your primary plan; this handoff is the orchestration wrapper around it.**

## 1. The mission in one line
Transform dydo from an agent-orchestration framework into lean **"dynamic documentation"**: **compiler + knowledge + PM + nudges.** Agent coordination/running is ceded to the platforms (Claude Code, Codex). You are DELETING the orchestration/identity/claim/wake/dispatch/messaging machinery and keeping the vendor-neutral, durable core.

## 2. Preconditions — DO THIS BEFORE ANY DELETION
- **The working tree has uncommitted, already-reviewed-green work** (a task-approval reform, Notion chunking, a codex-shell guard fix, plus new decision/issue records). Run `git status` — you'll see many modified + untracked files. **Commit ALL of it first as a single baseline checkpoint** (message e.g. `checkpoint: reviewed pre-simplification work (reform + chunking + guard-fix + records)`), so your deletion diff is cleanly separable for tomorrow's review. **`git add -A` including untracked `Commands/TaskDoneHandler.cs`.** Do NOT delete, revert, or clobber any of this work.
- **The guard is intentionally DISARMED** (the human renamed `dydo guard` → `dydo notguard`). Your file operations won't be blocked, and codex hook calls to `dydo guard` may error harmlessly — that is expected. **Do NOT re-arm the guard.** Git is your safety net now.
- **Verify a green baseline** before cutting: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` (0 errors) and the test suite green (see §5). If the baseline is red, STOP and report — do not build deletions on a red tree.

## 3. The fence — what survives, what dies (do NOT over-cut)

**KEEP (never delete):**
- **Compiler:** `Commands/SyncCommand.cs`, `Services/TemplateGenerator.cs`, `Services/RoleDefinitionService.cs`, role/skill/agent/mode/template generation, `.claude`/`.codex`/`.agents` emission. After ANY change, run `dydo sync` and confirm it still compiles roles correctly — this is the crown jewel.
- **Knowledge:** everything under `dydo/` (decisions, issues, docs, guides, reference).
- **PM:** `Commands/TaskCommand.cs`, `Commands/TaskCreateHandler.cs`, `Commands/TaskDoneHandler.cs`, `Commands/TaskReviewHandler.cs`, `Commands/IssueCommand.cs`, `Commands/ReviewCommand.cs`, changelog/hub handlers, and the records. (The `task done`/archive lifecycle just landed — keep it.)
- **Guard = nudges + off-limits ONLY:** keep the regex-pattern nudge engine and files-off-limits enforcement in `Commands/GuardCommand.cs`. Keep `Commands/CheckCommand.cs`, `Commands/FixCommand.cs`, `Commands/IndexCommand.cs`, `Commands/InitCommand.cs`, `Commands/GraphCommand.cs`, `Commands/ValidateCommand.cs`, `Commands/NotionCommand.cs`, `Commands/ModelCommand.cs`, `Commands/TemplateCommand.cs`, `Commands/RolesCommand.cs`, `Commands/CompletionsCommand.cs`, `Commands/CompleteCommand.cs`, `Commands/HelpCommand.cs`, `Commands/WorktreeCommand.cs` (de-orchestrated — see below).

**CUT (delete entirely):**
- Commands: `Whoami`, `Inbox`, `Message`, `Read`, `Wait`, `Dispatch`, `Agent` (claim/release/roster), `Workspace`, `Hand` (raise-hand-command), `GuardLift`.
- Services: `MessageService`, `InboxService`, `TerminalLauncher` + `WindowsTerminalLauncher` + `LinuxTerminalLauncher` + `MacTerminalLauncher`, `DispatchService`, `DispatchPreflight`, `AgentSessionManager`, and the claim/roster helpers (`AgentManagementHandlers`, `AgentListHandler`, `AgentLifecycleHandlers`, `AgentTreeHandler`, `AgentSelector`, `RecoveryClassifier`, `WorkspaceCleaner`).
- The wait/wake machinery incl. durable markers and the guard's "must keep a wait active" rule.
- The 26-agent named roster + agent state files.

**REWORK (do NOT delete — repurpose):**
- `Services/WatchdogService.cs` → a **Notion-sync daemon**: sync every ~15s; the CLI self-starts it on guard trigger; it also gives collaborator file-sync between commits. First strip its agent-lifecycle/auto-resume/TerminalLauncher guts (part of the campaign); the Notion-daemon rebuild can be a later slice — if you don't finish it, leave it as a clearly-marked stub and flag it, don't leave it half-referencing deleted code.

**CARVE (last, careful):**
- `Services/AgentRegistry.cs` — **276 refs across 40 files, load-bearing for KEEP code** (guard nudges, ReviewCommand, TaskCreate/TaskDone, IssueCreate, Validation). This is a CARVE, not a delete: keep the path/config/record bits KEEP-code needs; delete the claim/roster/identity/session bits. Do this LAST, after the CUT slices have removed most of its references. When in doubt about a method's owner, flag it for review rather than guess.

## 4. The method — serial surgical grind, leaves → branches → trunk
Nothing here is a clean leaf at the service layer, and **every cut ripples into the SHARED test project** (`DynaDocs.Tests/Integration/IntegrationTestBase.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`, `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`). So slices SERIALIZE on the test project — if you spawn your own worker threads, do NOT have two of them editing the shared test infra at once. Order:

1. **Command leaves** — for each CUT command: delete the command file + its dedicated handler(s) + its dedicated tests + remove its `rootCommand.Subcommands.Add(XCommand.Create())` line in `Program.cs` + remove its `HelpCommand` line + fix any shared-test-infra references (they invoke `dydo <cmd>` or the command class). When cutting `Wait`, also remove the guard's wait-enforcement rule. When cutting messaging, also remove `ReviewCommand`'s two `MessageService.DeliverInboxMessage(...)` notify calls (~lines 237, 253) and their now-unused locals.
2. **Orphaned services** — once no command references them, delete `MessageService`, `InboxService`, `TerminalLauncher`(+platform launchers), `DispatchService`, `DispatchPreflight`, `AgentSessionManager` + their tests. `TerminalLauncher` is ALSO used by the watchdog + `WorktreeCommand`; both must stop using it first (watchdog via its rework; `WorktreeCommand` by removing its agent-launch path).
3. **Trunk** — hollow the guard (strip must-reads/RBAC/claim-gate/session-binding; KEEP nudges + off-limits), finish the watchdog rework, then CARVE `AgentRegistry`.

**THE RATCHET (non-negotiable):** after EACH slice — build 0 errors, test suite green, THEN `git commit` that slice with a clear message (e.g. `cut: remove whoami command (identity display, DR-041)`). One slice per commit so tomorrow's review is slice-by-slice. A red build is stop-and-fix, never "continue." **Do not squash the whole campaign into one commit.**

## 5. Build / test / gate commands (Windows, .NET 10)
- Build: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore`
- Full suite (worktree-isolated runner — use THIS, not bare `dotnet test`): `python DynaDocs.Tests/coverage/run_tests.py`
- Coverage/tier gate (run at the end of a slice before committing): `python DynaDocs.Tests/coverage/gap_check.py --force-run` — every source module must have **CRAP ≤ 30**. NOTE: the CRAP formula = `maxMethodCC² · (1 − lineRate)³ + maxMethodCC`, and coverage is often <100%, so a method at CC 29 can still fail. **Target max method CC ≤ 28.** Deleting code usually IMPROVES this, but if you delete tests that covered a KEEP method, its coverage drops and its CRAP can rise — watch for that.
- Read the gate's actual RESULT lines; a trailing `| tail` can hide the real exit. Look for `Passed!`/`Failed!` and `All modules pass` vs `Gate FAILS`/`N modules fail`.

## 6. Gotchas that WILL bite if you skip them
- **Doc-consistency:** `CommandDocConsistencyTests` pins that every registered command appears in the docs. Removing a command mostly won't RED the gate (it's addition-driven), BUT it leaves stale `dydo <cmd>` references in `Commands/HelpCommand.cs`, `dydo/reference/dydo-commands.md` (+ `Templates/dydo-commands.template.md`), `dydo/reference/about-dynadocs.md` (+ `Templates/about-dynadocs.template.md` — these two must stay BYTE-IDENTICAL, a test checks it), and `README.md`. **Sweep those docs for each removed command** so you don't ship docs for commands that no longer exist.
- **`AgentRegistry.cs` also carries the reform's one-line change** (`status: pending`→`in-progress` in the task auto-create) — preserve it during the carve.
- **`GuardCommand.cs` carries the reform change** (removed `task approve|reject` from the human-only regex) AND the guard-fix (`shell_command|exec|local_shell|unified_exec` in `ShellTools`) — preserve both while stripping the identity layer.
- Line endings: some repo files are mixed LF/CRLF — edit surgically, don't whole-file-rewrite (git `autocrlf` will normalize but a whole-file flip is noisy in review).

## 7. Decisions already made — do NOT re-litigate
- Messaging/inbox: **CUT entirely** (no identity → no message delivery address; if it's ever needed, that's a future problem).
- Files are the source of truth; Notion is a projection (read-only view).
- Guard keeps nudges + off-limits; identity/RBAC/must-reads are cut.
- Compiler targets Claude + Codex only.

## 8. Flag for tomorrow's review (don't block on these)
- The exact `AgentRegistry` carve boundary (which methods are load-bearing for KEEP vs pure identity).
- Any deletion that forced a judgment call touching a KEEP file.
- The watchdog rework state (done vs stubbed).
- Anything you were unsure was CUT vs KEEP.
Leave a short `dydo/agents/<you>/handback-notes.md` (or a final commit message) listing what you completed, what you stubbed, and every judgment call, so the reviewer knows where to look.

## 9. Success criteria
- Baseline checkpoint committed first; deletion campaign in clean, one-slice-per-commit history.
- Build green + suite green + gate green after each committed slice.
- CUT list gone; KEEP fence intact; `dydo sync` still compiles roles; watchdog rework scoped or done.
- Stale command docs swept. Judgment calls flagged for review.

Good luck — the goal is a smaller, sharper dydo that does the vendor-neutral things well and lets the platforms run the agents. Cut confidently, verify relentlessly, commit cleanly.
