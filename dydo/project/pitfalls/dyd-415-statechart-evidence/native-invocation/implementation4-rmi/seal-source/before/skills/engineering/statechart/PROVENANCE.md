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
- The correction hop measures maintained adaptations separately from unchanged vendor
  functions. CLI output validation, worker launch and publication are local functions
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
- The retained Stage A draft adds original Balazs Bodnar MIT Java transport
  sources and a build-only JDK lock under `scripts/java/`. It routes verdict,
  ChartRun and native readiness TLC entry through a loopback RMI bootstrap;
  SANY stays direct. No helper artifact has been built or embedded. The
  [source checkpoint](../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/checkpoint.md)
  and its [closure result](../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031a/result.md)
  record the original standalone input gap, tightened jar checks, held execution,
  exact source identities and unavailable Java measurements. This is unvalidated local work,
  not a change to the upstream license or a prompt-free platform claim.

- The later checker-data isolation draft adds native and TypeScript helpers that
  reject an external unpinned `CommunityModules.jar` and copy only the verified
  official checker jar into existing owned scratch. SANY and bootstrap TLC share
  that isolated data; original model/library roots remain. The compiler-only
  exploded jar view is separate. The
  [source checkpoint](../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/runtime-isolation/checkpoint.md)
  binds accepted recipes, source hashes and outstanding proof. No helper artifact,
  runtime result or prompt-free claim is established by this source amendment.

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
