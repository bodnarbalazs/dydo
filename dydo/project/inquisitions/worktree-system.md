# Worktree System — Inquisition Report

## 2026-04-07 — Charlie

### Scope
- **Entry point:** Feature investigation — worktree system (WorktreeCommand, TerminalLaunchers, DispatchService worktree flow)
- **Files investigated:** Commands/WorktreeCommand.cs, Services/TerminalLauncher.cs, Services/WindowsTerminalLauncher.cs, Services/MacTerminalLauncher.cs, Services/LinuxTerminalLauncher.cs, Services/DispatchService.cs (worktree sections), Utils/FileLock.cs, Services/ProcessUtils.cs
- **Tests reviewed:** DynaDocs.Tests/Commands/WorktreeCommandTests.cs, DynaDocs.Tests/Commands/WorktreeCompatTests.cs, DynaDocs.Tests/Services/TerminalLauncherTests.cs, DynaDocs.Tests/Services/WorktreeCreationLockTests.cs, DynaDocs.Tests/Integration/WorktreeDispatchTests.cs, DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs
- **Docs cross-checked:** dydo/understand/architecture.md, dydo/understand/dispatch-and-messaging.md
- **Scouts dispatched:** 4 reviewers (Emma: WorktreeCommand quality, Grace: TerminalLaunchers quality, Henry: security, Iris: docs), 0 test-writers

### Findings

#### 1. Path traversal via `..` in task name allows arbitrary directory deletion
- **Category:** security
- **Severity:** high
- **Type:** obvious
- **Evidence:** `TerminalLauncher.GenerateWorktreeId()` at `TerminalLauncher.cs:35` validates task names with regex `^[a-zA-Z0-9_.\-]+$` which permits `.` and `..`. When used in `DispatchService.SetupWorktree()` at `DispatchService.cs:304`:
  ```csharp
  var worktreePath = Path.GetFullPath(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", worktreeId));
  ```
  A task name of `..` resolves `worktreePath` to `{projectRoot}/dydo/_system/.local/`, escaping the worktrees directory. `CreateGitWorktree` at `DispatchService.cs:620-621` then deletes this path recursively. Destroys all worktree state, audit data, and lock files. For child worktrees, `taskName=".."` with `parentWorktreeId="parent-task"` creates `worktreeId="parent-task/.."` resolving to the worktrees root.
- **Judge ruling:** [pending]

#### 2. Unvalidated worktreeId in cleanup CLI command allows arbitrary directory deletion
- **Category:** security
- **Severity:** high
- **Type:** obvious
- **Evidence:** `WorktreeCommand.ExecuteCleanup()` at `WorktreeCommand.cs:179` accepts `worktree-id` from CLI with zero input validation. The value passes to `ResolveWorktreePath()` which uses `Path.GetFullPath(Path.Combine(..., worktreeId))` at line 343. A crafted input like `../../../` resolves to ancestor directories. `RemoveZombieDirectory` at line 443 then calls `Directory.Delete(worktreePath, recursive: true)`. This is a command-line boundary, exactly where input validation is required per coding standards.
- **Judge ruling:** [pending]

#### 3. Worktree teardown sequence duplicated 3 times (Rule of Three violation)
- **Category:** antipattern
- **Severity:** high
- **Type:** obvious
- **Evidence:** The sequence `PreserveAuditFiles` + 4x `RemoveJunction` + `RemoveGitWorktree` + `DeleteWorktreeBranch` + `RemoveZombieDirectory` is copy-pasted in:
  - `ExecuteCleanup` at `WorktreeCommand.cs:211-218`
  - `FinalizeMerge` at `WorktreeCommand.cs:549-556`
  - `ExecutePrune` at `WorktreeCommand.cs:597-604`
  The 4 junction paths (`dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions`) are hardcoded identically in all 3. Adding a new junction requires editing 3 locations. The `FinalizeMerge` variant is slightly different (uses `-C mainRoot` for git commands), but the junction list is identical.
- **Judge ruling:** [pending]

