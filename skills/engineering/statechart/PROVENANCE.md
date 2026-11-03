# Statechart provenance

Source: [cursor/plugins, pstack/skills/statechart](https://github.com/cursor/plugins/tree/1ba91fc25c0977741dd0e267849e6234b037ed0a/pstack/skills/statechart),
commit `1ba91fc25c0977741dd0e267849e6234b037ed0a`. This is the canonical source,
not the generated Claude harness copy. [LICENSE](LICENSE) is the pinned pstack MIT
license, Copyright (c) 2026 Lauren Tan.

The compiler, frontends, passes, backends, corpus and renderer guide are vendored.
Local changes:

- Explicit-only Codex metadata accompanies upstream Claude metadata. The description
  is human-facing. Formal-check use retains the model, config, bounds and sync commit.
- Folder-local Git attributes retain LF for executable shell scripts and the
  byte-compared corpus even when the host enables `core.autocrlf`.
- A native PowerShell launcher selects supported Node. Windows workers wait until
  a Job Object owns them; the job kills their descendants on exit. A detached Node
  janitor cleans task-owned scratch after normal or abrupt parent termination.
  POSIX launch and process groups retain upstream behavior.
- Streamed TLC/DOT lines normalize CRLF. Tool overrides resolve against the caller
  before TLC changes directory (upstream issue #973 / PR #1017). Empty output folders
  on Windows use non-recursive rmdir before rename; existing content is never removed.
- `exitClass` has an explicit literal return type. Two upstream async test callbacks
  discard their return values for the pinned local TypeScript check. Neither changes assertions.
- CLI output validation, worker launch and publication are local functions
  to meet the cognitive-complexity limit; verdict classification avoids a nested ternary.
  Native lifecycle tests retain real tool execution with absolute, relative, bare and
  unset `JAVA`, and controlled failure tests cover ownership and cleanup errors.
- Windows tool launches and recurring test subprocesses request hidden consoles,
  including Java discovery, real TLC, renderer installation and idle lifecycle fixtures.
  Job ownership, handshakes, termination and cleanup remain intact.
- Tests serialize files, use Node to launch npm's CLI on Windows, normalize plain
  TLC line endings, and explicitly skip the upstream POSIX CLI/recording harness on
  Windows. All original cases remain for POSIX. Native Gherkin acceptance and Windows
  lifecycle tests are added, alongside the CRLF regression. Skips are not Windows proof.
- dydo's native invocation enters through `native-entry.ts`. On Linux,
  `posix-lifetime.ts` checks inside the control channel and process group that dydo's
  native owner hands over; that owner performs scratch cleanup.
- Checker input must be the official TLC 1.7.4 jar by SHA-256. A `CommunityModules.jar`
  beside it is rejected as an unpinned dependency, and each run copies only the verified
  jar into owned scratch. Replay tests therefore need the real jar in `TLA2TOOLS` and
  skip without it.
- Original Balazs Bodnar MIT Java sources under `scripts/java/` route TLC through a
  loopback RMI socket bootstrap; SANY stays direct. TLC calls require the sealed helper
  and never fall back to raw TLC. The helper is built but not runtime-validated; see
  [its readme](scripts/java/readme.md).

The Windows-era review and build records are at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).

Dependencies and setup: [runtime](references/runtime.md). The original package test
script is `node --disable-warning=ExperimentalWarning --test test/*.test.ts`;
the local command adds `--test-concurrency=1` to protect a shared machine. Runtime
TLC still uses its upstream pinned one worker, fingerprint 0 and final liveness check.

## Upstream delta check, 2026-10-29

The path's latest commit remains `519d36aa98955bf680db01bcde5fdfba686dc901`
(#981, wait for macOS process-group reaping), already in this pin. #982's qualified
action-name fix is also present. The SKILL.md rewrite remains `34b1b7c96f09f81a21a6c78f65b7833f7dcb5b24`.

Issues [#923](https://github.com/cursor/plugins/issues/923) (old harness temp paths /
LD_PRELOAD) and [#973](https://github.com/cursor/plugins/issues/973) (relative tool
paths) remain open. PRs [#1017](https://github.com/cursor/plugins/pull/1017),
[#997](https://github.com/cursor/plugins/pull/997),
[#924](https://github.com/cursor/plugins/pull/924),
[#890](https://github.com/cursor/plugins/pull/890),
[#844](https://github.com/cursor/plugins/pull/844),
[#819](https://github.com/cursor/plugins/pull/819) and
[#817](https://github.com/cursor/plugins/pull/817) are open and unmerged. The latter
four concern pre-rewrite interfaces; none is silently incorporated. No X announcement
was independently verified. Package versions and the license were read from the pin.
