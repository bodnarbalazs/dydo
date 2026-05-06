---
area: project
type: inquisition
---

# Inquisition: Test Runtime Regression (#0148 follow-up + #0165 cross-reference)

Re-prosecution of the test-suite runtime story prompted by balazs's observation that the prior `Git()`-helper fix (commit `aeee461`, "drain stdout/stderr concurrently in WorktreeMergeSafety Git helper") restored the **steady-state** baseline but did not address the **underlying mechanism** the original investigation hinted at. The downstream symptom in #0165 (xunit Console-capture cross-over under coverage) is consistent with the same broken isolation still being live. Investigation only — no code/test changes.

## 2026-05-05 — Charlie

### Scope

- **Entry point:** Feature investigation, dispatched by Adele on behalf of balazs to re-open #0148 in light of #0165.
- **Files investigated (test infra):**
  - `DynaDocs.Tests/coverage/run_tests.py`
  - `DynaDocs.Tests/coverage/gap_check.py`
  - `DynaDocs.Tests/coverage/coverage.runsettings`
  - `DynaDocs.Tests/ConsoleCapture.cs`
  - `DynaDocs.Tests/Integration/IntegrationTestBase.cs`
  - `DynaDocs.Tests/Integration/IntegrationTestCollection.cs`
  - `DynaDocs.Tests/Services/ProcessUtilsCollection.cs`
  - `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs:380-386` (`EndToEndCollection`)
