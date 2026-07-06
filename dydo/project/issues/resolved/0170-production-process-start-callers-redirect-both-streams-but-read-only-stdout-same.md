---
title: Production Process.Start callers redirect both streams but read only stdout: same deadlock pattern as #0148
id: 170
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-05
---

# Production Process.Start callers redirect both streams but read only stdout: same deadlock pattern as #0148

Multiple production sites (`SnapshotService`, `AuditService`, `FileCoverageService`, `InquisitionCommand`, `WorktreeCommand`) use the same `Process.Start` redirect-without-drain pattern that #0148 fixed in test code: both stdout and stderr redirected, only stdout drained, no concurrent `ReadToEndAsync`. If the spawned process produces enough stderr to fill the OS pipe buffer (~64 KB on Windows), it blocks on the stderr write, stdout never reaches EOF, and `ReadToEnd()` hangs until the per-site `WaitForExit` timeout fires — these are production hazards in their own right, currently masked in tests by `*Override` indirection.

## Description

Multiple production sites use the same `Process.Start` redirect-without-drain pattern that #0148 fixed in test code. They redirect both `StandardOutput` and `StandardError`, then call `StandardOutput.ReadToEnd()` (or read sequentially) without ever draining `StandardError`. If the spawned process produces enough stderr to fill the OS pipe buffer (~64 KB on Windows), it blocks on the stderr write, the stdout stream never reaches EOF, and `ReadToEnd()` hangs forever — or at least until the configured `WaitForExit` timeout fires (1s–10s depending on site).

These don't currently affect test wall-time because callers are exercised through `*Override` indirection (`SnapshotService.RunGitOverride`-style hooks, `WorktreeCommand.RunProcessOverride`, etc.) — so they don't contribute to the test runtime regression. They are production hazards in their own right.

## Evidence

| File:Lines | Command | Timeout | Drain |
|---|---|---|---|
| `Services/SnapshotService.cs:55-78` | `git rev-parse HEAD` | 5s | stdout via `ReadToEnd`; stderr never read |
| `Services/SnapshotService.cs:88-110` | `git ls-files --full-name` | 10s | stdout via `ReadToEnd`; stderr never read |
| `Services/AuditService.cs:272-298` | `git rev-parse --short HEAD` | 1s | stdout via `ReadToEnd`; stderr never read |
| `Services/FileCoverageService.cs:292-319` | `git ls-files` (general-purpose `RunGit`) | 10s | stdout via `ReadToEnd`; stderr never read |
| `Commands/InquisitionCommand.cs:165-196` | `git diff --stat HEAD@{since}` | 5s | stdout via `ReadToEnd`; stderr never read |
| `Commands/WorktreeCommand.cs:627-648` | git/cli spawn | configurable | stdout then stderr **sequentially** — same deadlock potential when stderr fills first |

`Services/WatchdogService.cs:157-167` redirects both streams but spawns the watchdog as a fire-and-forget persistent process; the parent never `WaitForExit`s, so the deadlock pattern does not apply. **Out of scope** for this issue.

The `ls-files` call in `SnapshotService` is the highest-risk candidate: it can produce very large stdout on a many-file repo; if git also emits stderr (CRLF warnings, deprecation notices, init-template hints), the stderr buffer fills, git blocks, and the read-stdout path is fine but stderr is silent.

## Fix path

The cleanest pattern is a single helper in `Utils/ProcessUtils.cs`:

```csharp
public static (int ExitCode, string Stdout, string Stderr) RunWithCapture(
    string fileName, string args, string? workingDir, int timeoutMs)
{
    // Concurrent-drain pattern — see commit aeee461 for the test-side equivalent.
}
```

Then route every site above onto it. This is a separate, larger refactor than the test-runtime fix batch — track here, prioritise after #0167 / #0169.

## Related

- #0148 — original test-side fix (`aeee461`).
- #0168 — same pattern in two more test files (`SnapshotServiceTests`, `InquisitionTests`).
- Inquisition: `dydo/project/inquisitions/test-runtime-regression.md` Finding #3.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)