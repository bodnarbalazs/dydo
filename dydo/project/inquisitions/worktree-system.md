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

---

## 2026-04-10 — Charlie

### Scope
- **Entry point:** Feature investigation — verify recent worktree fixes (junction-safe deletion, guard path normalization, GuardLiftService fallback, roles junction) and hunt for remaining gaps
- **Files investigated:** Commands/WorktreeCommand.cs, Services/DispatchService.cs (worktree sections), Services/TerminalLauncher.cs, Services/WindowsTerminalLauncher.cs, Services/LinuxTerminalLauncher.cs, Services/MacTerminalLauncher.cs, Services/GuardLiftService.cs, Utils/PathUtils.cs, Commands/GuardCommand.cs (worktree paths)
- **Tests reviewed:** DynaDocs.Tests/Commands/WorktreeCommandTests.cs (prune, cleanup, merge), DynaDocs.Tests/Services/PathUtilsTests.cs (NormalizeWorktreePath), DynaDocs.Tests/Services/TerminalLauncherTests.cs (ValidateWorktreeId)
- **Docs cross-checked:** dydo/understand/architecture.md, dydo/understand/dispatch-and-messaging.md, dydo/guides/how-to-merge-worktrees.md, dydo/guides/how-to-review-worktree-merges.md
- **Scouts dispatched:** 4 reviewers (Grace: junction safety, Henry: path normalization, Iris: terminal security, Jack: docs), 2 test-writers (Kate: nested worktree path, Leo: junction-safe deletion)
- **Prior inquisition status:** 17 findings from 2026-04-07; 14 verified fixed, 1 partially addressed, 2 documented as won't-fix

### Fix Verification

The four fixes named in the brief have been verified:

1. **Junction-safe Directory.Delete** — `DeleteDirectoryJunctionSafe` (WorktreeCommand.cs:442-458) recursively walks directories, checks `FileAttributes.ReparsePoint` on each subdirectory, and removes junctions via `RemoveJunction` before deleting files and the parent. Called from `CreateGitWorktree` (DispatchService.cs:624) for stale directory cleanup. Correct behavior verified.

2. **Guard worktree path normalization** — `NormalizeWorktreePath` fallback (PathUtils.cs:107-112) now uses the first segment after the worktree marker when `File.Exists` can't verify `dydo.json`. This handles the case where the guard's CWD differs from the worktree directory. Verified via existing test `NormalizeWorktreePath_NoDydoJson_FallsBackToFirstSegment`.

3. **GuardLiftService directory fallback** — `Lift` method (GuardLiftService.cs:33-34) now calls `Directory.CreateDirectory` before writing the marker, ensuring the agent directory exists in worktrees where the agents junction may be absent. Verified.

4. **Roles junction creation** — All 4 junctions (agents, roles, issues, inquisitions) are now created in terminal scripts and tracked in `JunctionSubpaths` (WorktreeCommand.cs:393-399). Junction list matches across C#, bash, and PowerShell scripts (Grace confirmed Q2 clean).

### Findings

#### 1. RemoveZombieDirectory uses Directory.Delete(recursive:true) instead of DeleteDirectoryJunctionSafe
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `RemoveZombieDirectory` at WorktreeCommand.cs:480 calls `Directory.Delete(worktreePath, recursive: true)`. `TeardownWorktree` removes the 4 known junctions first via `JunctionSubpaths`, but if any OTHER reparse point exists (user-created junction, future 5th junction added to terminal scripts but not to `JunctionSubpaths`), `Directory.Delete` would follow it. Meanwhile, `DeleteDirectoryJunctionSafe` at line 442 scans for ALL reparse points at any depth — the exact pattern needed here. The inconsistency means `CreateGitWorktree` is junction-safe but `TeardownWorktree` is not. Fix: replace `Directory.Delete(worktreePath, recursive: true)` at line 480 with `DeleteDirectoryJunctionSafe(worktreePath)`. (Grace independently confirmed: "defense-in-depth gap")
- **Judge ruling:** CONFIRMED
- **Files examined:** WorktreeCommand.cs (lines 393-413, 442-486), DispatchService.cs (line 624)
- **Independent verification:** Read `TeardownWorktree` (line 406-413) — it iterates `JunctionSubpaths` to remove 4 known junctions, then calls `RemoveZombieDirectory` which uses `Directory.Delete(recursive: true)`. Separately read `DeleteDirectoryJunctionSafe` (lines 442-458) — it scans for ALL reparse points via `File.GetAttributes` at any depth. The two methods implement fundamentally different safety models: known-list vs. scan-all.
- **Alternative explanations considered:** Could the known-list approach be sufficient? Only if the junction list never changes and no external reparse points exist. Since finding #4 documents the junction list being maintained in 5+ parallel locations, a mismatch is a realistic risk.
- **Issue:** #0084

