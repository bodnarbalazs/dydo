---
id: 168
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-05
---

# Git-helper redirect-without-drain pattern in SnapshotServiceTests.RunGit and InquisitionTests.InitGitRepo

Two test files (`SnapshotServiceTests.RunGit` and `InquisitionTests.InitGitRepo`) carry the same redirect-without-drain pattern that commit `aeee461` fixed in `WorktreeMergeSafetyIntegrationTests.Git()` for #0148: both stdout and stderr redirected, neither drained concurrently with `WaitForExit`. Lower probability per site than #0148 because the git commands they invoke (`init`, `config`, `add`, `commit`) produce less stderr, but the latent deadlock is identical and would surface intermittently as the same `Process must exit before requested information…` failure once the OS pipe buffer fills.

## Description

The pipe-drain deadlock pattern fixed in commit `aeee461` (`WorktreeMergeSafetyIntegrationTests.Git()`, #0148) is also present in two other test files. Lower probability per site (the git commands they invoke produce less stderr) but the latent risk is identical and would hide the same way: intermittent slowdown plus `Process must exit before requested information…` failures when git's stderr fills the OS pipe buffer.

## Evidence

### `DynaDocs.Tests/Services/SnapshotServiceTests.cs:52-67` — `RunGit`

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

Same shape as `WorktreeMergeSafetyIntegrationTests.Git` *before* commit `aeee461`: stdout and stderr both redirected, neither drained, `WaitForExit` with no concurrent `ReadToEndAsync`. Lower timeout than the original 30s site, so the worst-case dead time per call is 5s instead of 30s. Used by `InitGitRepo()` (lines 45-50, three calls per init) and by individual tests that follow with `RunGit("add .")`, `RunGit("commit -m \"initial\"")`. ~10+ git invocations per test fixture in some tests.

### `DynaDocs.Tests/Integration/InquisitionTests.cs:14-33` — `InitGitRepo`

Same redirect-without-drain pattern, two calls (`init` + `commit --allow-empty`), each with `WaitForExit(5000)`. Smaller blast radius than `SnapshotServiceTests` (one git op pair per test, not the full lifecycle), but the same pattern.

## Why this matters

Tara's plan (`dydo/agents/Tara/plan-fix-test-git-helper-deadlock.md` §"Out of Scope") deliberately left these for later because the git commands they invoke (`init`, `config`, `add`, `commit -m`, `commit --allow-empty`) are normally low-output. That reasoning holds for the steady state. But under the conditions that triggered the original cliff (PowerShell tool invocation, AV/indexer-induced slow git output, parallel disk pressure), `init`/`commit` can produce unbounded `Updating files: …%` progress and autocrlf warnings. The same intermittent pattern would re-surface here with a 5s-per-call ceiling instead of 30s.

## Fix path

Apply the exact same refactor commit `aeee461` made to `WorktreeMergeSafetyIntegrationTests.Git()`:

1. Drain stdout and stderr concurrently via `ReadToEndAsync` before `WaitForExit`.
2. On timeout: `Process.Kill(entireProcessTree: true)` and throw a clear timeout message.
3. On non-zero exit: surface the captured stderr.

Mechanical patch, two test files, no production code touched. Optionally extract a single `TestProcess.RunGit` helper in `DynaDocs.Tests/` and switch all three sites onto it so the same drift can't accrete a fourth time.

## Related

- #0148 — original investigation and fix (`aeee461`).
- Inquisition: `dydo/project/inquisitions/test-runtime-regression.md` Finding #2.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)