#### 4. LinuxTerminalLauncher duplicates argument construction logic between GetArguments and TryLaunch
- **Category:** antipattern
- **Severity:** high
- **Type:** obvious
- **Evidence:** `LinuxTerminalLauncher.GetArguments()` at lines 10-41 and `TryLaunch()` at lines 43-92 both independently implement identical logic for: DYDO_AGENT export injection (L20 vs L55), DYDO_WINDOW export (L22-23 vs L57-58), worktree setup (L25-28 vs L60-63), inherited worktree cleanup (L29-35 vs L64-70), autoClose substitution (L37-38 vs L72-73). `TryLaunch` does NOT call `GetArguments` — it reimplements everything. Compare to `WindowsTerminalLauncher` where `Launch` correctly delegates to `GetArguments`. This means tests that verify argument construction via `GetLinuxArguments` test code that does not run in production.
- **Judge ruling:** [pending]

#### 5. MacTerminalLauncher duplicates argument construction between GetArguments and Launch
- **Category:** antipattern
- **Severity:** high
- **Type:** obvious
- **Evidence:** `MacTerminalLauncher.GetArguments()` at lines 11-33 and `Launch()` at lines 35-86 both independently build `cdPrefix`, `agentExport`, `windowExport`, `wtSetup`, `wtCleanup`, `shellCommand`, and `postCheck`. `Launch` does NOT call `GetArguments`. Same duplication pattern as LinuxTerminalLauncher.
- **Judge ruling:** [pending]

#### 6. Test coverage blind spot — tests verify GetArguments, production runs TryLaunch/Launch
- **Category:** missing-test
- **Severity:** high
- **Type:** obvious
- **Evidence:** Due to findings #4 and #5, tests in `TerminalLauncherTests.cs` that verify argument construction via `GetLinuxArguments` (e.g., lines 1460-1510, 1515-1520) and `GetMacArguments` (lines 1523-1551) are testing code paths that are NOT exercised in production. A bug in `TryLaunch`'s or `Launch`'s copy of the logic would go undetected by these tests.
- **Judge ruling:** [pending]

#### 7. RunProcessWithExitCode masks failures when only RunProcessOverride is set
- **Category:** bug
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `WorktreeCommand.RunProcessWithExitCode()` at `WorktreeCommand.cs:379-382`: when only `RunProcessOverride` is set (not `RunProcessWithExitCodeOverride`), it calls the void override and returns hardcoded `0`, silently masking failures. This means tests that only set the void override can never observe non-zero exit codes from `ExecuteMerge`.
- **Judge ruling:** [pending]

#### 8. Missing `--` separator in git commands enables flag injection
- **Category:** security
- **Severity:** medium
- **Type:** obvious
- **Evidence:** In `WorktreeCommand.FinalizeMerge()` at line 527: `RunProcessWithExitCode("git", $"-C \"{mainRoot}\" merge {mergeSource} --no-edit")` and line 563: `RunProcess("git", $"-C \"{mainRoot}\" branch -D {mergeSource}")` — `mergeSource` is not preceded by `--`. Current mitigation: `mergeSource` always starts with `worktree/` (not `-`), but defense-in-depth requires `--` separator. Also applies to `DeleteWorktreeBranch` at line 457.
- **Judge ruling:** [pending]

#### 9. Inconsistent git `-C` usage between cleanup paths
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `DeleteWorktreeBranch` at `WorktreeCommand.cs:457` runs `git branch -D` from CWD (no `-C` flag). `FinalizeMerge` at line 563 runs the same operation with `-C mainRoot`. During cleanup, CWD depends on the terminal script having correctly set the working directory. Works in practice but is fragile and inconsistent.
- **Judge ruling:** [pending]

#### 10. Near-duplicate process runner methods
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `WorktreeCommand.RunProcess()` at lines 349-372 and `RunProcessWithExitCode()` at lines 374-402 share identical `ProcessStartInfo` setup but differ only in return value and fallback logic. `DispatchService.RunGitForWorktree()` at lines 630-648 is a third variant with no timeout (unlike the 30s timeout in the WorktreeCommand versions).
- **Judge ruling:** [pending]

#### 11. Near-duplicate CountWorktreeReferences vs CountLiveWorktreeReferences
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `CountWorktreeReferences` at `WorktreeCommand.cs:281-300` and `CountLiveWorktreeReferences` at lines 615-628 differ only in whether `.worktree-hold` is included. Should be one method with a parameter.
- **Judge ruling:** [pending]