#### 2. DeleteDirectoryJunctionSafe has unguarded File.GetAttributes
- **Category:** bug
- **Severity:** low
- **Type:** tested
- **Evidence:** `File.GetAttributes(subDir)` at WorktreeCommand.cs:448 can throw `UnauthorizedAccessException`, `PathTooLongException`, `IOException`, or `FileNotFoundException` (race condition). No try-catch wraps this call or any other filesystem operation in the method. An exception aborts the recursive deletion mid-traversal, leaving a partially-cleaned directory. Leo's tests demonstrate: (a) a locked file causes `IOException` to propagate, leaving root dir, sub-dir, locked file, and root-level file all intact; (b) `File.GetAttributes` throws `FileNotFoundException` when a directory disappears between `Directory.GetDirectories` and the `GetAttributes` call (TOCTOU race). Key test code:
  ```csharp
  // Locked file leaves partial state
  using var lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);
  Assert.Throws<IOException>(() => WorktreeCommand.DeleteDirectoryJunctionSafe(root));
  Assert.True(Directory.Exists(root)); // root still exists
  Assert.True(File.Exists(rootFile));  // root-level file still exists
  ```
  The caller in `DispatchService.CreateGitWorktree` (line 624) also has no try-catch. Grace independently confirmed via code review. Fix: wrap `File.GetAttributes` in try-catch; treat exceptions as potential junction (safe-side) or catch-and-skip with warning.
- **Judge ruling:** CONFIRMED
- **Files examined:** WorktreeCommand.cs (lines 442-458), WorktreeCommandTests.cs (lines 2276-2341)
- **Independent verification:** Read `DeleteDirectoryJunctionSafe` — confirmed no try-catch around `File.GetAttributes` (line 448), `File.Delete` (line 455), or `Directory.Delete` (line 457). Independently verified Leo's 4 tests: locked file test (line 2300) demonstrates IOException propagation leaving partial state; TOCTOU test (line 2330) demonstrates FileNotFoundException when directory vanishes between enumeration and attribute check. Both pass, confirming the structural lack of error handling.
- **Alternative explanations considered:** Could the caller be expected to handle exceptions? The `CreateGitWorktree` caller at DispatchService.cs:624 also has no try-catch. `RemoveZombieDirectory` (line 473) does have a try-catch — so the pattern of defensive error handling exists elsewhere in the same file but was omitted here.
- **Issue:** #0088

#### 3. Unescaped mainProjectRoot in inherited-worktree cleanup cd on Linux/Mac
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `LinuxTerminalLauncher.cs:24`: `$"cd '{mainProjectRoot}' && ..."` and `MacTerminalLauncher.cs:27`: `$"; cd '{mainProjectRoot}' && ..."` — `mainProjectRoot` is inside single quotes but NOT escaped for single quotes (`'` → `'\''`). Compare to `TerminalLauncher.WorktreeSetupScript` (line 97) and `WorktreeInheritedSetupScript` (line 125) where `mainProjectRoot.Replace("'", "'\\''")` IS used. Impact is low (requires single quote in project directory path, e.g., `/home/user/Tom's Projects/`). Windows `WindowsTerminalLauncher` correctly escapes at line 74 via `.Replace("'", "''")`. (Iris independently confirmed)
- **Judge ruling:** CONFIRMED
- **Files examined:** LinuxTerminalLauncher.cs (lines 6-31), MacTerminalLauncher.cs (lines 10-33), TerminalLauncher.cs (lines 91-130), WindowsTerminalLauncher.cs (lines 65-84)
- **Independent verification:** Read the inherited-worktree cleanup path in both Linux (line 23) and Mac (line 27) — `mainProjectRoot` is used raw inside single quotes. Then read `WorktreeSetupScript` (line 97) and `WorktreeInheritedSetupScript` (line 125) — both use `mainProjectRoot.Replace("'", "'\\''")`. The setup scripts in the same callers correctly delegate to these escaped methods, but the cleanup `cd` command is constructed inline without escaping. WindowsTerminalLauncher (line 74) uses `.Replace("'", "''")` for PowerShell escaping. The inconsistency is clear: setup is escaped, cleanup in the same code path is not.
- **Alternative explanations considered:** Could this be intentional because project roots never contain single quotes? No — there's no such constraint, and the fact that adjacent code does escape confirms this is an oversight, not a deliberate omission.
- **Issue:** #0089