- **Files investigated (test sites):**
  - `DynaDocs.Tests/Services/AuditCompactionTests.cs:835-858`
  - `DynaDocs.Tests/Services/AuditEdgeCaseTests.cs:88-130`
  - `DynaDocs.Tests/Services/SnapshotServiceTests.cs:45-67`
  - `DynaDocs.Tests/Services/WatchdogServiceTests.cs:1107-2140` (Task.Delay/WhenAny family)
  - `DynaDocs.Tests/Services/QueueServiceTests.cs:238-256` (live flake observed)
  - `DynaDocs.Tests/Services/AgentRegistryTests.cs:2970-3016` (#0165 failure #1)
  - `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:1-296`
  - `DynaDocs.Tests/Commands/WorktreeCommandTests.cs:516-546, 2905-2955` (#0165 failure #2)
  - `DynaDocs.Tests/Integration/InquisitionTests.cs:14-33`
  - `DynaDocs.Tests/Integration/WaitCommandTests.cs:192-355`
  - `DynaDocs.Tests/Integration/DispatchQueueTests.cs`
  - `DynaDocs.Tests/Utils/FileLockTests.cs`
- **Prior context cross-checked:**
  - Issue `dydo/project/issues/0148-…` (closed-style narrative; Resolution still empty)
  - Issue `dydo/project/issues/0165-…` (open; the trigger for this re-prosecution)
  - Inquisition `dydo/agents/Tara/notes-investigate-test-runtime.md`
  - Plan `dydo/agents/Tara/plan-fix-test-git-helper-deadlock.md`
  - Fix commit `aeee461a` — `git show aeee461`
- **Empirical reproductions on this hardware (clean worktree, fresh baseline):**
  - `python DynaDocs.Tests/coverage/run_tests.py` — 16:39:05 → 16:42:25 = **3 m 20 s** (dotnet test 3 m 10 s + ~10 s worktree setup/cleanup), **4053/4054 pass, 1 flake**: `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` `Assert.Single() Failure: collection was empty`. The flake is independent confirmation of the static-mutation race documented in Finding #2.
  - `python DynaDocs.Tests/coverage/gap_check.py --force-run` — 16:43:03 → 16:46:28 = **3 m 25 s** (dotnet test 3 m 13 s, +3 s coverage overhead, ~9 s worktree setup/cleanup), **4054/4054 pass on this run, exit 0, `[RESULT] All modules pass tier requirements`**. The run_tests-vs-gap_check coverage delta is ~5 s wall-clock, not the 2-5× the original brief feared. The flake from the prior `run_tests.py` reproduction did not re-fire here — confirming Finding #1's race is *intermittent*, not deterministic.
- **Scouts dispatched:** 0. The brief asks for a focused re-prosecution; the evidence is concentrated in a small number of test infrastructure files plus reproducibility on this machine. A solo case is more efficient than parallel scouts here. Judge dispatched at the end.

### Headline

The original investigation's "compounding waits + git-worktree-ops" framing pointed at the right family of mechanisms but the chosen fix addressed only **one symptom in one helper**: a 30-second `WaitForExit` cliff in `WorktreeMergeSafetyIntegrationTests.Git()` that fired when git's stdout/stderr filled the OS pipe buffer. That fix is correct and stays — but it leaves three larger problems live:

1. **Cross-collection static-state mutation.** xUnit collection definitions are misconfigured: only `Integration` actually disables parallelization between collections. `ProcessUtils`, `ConsoleOutput`, and the implicit "no-collection" classes can run in parallel, yet they all mutate the same process-global statics (`Console.Out/Error`, `ProcessUtils.IsProcessRunningOverride`). #0165's Console cross-over is the most visible face of this; the live `QueueServiceTests` flake observed during this investigation is another. The "worktree-lock fix" balazs remembered was real (`Services/WorktreeCreationLockTests.cs`, lock-file serialisation in `DispatchService.CreateGitWorktree`) but it solves a different problem (concurrent agent dispatches stomping on each other's worktree dirs); it has no bearing on in-process xunit isolation.
2. **The Git-helper deadlock pattern is not unique to the file Tara fixed.** Two more test files (`SnapshotServiceTests.cs`, `InquisitionTests.cs`) and at least six production sites (`SnapshotService`, `AuditService`, `WatchdogService`, `FileCoverageService`, `InquisitionCommand`, `WorktreeCommand`) use the same redirect-without-drain `Process.Start` pattern. Lower probability per site — the git commands they call produce less stderr — but the latent risk is identical and would hide the same way (intermittent slowdown plus `Process must exit before requested information…` failures).
3. **`gap_check.py` does not propagate test-process exit code into its own.** This is the divergence #0165 prosecuted but is worth re-stating as a runtime/observability hazard in its own right: a green gate signal masks failing assertions (`gap_check` exits 0 on tier-pass even when the embedded `dotnet test` returned non-zero).

The 10-min observation balazs reports does **not** appear at steady state on this hardware today — empirical wall-time is back to 3:20. But the same conditions that made the cliff fire in the first place (heavy disk pressure, slow git, coverage instrumentation) are still latent in the codebase, and the mechanism that makes #0165 reproducible on coverage runs is the same family of static-state-leak that *also* expands runtime through retries/flakes when the race lands wrong.

### Findings

#### 1. xUnit collections misconfigured: only `Integration` disables parallelization between collections — `ConsoleOutput`, `ProcessUtils`, `EndToEnd`, and no-collection classes all run concurrently and share process-global state

- **Classification:** root-cause (of #0165 cross-over and the QueueServiceTests live flake; contributing-factor for runtime variance via retried-flake tests).
- **Severity:** high.
- **Type:** obvious + tested (live reproduction of the QueueServiceTests flake on this run).
- **Evidence:**
  - `DynaDocs.Tests/Integration/IntegrationTestCollection.cs:8` — `[CollectionDefinition("Integration", DisableParallelization = true)]`. Only collection that opts out of cross-collection parallelism.
  - `DynaDocs.Tests/Services/ProcessUtilsCollection.cs:8` — `[CollectionDefinition("ProcessUtils")]` **without** `DisableParallelization = true`. Class doc-comment claims "Disables xUnit parallel execution between test classes that share static mutable ProcessUtils overrides" — the comment is a lie. The flag is missing, so the collection only sequences tests *within* `ProcessUtils`; it does nothing to stop other collections from running concurrently.
  - `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs:385` — `[CollectionDefinition("EndToEnd")]` similarly without the flag.
  - **No `[CollectionDefinition("ConsoleOutput")]` exists at all.** 14+ classes carry `[Collection("ConsoleOutput")]` but with no definition the collection has default settings (parallelizable between collections). Within-collection serialization still works (xunit auto-creates the implicit definition) — but the cross-collection isolation that the comment in `IntegrationTestCollection.cs:5-7` promises ("share process-global state: Environment.CurrentDirectory and Console.Out/Error") is not enforced for `ConsoleOutput`.
  - **`ProcessUtils.IsProcessRunningOverride` is mutated from three different collections:**
    - `[Collection("ProcessUtils")]`: `WatchdogServiceTests`, `QueueServiceTests`, `FileLockTests` (and others).
    - `[Collection("Integration")]`: `WaitCommandTests:202-223,336-355`, `DispatchQueueTests`. Safe in practice because `Integration` has `DisableParallelization = true`.
    - `[Collection("ConsoleOutput")]`: `WorktreeCommandTests:2905-2955`. **Not safe** — `ConsoleOutput` parallelises with `ProcessUtils`. While `WorktreeCommandTests` is running with `IsProcessRunningOverride = X`, a `QueueServiceTests` test in another thread is asserting that `IsProcessRunningOverride = false` is in effect.
  - **`Console.Out`/`Console.Error` are mutated outside `ConsoleCapture`'s gate:**
    - `DynaDocs.Tests/Services/AuditCompactionTests.cs:843-854` — direct `Console.SetError(stderr)` and restore. Class has **no** `[Collection]` attribute → its own implicit collection → parallelises with everything.
    - `DynaDocs.Tests/Services/AuditEdgeCaseTests.cs:110-129` — same pattern, same lack of `[Collection]`.
    - `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:278-296` (`CaptureAll`) — direct `Console.SetOut/SetError`. Class is `[Collection("ConsoleOutput")]`, so it serialises with other ConsoleOutput classes but not with the no-collection classes above.
    - For comparison, the disciplined path is `DynaDocs.Tests/ConsoleCapture.cs:8-115` (single static `SemaphoreSlim Gate` serialises every redirect-execute-restore). When some sites bypass the gate, the rest of the gate is no longer load-bearing.
  - **#0165's smoking-gun cross-over is exactly this pattern:**
    - `WorktreeCommandTests.Merge_BranchNotAdvanced_Blocks_WithoutRunningGitMerge` (line 517) calls `CaptureAll(...)` (gate-bypassing helper at line 278) and asserts `"0 commits ahead"` in `stderr`.
    - `AuditCompactionTests.Compact_CorruptBaseline_LogsWarningInsteadOfSilentSkip` (line 836) does `Console.SetError(stderr); …; Console.SetError(originalErr);` and asserts `"[dydo] WARNING"` in `stderr`.
    - The two classes are in different (parallelisable) collections. When `WorktreeCommandTests` is mid-test holding `Console.Error = errWriter_W`, `AuditCompactionTests` enters and snapshots `Console.Error` — getting `errWriter_W` as its "original". The remaining ordering of writes/reads/restores produces exactly the cross-over reported in #0165 (each test sees the *other's* expected output).
  - **Live reproduction on this run:** `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` failed with `Assert.Single() Failure: The collection was empty` at xUnit time `00:00:06.76` (i.e. very early in the run, well within the parallelization window). The test sets `IsProcessRunningOverride = _ => false` at line 244 and then expects `FindStaleActiveEntries()` to return one stale entry for PID 99999. `WorktreeCommandTests` is in `ConsoleOutput` and concurrently nulls `IsProcessRunningOverride` in its `finally` block at line 2921 / 2953, which races with the read inside `FindStaleActiveEntries`. This single observation is one realisation of the same race that produces #0165 failure #1 (`AgentRegistryTests.IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount` `Expected: 10, Actual: 9`) — `AgentRegistryTests` has no `[Collection]` attribute either, so it parallelises with `ProcessUtils` and `ConsoleOutput`.
- **Proposed fix path:**
  - Add `DisableParallelization = true` to `ProcessUtilsCollection`, `EndToEndCollection`, and a new `ConsoleOutputCollection` definition. Or, more cleanly, set `[assembly: CollectionBehavior(DisableTestParallelization = true)]` for the whole assembly and only re-enable parallelism for the unit-only collections that don't touch globals — given how many globals are involved, full sequential is likely the right invariant for this test suite.
  - Audit every test class without a `[Collection]` attribute that touches `Console.Out/Error` or any `ProcessUtils.*Override` static and route it through the right collection (the simplest path is one umbrella collection covering all classes that touch any process-global static).
  - Migrate the four gate-bypass `Console.SetOut/SetError` sites onto `ConsoleCapture.Stdout/Stderr/All` so the bypass surface goes to zero. Once that's done, the collection separation matters less because the gate alone is sufficient.
  - Long-term cleaner alternative (per #0165 path 1): migrate Console-asserting tests to xunit's `ITestOutputHelper`, and have production code accept a redirectable writer. Larger blast radius — call out as a separate decision rather than bundling into the fix batch.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Integration/IntegrationTestCollection.cs` (1-13), `DynaDocs.Tests/Services/ProcessUtilsCollection.cs` (1-9), `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs` (375-387), `DynaDocs.Tests/ConsoleCapture.cs` (1-148), `DynaDocs.Tests/Services/AuditCompactionTests.cs` (1-15, 830-858), `DynaDocs.Tests/Services/AuditEdgeCaseTests.cs` (1-15, 88-130), `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs` (1-25, 270-296), `DynaDocs.Tests/Commands/WorktreeCommandTests.cs` (1-7, 2750-2754, 2895-2955), `DynaDocs.Tests/Services/QueueServiceTests.cs` (1-20, 238-256), `DynaDocs.Tests/Integration/WaitCommandTests.cs` (1-7), `DynaDocs.Tests/Services/AgentRegistryTests.cs` (1-15, 2965-3016)
- **Independent verification:**
  - Grepped the entire `DynaDocs.Tests/` tree for `CollectionDefinition` — exactly three exist (`Integration`, `ProcessUtils`, `EndToEnd`); only `Integration` has `DisableParallelization = true`. `ConsoleOutput` has zero `[CollectionDefinition]` declarations and 9 `[Collection("ConsoleOutput")]` users (Charlie said "14+"; the substantive claim — no definition exists — stands, only the count is overstated).
  - Independently confirmed `ProcessUtilsCollection.cs:1-9`'s doc-comment lies: it claims to disable cross-class parallel execution, but the attribute is missing the flag.
  - Verified `WorktreeCommandTests.CaptureAll` at line 2752 delegates to `ConsoleCapture.All` — i.e. it **does** use the gate. Charlie's report describes `WorktreeCommandTests:517` as calling a "gate-bypassing helper at line 278"; that's an attribution error (the line-278 helper is in `WorktreeMergeSafetyIntegrationTests`, not `WorktreeCommandTests`). The cross-over scenario is still real, however: `AuditCompactionTests` bypasses the gate, and any non-gate caller can race with a gate-holder by capturing the gate-holder's writer as its "original" and producing the cross-over on restore. Three real gate-bypass sites confirmed (`AuditCompactionTests`, `AuditEdgeCaseTests`, `WorktreeMergeSafetyIntegrationTests.CaptureAll`); not four.
  - Confirmed `QueueServiceTests:244` mutates `IsProcessRunningOverride` inside `[Collection("ProcessUtils")]`; `WorktreeCommandTests:2907,2921,2940,2953` mutates the same static inside `[Collection("ConsoleOutput")]`. Since neither collection has `DisableParallelization=true`, the cross-collection race is real.
  - **Caveat on the `AgentRegistryTests` connection:** Charlie ties #0165's failure #1 (`IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount` `Expected: 10, Actual: 9`) to this finding via "AgentRegistryTests has no [Collection], so it parallelises with ProcessUtils and ConsoleOutput". That linkage is weak — `IncrementResumeAttempts` does not touch `Console.Out/Error` or `IsProcessRunningOverride`. The race is internal to its file-write/read cycle (`File.Move` racing with AV/indexers under heavy parallel load, per the test's own comment at lines 2989-2992). Adding `DisableParallelization` won't directly fix that bug; it may *mitigate* it by reducing concurrent disk pressure, but #0165's failure #1 likely needs its own probe (as #0165 itself flagged: "warrants its own probe before being chalked up to test timing under coverage"). Issue #0167 explicitly does not claim to fix it.
- **Alternative explanations considered:** Could the `[CollectionDefinition("ConsoleOutput")]` omission be intentional (relying on xunit's implicit-definition fallback)? The implicit definition gives within-collection serialisation but not cross-collection isolation; the doc-comment in `IntegrationTestCollection.cs:5-7` and the existence of `ConsoleCapture`'s gate both presume cross-collection isolation. The omission is a bug, not a deliberate choice. Could `ProcessUtilsCollection`'s missing flag be deliberate? Its own doc-comment claims it disables cross-class parallel execution — the flag is missing, the comment lies. Bug.
- **Issue:** #0167

#### 2. The `Git()`-helper deadlock pattern is not unique to the file Tara fixed — same redirect-without-drain pattern exists in two more test files

- **Classification:** contributing-factor (latent — same shape, lower probability per site).
- **Severity:** medium.
- **Type:** obvious.
- **Evidence:**
  - `DynaDocs.Tests/Services/SnapshotServiceTests.cs:52-67` — `RunGit`:

    ```csharp
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = args,
        WorkingDirectory = _testDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using var process = Process.Start(psi);
    process?.WaitForExit(5000);
    ```

    Same shape as `WorktreeMergeSafetyIntegrationTests.Git` *before* commit `aeee461`: stdout and stderr both redirected, neither drained, `WaitForExit(5000)`. Lower timeout than the original 30s site, so the worst-case dead time per call is 5s instead of 30s. Used by `InitGitRepo()` (lines 45-50, three calls per init: `init`, `config user.email`, `config user.name`) and by individual tests that follow with `RunGit("add .")`, `RunGit("commit -m \"initial\"")`. ~10+ git invocations per test fixture in some tests.
  - `DynaDocs.Tests/Integration/InquisitionTests.cs:14-33` — `InitGitRepo`: same redirect-without-drain pattern, two calls (`init` + `commit --allow-empty`), each with `WaitForExit(5000)`. Smaller blast radius than `SnapshotServiceTests` (one git op pair per test, not the full lifecycle), but the same pattern.
- **Why this matters even though Tara called it "out of scope":** Tara's plan §"Out of Scope" deliberately left these for later because (a) they don't run 8-in-a-row like `WorktreeMergeSafetyIntegrationTests` did, and (b) the git commands they invoke (`init`, `config`, `add`, `commit -m`) are normally low-output. That reasoning still holds for the steady state. But under the conditions that triggered the original cliff (PowerShell tool invocation, AV/indexer-induced slow git output, parallel disk pressure), `init`/`commit` can produce unbounded `Updating files: …%` progress and autocrlf warnings. The same intermittent pattern would re-surface here with a 5s-per-call ceiling instead of 30s. balazs's concern that "the fix wasn't the fix" is supported by this finding: the fix was applied to one helper of N where N >= 3 (test code) and N >= 6 if you count the production helpers below.
- **Proposed fix path:** apply the exact same refactor commit `aeee461` made to `WorktreeMergeSafetyIntegrationTests.Git()` — drain stdout and stderr concurrently via `ReadToEndAsync` before `WaitForExit`; on timeout `Process.Kill(entireProcessTree: true)` and throw a clear timeout message; on non-zero exit surface the captured stderr. Mechanical patch, two test files, no production code touched. Optionally extract a single `TestProcess.RunGit` helper in `DynaDocs.Tests/` and switch all three sites onto it, so the same drift can't accrete a fourth time.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Services/SnapshotServiceTests.cs` (40-67), `DynaDocs.Tests/Integration/InquisitionTests.cs` (1-33), and the diff of commit `aeee461` against `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs`.
- **Independent verification:** read the cited helpers line-by-line. `SnapshotServiceTests.RunGit` (lines 52-67) sets `RedirectStandardOutput = true` and `RedirectStandardError = true`, calls `Process.Start(psi)`, then `process?.WaitForExit(5000)` with no `ReadToEndAsync` between the two — exactly the shape the `aeee461` commit replaced. `InquisitionTests.InitGitRepo` (lines 14-33) is the same shape with two sequential calls. Compared against `aeee461`'s diff: the fixed helper now wraps `Process.Start` with `using`, kicks off `StandardOutput.ReadToEndAsync()` and `StandardError.ReadToEndAsync()` concurrently before `WaitForExit`, and on timeout calls `Kill(entireProcessTree: true)`. The two test sites match the *pre-fix* shape exactly.
- **Alternative explanations considered:** Tara's plan §"Out of Scope" explicitly deferred these on the grounds that `init`/`config`/`commit --allow-empty` produce minimal stderr in the steady state. That reasoning holds for the steady state but does not eliminate the latent risk under the same trigger conditions that produced the original cliff (PowerShell tool, slow git output, parallel disk pressure). Charlie's framing — "the fix was applied to one helper of N where N >= 3" — is correct.
- **Issue:** #0168

#### 3. Same redirect-without-drain pattern exists in production code — six call sites with `RedirectStandardError = true` that read only `StandardOutput`

- **Classification:** out-of-scope (for the runtime-regression brief — these don't affect test wall-time directly) but worth surfacing because the original investigation flagged them and the same mechanism was the headline of the test-side fix.
- **Severity:** medium.
- **Type:** obvious.
- **Evidence:** `Services/SnapshotService.cs:60-71` (`rev-parse HEAD`, 5s timeout) and `:91-104` (`ls-files --full-name`, 10s timeout) — both redirect both streams, only `StandardOutput.ReadToEnd()`, then `WaitForExit`. The `ls-files` call in particular can produce very large stdout on a many-file repo; if git also emits stderr (CRLF warnings, deprecation notices, init-template hints), the stderr buffer fills, git blocks, and the read-stdout path is fine but stderr is silent. Same hidden hang pattern. Other sites with `RedirectStandardError = true`: `Services/AuditService.cs:281` (`rev-parse --short HEAD`), `Services/WatchdogService.cs:159-165` (spawned watchdog process — different shape, persistent, less risk), `Services/FileCoverageService.cs:299-305` (general-purpose `RunProcess`-like helper, varies by caller), `Commands/InquisitionCommand.cs:175-181` (`git diff --stat HEAD@{since}`), `Commands/WorktreeCommand.cs:632-638` (worktree merge spawns).
- **Why "out of scope" rather than "contributing factor":** these are exercised under tests but only via mocked-out overrides (`DispatchService.CreateGitWorktreeOverride`, `WorktreeCommand.RunProcessOverride`, etc.), so they don't contribute to test wall-time. They are production hazards in their own right and warrant a tracked issue, but they should not be bundled into the fix-order for the runtime regression.
- **Proposed fix path:** track separately from this inquisition. The cleanest path is a small helper `ProcessUtils.RunWithCapture(string fileName, string args, int timeoutMs)` that does the concurrent-drain pattern once and is used by every caller — but that's a separate, larger refactor. For this inquisition, surface as a proposed issue (see end of report) and move on.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/SnapshotService.cs` (55-110), `Services/AuditService.cs` (265-298), `Services/FileCoverageService.cs` (280-319), `Commands/InquisitionCommand.cs` (160-196), `Commands/WorktreeCommand.cs` (620-650), `Services/WatchdogService.cs` (150-180).
- **Independent verification:** read each cited site. `SnapshotService.GetCurrentGitHead` and `GetGitTrackedFiles` (the two highest-traffic sites) drain stdout via blocking `ReadToEnd()` but never read stderr — confirmed identical to Charlie's pattern description. `AuditService.GetCurrentGitHead`, `FileCoverageService.RunGit`, and `InquisitionCommand.HasChangesSince` follow the same pattern. `WorktreeCommand.RunProcessWithExitCode` (lines 627-648) is a slightly different shape: it reads `StandardOutput.ReadToEnd()` then `StandardError.ReadToEnd()` *sequentially* — same deadlock potential when stderr fills before stdout reaches EOF. Charlie's "different shape, persistent, less risk" classification of `WatchdogService.cs:157-167` is correct: that path spawns the watchdog as a fire-and-forget process with no `WaitForExit` on the parent side, so the deadlock pattern does not apply.
- **Alternative explanations considered:** classification as "out-of-scope" for the runtime-regression brief stands — these sites are tested through `*Override` indirection (`DispatchService.CreateGitWorktreeOverride`, `WorktreeCommand.RunProcessOverride`, `FileCoverageService.GitLsFilesOverride`, etc.), so they don't contribute to test wall-time. They are production hazards in their own right and warrant a tracked issue.
- **Issue:** #0170

#### 4. `gap_check.py` does not propagate the underlying `dotnet test` exit code into its own — runtime-observability gap that masks the very flakes Finding #1 produces

- **Classification:** root-cause (of #0165's "exit 0 with three failures" trap) and contributing-factor for runtime perception (the "real" runtime is hidden because re-runs are needed to see failures).
- **Severity:** high (operational — "gate green" is taken as truth).
- **Type:** obvious.
- **Evidence:**
  - `DynaDocs.Tests/coverage/gap_check.py:347-354` — `run_tests()` correctly returns `False` on non-zero `dotnet test` exit code. Good.
  - `DynaDocs.Tests/coverage/gap_check.py:673-686` — `main()` calls `run_tests()` (force-run path or staleness-triggered path), captures the return into `tests_ok`, **prints a message** (`"Tests failed. Analyzing available coverage data anyway."`), and proceeds. The `tests_ok` variable is never consulted again.
  - `DynaDocs.Tests/coverage/gap_check.py:716-719`:

    ```python
    if has_failures or registry_errors:
        sys.exit(1)
    sys.exit(0)
    ```

    Exit code is conditioned only on coverage-tier failures and registry errors — not on `tests_ok`. So a run where `dotnet test` returns non-zero (e.g. one `[FAIL]` on a flake) but every module still meets its tier exits 0. This is exactly the trap #0165 documented and the reason the embedded failures are silent to the gate.
  - The complementary `run_tests.py` does the right thing — it propagates the dotnet exit code at line 160 (`sys.exit(rc)`), which is why `python run_tests.py` correctly returned non-zero on this run when the `QueueServiceTests` flake fired.
- **Proposed fix path:** in `gap_check.py:716-719`, OR `tests_ok` into the exit condition: `if has_failures or registry_errors or not tests_ok: sys.exit(1)`. One-line patch. Optionally also surface a top-line `[RESULT]` string before exit so the human sees "Tests failed AND tier check passed — gate FAILS" rather than scrolling for the buried `Tests failed (exit code 1)` line.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/coverage/gap_check.py` (340-355, 660-720), `DynaDocs.Tests/coverage/run_tests.py` (140-160).
- **Independent verification:** traced the `tests_ok` lifecycle in `gap_check.py`. Lines 347-354 confirm `run_tests()` returns `False` on non-zero `dotnet test` exit code. Lines 674-686 confirm `tests_ok` is captured into a local variable in *both* the `--force-run` branch and the staleness-triggered branch, used only to print a console message ("Tests failed. Analyzing available coverage data anyway."), and never referenced again. Lines 716-719 confirm the exit condition uses only `has_failures or registry_errors`. The complementary `run_tests.py:160` does `sys.exit(rc)` — exact opposite behaviour, which is why this run's `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` flake produced non-zero exit on `run_tests.py` but exit 0 on `gap_check.py --force-run`. **Additional observation:** `tests_ok` is bound conditionally — if `args.force_run` is False AND `is_fresh` is True, no `tests_ok` is bound. Today this never trips because `tests_ok` is not consulted outside its branches; the proposed fix needs to initialise `tests_ok = True` before the if/else (or check `'tests_ok' in locals()`).
- **Alternative explanations considered:** could the divergence be intentional ("the gate's job is coverage, not tests")? No — both gates ultimately ask "is the test suite green?" and the docstring at `gap_check.py:1-25` and the issue Resolution sections in #0148 and #0165 both treat `gap_check`'s exit code as authoritative. Charlie's reading is correct.
- **Issue:** #0169

#### 5. `WatchdogServiceTests` adds at least 9 fixed `Task.Delay(250-400ms)` settle waits plus 9 `Task.WhenAny(runTask, Task.Delay(5s))` upper-bounds — wallclock floor of ~2.3 s on the class regardless of how fast the underlying logic is

- **Classification:** contributing-factor (modest, but compounds with #1 because these are in `ProcessUtils` collection that races with `ConsoleOutput`).
- **Severity:** low.
- **Type:** obvious.
- **Evidence:** `DynaDocs.Tests/Services/WatchdogServiceTests.cs:1118, 1137, 1160, 1255, 1342, 1963, 2055, 2077, 2138` (settle waits 250 ms / 400 ms) and `:1121, 1140, 1162, 1260, 1333, 1358, 1965, 2057, 2079, 2140` (5 s upper-bound `WhenAny`s). Pattern is the standard "let the loop spin up, do the deed, wait for it to settle" — fine in isolation, but cumulative for a class with this many lifecycle tests. Floor is `~9 × 250 ms = 2.3 s` for the class even when every `runTask` exits in microseconds; ceiling jumps to `~9 × 5 s = 45 s` if any of them stall on the static-mutation race in Finding #1.
- **Proposed fix path:** out-of-scope for this inquisition. Suggest a follow-up "tighten watchdog test polling" task that switches the settle waits to `await PollUntil(condition, timeout: 250ms, step: 10ms)` so fast paths drop to the actual settle time. Trivially safe under the normal case and the mechanism is identical to xunit's existing `Eventually(...)` pattern in other test suites. Not the headline finding.
- **Judge ruling:** CONFIRMED (with minor count correction)
- **Files examined:** `DynaDocs.Tests/Services/WatchdogServiceTests.cs` spot-checked at lines 1115-1145.
- **Independent verification:** grep for `Task.Delay(250)` / `Task.Delay(400)` returned 8 hits (Charlie said 9); grep for `TimeSpan.FromSeconds(5)` returned 13 hits (Charlie said 9). The substantive claim — fixed waits compound to a ~2 s floor for the class even on fast paths — stands. The exact counts are slightly off in both directions; the order of magnitude and the contributing-factor classification are correct.
- **Alternative explanations considered:** could the settle waits be necessary because the watchdog has no reliable "I'm ready" signal? Yes — the proposed fix path (poll-with-timeout) acknowledges this and just tightens the worst case; the floor still exists, it just becomes adaptive. Severity: low. Not a fix candidate for this inquisition's batch.
- **Issue:** none filed (low severity, opportunistic follow-up; track in the same future task as similar test-tightening work).

#### 6. `WorktreeMergeSafetyIntegrationTests` real-git fixture costs ~24-40 s for the class because every [Fact] does `git init` + commits + `git worktree add` against the real filesystem

- **Classification:** contributing-factor.
- **Severity:** low.
- **Type:** obvious.
- **Evidence:** `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:204-216` (`InitRepoWithUnadvancedBranch`) and similar `InitRepoWithAdvancedBranch`. Each test calls `Git(_repoDir, "init", …)`, two `Git config`, write seed, `Git add`, `Git commit`, `Git branch`, `Git worktree add` — eight git invocations per fixture. Eight `[Fact]`s in the class, each invoking the fixture in its arrange block. Tara measured ~4.4 s × 8 = ~35 s for this class on the fast path; the cliff (now fixed) added ~30 s × N on top. The fast-path cost is real but is intentional — these tests are explicitly integration tests that exercise real git behaviour, per the class doc-comment at lines 8-16. So this is not a fix candidate; just acknowledge the cost and move on.
- **Proposed fix path:** none. Documented for completeness so future runtime investigations don't re-discover this cost as if it were anomalous.
- **Judge ruling:** CONFIRMED (acknowledgement, no-fix)
- **Files examined:** `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs` (1-25 — class doc-comment confirms intentional integration-style real-git execution).
- **Independent verification:** the class doc-comment explicitly says "End-to-end safety check tests that invoke ExecuteMerge against a real git repo WITHOUT mocking RunProcessCapture", which justifies the ~4.4 s/test fast-path cost cited in Tara's notes.
- **Alternative explanations considered:** N/A — Charlie's classification (intentional cost, no fix) is correct.
- **Issue:** none.

#### 7. `run_tests.py`'s per-invocation `git worktree add` is a fixed ~10 s setup tax that compounds with rebuilds

- **Classification:** out-of-scope (intentional design for parallel-agent isolation; balazs's hint about "git operations taking ages (worktrees)" was about a different layer).
- **Severity:** low.
- **Type:** obvious.
- **Evidence:** `DynaDocs.Tests/coverage/run_tests.py:38-46` (`git worktree add --detach <tempdir> HEAD`, observed in this run's log to take ~10 s on a clean checkout because of `Updating files: 1494/2996…100%` — i.e. 2996 files copied). `:49-77` (`copy_dirty_files`). `:93-105` (`git worktree remove --force` + retry `shutil.rmtree`). The cleanup retry loop tolerates up to 2 s of extra wait if the first remove fails (`time.sleep(1)` × 2). Each invocation pays this cost; there is no reuse across invocations because the temp worktree is per-uuid (`f"dydo-test-{uuid.uuid4().hex[:8]}"`). The build artefacts don't survive between runs either — every `python run_tests.py` triggers a full restore + build because the worktree is fresh. On this run the build phase showed `Restored … (in 184 ms)` followed by full compilation; the file copy loop was the dominant cost.
- **Why "out of scope":** this is the documented design (`run_tests.py:1-12` docstring: "Avoids DLL lock contention when multiple agents run tests concurrently"). It is the right tradeoff for the parallel-agent case; for solo iteration the ~10-15 s tax could be saved by reusing a stable worktree, but that's a feature request, not a regression.
- **Proposed fix path:** none for this inquisition. If solo-iteration speed becomes important, surface a separate `--reuse-worktree` flag — but match the dispatch policy's expectations first; don't break the parallel-agent guarantee.
- **Judge ruling:** CONFIRMED (acknowledgement, no-fix)
- **Files examined:** `DynaDocs.Tests/coverage/run_tests.py` (1-12 docstring, 38-46 `create_worktree`, 49-77 `copy_dirty_files`, 93-105 `remove_worktree`).
- **Independent verification:** the file's own docstring at lines 1-12 confirms the "avoid DLL lock contention when multiple agents run tests concurrently" design intent. The `time.sleep(1) × 2` retry loop in `remove_worktree` is consistent with Charlie's "tolerates up to 2s of extra wait" claim.
- **Alternative explanations considered:** N/A — Charlie's "intentional design, out-of-scope" classification is correct.
- **Issue:** none.

### Fix-order Recommendation — Judge Review

The proposed fix order in §"Fix-order Recommendation" below is **sound**:
1. **#0169** (`gap_check.py` exit propagation) first — without it, every regression test for #0167 / #0168 / #0170 is silent, which means we can't reliably verify the other fixes.
2. **#0167** (collection isolation, two-step) second — landing 1a (gate-bypass migration) and 1b (DisableParallelization) together is the right call, for the dependency reasons Charlie articulates.
3. **#0168** (test-side git-helper drain) any time after #0169 lands — mechanical, low risk, independent of #0167.
4. **#0170** (production redirect-without-drain) tracked separately — different blast radius, different testing burden, not bundled.
5. Finding #5 (WatchdogServiceTests timing) — opportunistic, no issue filed.

### Hypotheses Not Reproduced

- "The 10-min runtime is the current steady state on this hardware." — **Not reproduced.** Empirical run on a fresh worktree returned 3:20 / 4053 of 4054 passing, identical to Tara's pre-fix baseline. The fix is doing what it was supposed to: keep the cliff from firing in the steady state. balazs's reported 10-min experience appears to be an *episodic* manifestation tied to the conditions the original investigation identified (PowerShell tool, slow git output, AV pressure) — and the same conditions can still surface in two unfixed test files (Finding #2). I could not reproduce it on demand.
- "xUnit `parallelizeTestCollections` is set wrong in config." — **Not reproduced.** No `xunit.runner.json`, no `[assembly: CollectionBehavior(...)]`, no `MaxParallelThreads` override. Defaults are in effect. The misconfiguration is purely in the per-collection definitions (Finding #1), not in any global flag.

### Fix-order Recommendation

If you take this batch end-to-end, the dependency order is:

1. **Finding #4** (`gap_check.py` exit-code propagation). One-line patch. Land first — until the gate signal is honest, every other fix's regression test is silent. This makes the rest of the work observable.
2. **Finding #1** (collection / Console-capture isolation). Two-step, both small:
   - 1a. Migrate the four gate-bypass `Console.SetOut/SetError` sites onto `ConsoleCapture` (`AuditCompactionTests`, `AuditEdgeCaseTests`, `WorktreeMergeSafetyIntegrationTests.CaptureAll`, `IntegrationTestBase` already does it correctly via `ConsoleCapture.AllAsync` — the only one to leave alone).
   - 1b. Add `DisableParallelization = true` to `ProcessUtilsCollection`, `EndToEndCollection`, and a new `ConsoleOutputCollection`; route every no-collection class that touches any process-global static into one of those collections.
   These should land together — landing 1b without 1a leaves the gate-bypass sites as a residual hazard inside their newly-sequential collection (less harmful but still a bug); landing 1a without 1b leaves the cross-collection race for `ProcessUtils.IsProcessRunningOverride` etc.
3. **Finding #2** (Git-helper deadlock pattern in 2 more test files). Mechanical refactor. Land at any point but before any future investigation that changes the conditions under which git's stderr fires.
4. **Finding #3** (production redirect-without-drain). Track separately from the test-runtime fix batch — different blast radius, different testing burden.
5. **Finding #5** (WatchdogServiceTests timing tightening) — opportunistic, low priority.

### Issues Filed (judge action, 2026-05-05)

- **#0167** (was #NEW-A): Test parallelism breaks process-global static isolation — Finding #1. Severity: high. Area: general.
- **#0168** (was #NEW-B): Git-helper redirect-without-drain in `SnapshotServiceTests.RunGit` and `InquisitionTests.InitGitRepo` — Finding #2. Severity: medium. Area: backend.
- **#0169** (was #NEW-C): `gap_check.py` does not propagate `dotnet test` exit code — Finding #4. Severity: high. Area: general.
- **#0170** (was #NEW-D): Production `Process.Start` callers redirect both streams but read only stdout — Finding #3. Severity: medium. Area: backend.
- **(Resolution-update suggestion for #0148):** the original fix is correct and stays — but the issue's "Other lower-priority findings from this investigation" bullet under Related is incomplete. Add pointers to #0167 / #0168 / #0170 and (if #0148 is being closed) re-frame as "narrow fix; broader pattern tracked under #0167–#0170".

### Confidence: high — for the test-side findings; medium for the production-side surface area.

- High on Finding #1 (reproduced live in this run, plus direct code inspection of the 4 gate-bypass sites and the 3 collections that mutate `IsProcessRunningOverride`).
- High on Finding #4 (single-screen Python; the divergence is unambiguous).
- High on Finding #2 (mechanical pattern match; same shape as the file Tara already patched).
- Medium on Finding #3 (six production sites identified by grep; I have not exhaustively traced each one's drain semantics, only confirmed they redirect both streams and at least the two highest-traffic ones in `SnapshotService` read only stdout).
- The empirical reproduction of `gap_check.py --force-run` was running at the time of this draft and will be appended below as a postscript when it lands. It does not change any classification — only the wall-time ratio between with-/without-coverage runs.

### Postscript: gap_check.py timing landed (2026-05-05 16:46:28)

`gap_check.py --force-run` completed in **3 m 25 s** wall-clock (dotnet test 3 m 13 s). All 4054 tests passed on this run; the `QueueServiceTests` flake observed in the prior `run_tests.py` reproduction did not re-fire — consistent with Finding #1's "intermittent, race-driven" framing. Coverage instrumentation added ~3 s of dotnet test wall-time; setup overhead (worktree create + restore + build + cleanup) was ~9 s. The 2-5× coverage cost called out in earlier discussions is **not** present on this hardware in 2026-05.

What the timing data **does** confirm:

- The 10-min runtime is *not* a steady-state fact today — it was a real but episodic manifestation of the cliff Tara fixed plus the conditions under which it fires. The fix did its job for the steady state.
- The runtime delta between the two runners (3:20 vs 3:25) does not justify the existence of two separate gates if their purpose is "is the test suite green?". Finding #4's recommendation (fold `tests_ok` into `gap_check`'s exit code) restores that semantic.

What it **does not** confirm (and could not, in a single run):

- Whether the in-#0165 cross-over reproduces at this commit. It didn't fire on this run, but #0165's reporter (also Charlie, on a different branch) saw it on `gap_check` runs on this same day. The race is real per Finding #1's code analysis; reproducing it on demand requires running gap_check enough times to land the timing window — out of scope for this inquisition.