#### 12. Dead code: TerminalLauncher.MacTerminals array
- **Category:** dead-code
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `TerminalLauncher.MacTerminals` at `TerminalLauncher.cs:67-71` is defined but never referenced in production code or tests. `MacTerminalLauncher.Launch` uses its own hardcoded `osascript` logic. This is a vestigial artifact from when Mac terminals were handled like Linux terminals.
- **Judge ruling:** [pending]

#### 13. Test-only pass-through methods on TerminalLauncher
- **Category:** dead-code
- **Severity:** low
- **Type:** obvious
- **Evidence:** `TerminalLauncher.GetWindowsArguments`, `GetLinuxArguments`, `GetMacArguments`, `GetITermTabScript`, `GetITermWindowScript` at `TerminalLauncher.cs:129-142` are static pass-throughs to platform-specific classes. They have zero production callers — only test callers. These exist because tests were written against `TerminalLauncher.*` before the platform classes were extracted.
- **Judge ruling:** [pending]

#### 14. Duplicated BashPostClaudeCheck method
- **Category:** antipattern
- **Severity:** low
- **Type:** obvious
- **Evidence:** Identical `BashPostClaudeCheck` method defined in `LinuxTerminalLauncher.cs:7-8` and `MacTerminalLauncher.cs:121-122`. Both generate the same shell snippet.
- **Judge ruling:** [pending]

#### 15. Unescaped agentName in PowerShell env var assignment
- **Category:** security
- **Severity:** low
- **Type:** obvious
- **Evidence:** `WindowsTerminalLauncher.GetArguments()` at line 19: `$env:DYDO_AGENT='{agentName}'` — `agentName` is inside single quotes but not escaped (`'` → `''`). The `prompt` on line 13 IS properly escaped. Inconsistent. Same applies to `windowName` at lines 21-22. Impact is low since agent names are directory names and windowName is a GUID, but the inconsistency with prompt escaping is a code smell.
- **Judge ruling:** [pending]

#### 16. RunGitForWorktree has no timeout
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `DispatchService.RunGitForWorktree()` at line 646: `proc?.WaitForExit()` with no timeout, while `WorktreeCommand.RunProcess` uses a 30-second timeout. `git worktree add` could hang indefinitely on a corrupted git repository.
- **Judge ruling:** [pending]

#### 17. Documentation significantly lags implementation
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `architecture.md:82-92` and `dispatch-and-messaging.md` contain multiple inaccuracies:
  - Both claim child dispatches "inherit the same worktree" — code has 3 branches: child creation (--worktree), inheritance (default), merge dispatch
  - Both say only `dydo/agents/` is symlinked — code symlinks 4 directories (agents, roles, issues, inquisitions)
  - Both list 3 workspace markers — code uses 5 (.worktree-root and .worktree-hold are undocumented)
  - `dispatch-and-messaging.md` claims a nudge is emitted on worktree inheritance — no such nudge exists in code
  - `architecture.md:91` attributes orphan handling to `git worktree prune` — actual handler is `dydo worktree prune` with custom logic
  - Major undocumented subsystems: merge flow, prune command, init-settings command, audit preservation, branch name encoding, file lock serialization, child count blocking, queue+worktree interaction
  - ISSUE 0008 (changelog 2026-04-05) already flagged the inheritance/nudge discrepancy — docs still not updated
- **Judge ruling:** [pending]

### Hypotheses Not Reproduced
- Junction safety in `Directory.Delete(recursive: true)`: .NET 10 correctly removes junction reparse points without following them. The stale directory cleanup in `CreateGitWorktree:621` is safe.
- TOCTOU in `CreateGitWorktree`: The file lock serializes dydo processes, and external filesystem manipulation is out of scope for the trust model.
- PID reuse in `FileLock.TryRemoveStaleLock()`: Theoretical denial-of-service but narrow race window and consequence is only a timeout, not data loss.

### Confidence: high
The core worktree lifecycle (setup, cleanup, merge, prune) and all terminal launchers were reviewed thoroughly by the inquisitor and 4 specialized scouts. Security findings were independently verified against the source code. Documentation was cross-checked against actual behavior. The main gap is edge-case testing — no test-writer scouts were dispatched to write regression tests for the security findings (path traversal with `..`, unvalidated cleanup CLI input). These should be tested as part of the fix.