#### 4. Junction list hardcoded in 5+ locations
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** The 4 junction paths (agents, roles, issues, inquisitions) are maintained in parallel across:
  - `WorktreeCommand.JunctionSubpaths` (WorktreeCommand.cs:393-399) — C# array for teardown
  - `TerminalLauncher.WorktreeSetupScript` (TerminalLauncher.cs:99-112) — bash for setup, 2 variants (absolute and relative)
  - `WindowsTerminalLauncher.GetArguments` (WindowsTerminalLauncher.cs:34-64) — PowerShell for setup, 2 variants
  Adding a 5th junction requires editing 5+ locations across 3 files. The `JunctionSubpaths` array was added to centralize teardown, but setup still hardcodes the list in generated shell scripts. A mismatch between setup and teardown would cause either missing junctions or, worse, `RemoveZombieDirectory` following unjunctioned paths.
- **Judge ruling:** CONFIRMED
- **Files examined:** WorktreeCommand.cs (lines 393-399), TerminalLauncher.cs (lines 91-113), WindowsTerminalLauncher.cs (lines 34-68)
- **Independent verification:** Counted junction references independently: `JunctionSubpaths` array (1 location, 4 paths), `WorktreeSetupScript` bash with absolute root variant (lines 98-102, 4 junction operations) and relative variant (lines 108-112, 4 junction operations), `WindowsTerminalLauncher.GetArguments` PowerShell (lines 34-64, 4 junction operations in 2 variants). Total: 5+ distinct code locations across 3 files and 3 languages. Adding a 5th junction requires synchronized edits to all locations — matches the Rule of Three threshold from coding standards.
- **Alternative explanations considered:** Could the duplication be unavoidable because shell scripts can't reference C# arrays? The junction list could be emitted from a single source (e.g., `JunctionSubpaths` generates the shell snippets), but currently each script maintains its own copy.
- **Issue:** #0090

#### 5. Worktree documentation still significantly lags implementation (0/6 prior items corrected)
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Prior inquisition finding #17 (2026-04-07) identified 6+ discrepancies. Jack's review (2026-04-10) found 0/6 corrected and 10 total discrepancies:
  - `architecture.md:87` claims 1 junction (`dydo/agents/`) — code creates 4 (WorktreeCommand.cs:393-399)
  - `architecture.md:88` lists 3 markers — code uses 5 (WorktreeCommand.cs:276 includes `.worktree-root`, `.worktree-hold`)
  - `architecture.md:90` and `dispatch-and-messaging.md:63` oversimplify child dispatch as "inherit" — code has 3 branches (DispatchService.cs:146-172)
  - `dispatch-and-messaging.md:63` claims "a nudge is emitted" on inheritance — no such nudge exists in code
  - `architecture.md:90` attributes orphan handling to `git worktree prune` — actual handler is `dydo worktree prune` (WorktreeCommand.cs:605+)
  - `how-to-merge-worktrees.md` omits child worktree blocking (WorktreeCommand.cs:532-536)
  - Both merge guides missing Related sections per writing-docs.md standards
  This was previously filed as ISSUE 0028. No progress since.
- **Judge ruling:** CONFIRMED
- **Files examined:** architecture.md (lines 81-91), dispatch-and-messaging.md (lines 56-63), WorktreeCommand.cs (lines 276, 393-399, 527-536, 605+)
- **Independent verification:** Read all 5 cited doc discrepancies against the code: (1) architecture.md:87 says "junction/symlink to `dydo/agents/`" — code at `JunctionSubpaths` (lines 393-399) creates 4 junctions (agents, roles, issues, inquisitions). (2) architecture.md:88 lists 3 markers — `RemoveWorktreeMarkers` (line 276) handles 5 (`.worktree`, `.worktree-path`, `.worktree-base`, `.worktree-hold`, `.worktree-root`). (3) dispatch-and-messaging.md:63 claims "a nudge is emitted" — grepped for nudge-related code near worktree inheritance in DispatchService; no such nudge exists. (4) architecture.md:90 says "`git worktree prune` handling orphans" — actual handler is `ExecutePrune` at WorktreeCommand.cs:605 with custom logic. All discrepancies verified independently.
- **Alternative explanations considered:** Could the docs describe a planned design? No — issue 0028 has been open since 2026-04-07 with no progress, and the discrepancies match the prior inquisition's finding #17, which also went unaddressed.
- **Issue:** #0028

#### 6. EnumerateLeafDirectories misidentifies worktree roots for non-empty orphaned directories
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `CollectLeafDirectories` at WorktreeCommand.cs:683-695 recursively finds only leaf directories (no subdirectories). For an orphaned worktree with a full project structure (e.g., `worktrees/my-task/Services/`, `worktrees/my-task/Commands/`), it reports deep project subdirectories as worktree IDs instead of the worktree root. `CountWorktreeReferences` returns 0 for these (no agent has `.worktree=my-task/Services`), so `TeardownWorktree` runs on each subdirectory individually. This progressively dismantles the orphan from the leaves inward across multiple prune runs rather than removing it in one pass. Not a data-loss bug (junctions are at the worktree root, not in subdirectories), but inefficient and prevents proper `git worktree remove` on the actual root. All prune tests use flat directories with no subdirectories, so this path is untested.
- **Judge ruling:** CONFIRMED
- **Files examined:** WorktreeCommand.cs (lines 676-695), WorktreeCommandTests.cs (prune test regions)
- **Independent verification:** Read `CollectLeafDirectories` — it recurses into every subdirectory and only yields entries where `subdirs.Length == 0`. For `worktrees/my-task/Commands/SubDir/` (a leaf), it reports worktree ID `my-task/Commands/SubDir`. `CountWorktreeReferences` (line 290) then checks if any agent has `.worktree=my-task/Commands/SubDir` — none will, so `TeardownWorktree` runs on that leaf path. The junction removal in `TeardownWorktree` tries `Path.Combine(leafPath, "dydo", "agents")` which doesn't exist at the leaf. The actual junctions at the worktree root (`my-task/dydo/agents`) are never addressed. Additionally, if junctions still exist at the worktree root, `Directory.GetDirectories` would follow them, potentially traversing into the main repo.
- **Alternative explanations considered:** Could this be a deliberate progressive-cleanup design? No — `RemoveGitWorktree` at line 460 needs the actual worktree root path to work correctly, and `git worktree remove` on a subdirectory would fail.
- **Issue:** #0091

#### 7. No tests for DeleteDirectoryJunctionSafe
- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:** `DeleteDirectoryJunctionSafe` (WorktreeCommand.cs:442-458) is a critical safety method that prevents junction-following during directory deletion. It has zero test coverage — no tests in the entire test suite (confirmed by grep). Key untested scenarios: (a) directory with reparse points at various depths, (b) empty directory, (c) non-existent directory, (d) `File.GetAttributes` throwing for inaccessible directories. This method was added in commit b799726 as a fix for junction safety.
- **Judge ruling:** CONFIRMED
- **Files examined:** WorktreeCommand.cs (lines 442-458), WorktreeCommandTests.cs (lines 2276-2341)
- **Independent verification:** Leo's tests (hyp-2 region, lines 2276-2341) added 4 tests: non-existent path (line 2279), nested tree happy path (line 2286), locked file IOException propagation (line 2300), and TOCTOU FileNotFoundException (line 2330). These cover scenarios (b), (c), and (d) from the finding. However, scenario (a) — directory with reparse points at various depths — remains untested. This is the core use case: the entire purpose of `DeleteDirectoryJunctionSafe` is to detect and remove junctions, yet no test verifies junction handling. The existing tests prove the method can delete regular directories and that it lacks error handling, but not that it correctly handles junctions.
- **Alternative explanations considered:** Could junction testing be impractical in unit tests? Creating junctions requires `mklink /J` on Windows and `ln -s` on Unix, which is platform-specific but feasible. The existing `RemoveJunction` method in WorktreeCommand.cs already implements the platform-specific logic.
- **Issue:** #0092

#### 8. Thin test coverage for ValidateWorktreeId
- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:** `ValidateWorktreeId` (TerminalLauncher.cs:48-64) has 4+ validation branches: empty string, backslash, path traversal (`.`/`..`), unsafe characters. Only 2 tests exist (`ValidateWorktreeId_EmptyString_Throws` at TerminalLauncherTests.cs:2383, `ValidateWorktreeId_UnsafeCharsInComponent_Throws` at line 2390). Missing tests: backslash rejection, `..` rejection via ValidateWorktreeId (only tested via GenerateWorktreeId at line 1577), valid hierarchical ID acceptance, `//` (empty component) rejection.
- **Judge ruling:** CONFIRMED
- **Files examined:** TerminalLauncher.cs (lines 48-64), TerminalLauncherTests.cs (lines 2383-2394)
- **Independent verification:** Read `ValidateWorktreeId` — 4 distinct validation branches: (1) empty/null check (line 50-51), (2) backslash rejection (line 54-55), (3) per-component `.`/`..` rejection (line 59), (4) unsafe character regex (line 61). Only 2 tests exist: `ValidateWorktreeId_EmptyString_Throws` and `ValidateWorktreeId_UnsafeCharsInComponent_Throws`. The `..` path is only tested indirectly via `GenerateWorktreeId` (line 1577 in tests), not via `ValidateWorktreeId` itself — these are different code paths. Backslash rejection has zero test coverage. Valid hierarchical ID acceptance (e.g., `parent/child`) is untested. Empty component from `//` input (which produces `""` in `Split('/')`) is untested.
- **Alternative explanations considered:** Are the `GenerateWorktreeId` tests sufficient proxy coverage? No — `GenerateWorktreeId` validates task name format (regex), while `ValidateWorktreeId` validates the composed ID with hierarchy. They share the `..` rejection but via different code paths.
- **Issue:** #0093

### Hypotheses Tested

- **Nested worktree path fallback (hyp-1):** CONFIRMED by Kate. `NormalizeWorktreePath` fallback (PathUtils.cs:109) uses `IndexOf('/')` for the first segment, which strips only the first worktree ID component for nested paths. Input `C:/project/dydo/_system/.local/worktrees/parent-task/child-task/some-file.cs` returns `C:/project/child-task/some-file.cs` instead of `C:/project/some-file.cs`. Test code (PathUtilsTests.cs:229-246):
  ```csharp
  var input = "C:/project/dydo/_system/.local/worktrees/parent-task/child-task/some-file.cs";
  var result = PathUtils.NormalizeWorktreePath(input);
  Assert.Equal("C:/project/child-task/some-file.cs", result);
  // Correct result would be "C:/project/some-file.cs"
  ```
  **Mitigated by design:** Henry's review confirmed this fails safe — the over-long path doesn't match role patterns, causing the guard to over-block (deny access) rather than under-block. Primary detection via `File.Exists(dydo.json)` handles normal cases correctly. The fallback only fires when filesystem verification is impossible. Severity: low.
  - **Judge ruling:** CONFIRMED
  - **Files examined:** PathUtils.cs (lines 104-121), PathUtilsTests.cs (lines 228-246)
  - **Independent verification:** Read `NormalizeWorktreePath` fallback at lines 104-113 — when `bestSplitPos < 0` (no `dydo.json` found on disk), it uses `afterMarker.IndexOf('/')` which finds only the first `/` after the worktree marker. For input `parent-task/child-task/some-file.cs`, `IndexOf('/')` returns the position after `parent-task`, stripping only the first segment. Verified Kate's test at PathUtilsTests.cs:236-243 — the assertion `Assert.Equal("C:/project/child-task/some-file.cs", result)` passes, confirming the fallback produces an incorrect path that retains `child-task/` as part of the project path. Independently confirmed the fail-safe property: the resulting path `C:/project/child-task/some-file.cs` won't match any role pattern, so the guard denies access rather than grants it.
  - **Alternative explanations considered:** Could the fallback be intentionally conservative, accepting over-blocking as a tradeoff? Possibly, but the comment at line 106-108 says it "handles relative paths where File.Exists can't verify the root" — it's designed to work, not to fail safe. The fail-safe behavior is an accidental consequence, not an intentional design.
  - **Issue:** #0094

- **DeleteDirectoryJunctionSafe exception handling (hyp-2):** CONFIRMED by Leo. 4 tests written: (a) non-existent path no-op, (b) happy-path full deletion, (c) locked file causes IOException propagation leaving partial state, (d) TOCTOU race where `File.GetAttributes` throws `FileNotFoundException` on vanished directory. All pass, confirming the method has no error handling and leaves partially-deleted directories on any filesystem exception. See finding #2.
  - **Judge ruling:** CONFIRMED (subsumed by finding #2 ruling — see issue #0088)

### Hypotheses Not Reproduced
- None — both hypotheses confirmed or partially confirmed.

### Confidence: high
The worktree system was investigated thoroughly from junction safety, path normalization, terminal security, and documentation perspectives. 6 scouts (4 reviewers, 2 test-writers) provided independent verification. All critical paths (setup, teardown, merge, prune, guard integration) were examined. The main verified fixes are solid. Remaining issues are defense-in-depth improvements (findings 1, 2), a documentation backlog (finding 5), and missing test coverage (findings 7, 8). No critical or high-severity issues found — the prior inquisition's high-severity findings have been fixed.
