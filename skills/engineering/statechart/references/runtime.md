# Runtime and provenance

This folder vendors `pstack/skills/statechart` from `cursor/plugins` at
`1ba91fc25c0977741dd0e267849e6234b037ed0a`. The adjacent [LICENSE](../LICENSE)
preserves MIT Copyright (c) 2026 Lauren Tan. [Provenance](../PROVENANCE.md) records local changes.

## Prerequisites

`dydo statechart` installs and verifies its own locked prerequisites on first use:
Node 26.13.0, Temurin JRE 21.0.8+9, TLC 1.7.4 and Pele 1.9.0. It accepts an existing
Node 22.18–22.x or 23.6+ and Java 11+ found on PATH or named by `JAVA`.

The skill's own launchers need no dydo. The POSIX launcher selects Node 22.18–22.x or
23.6+, Bun 1.1.5+, or Deno 2+; the PowerShell launcher selects Node with the same bounds
and needs a local Java executable. The pinned TLC jar is 1.7.4, SHA-256
`936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.

Download the jar from
https://github.com/tlaplus/tlaplus/releases/download/v1.7.4/tla2tools.jar
into a task-local directory and verify that hash. Set `TLA2TOOLS` to it and `JAVA` to
the chosen Java executable before invoking the launcher. Both paths may contain spaces.
Path-valued overrides resolve from the caller's directory; a bare `JAVA=java` uses PATH.
A `CommunityModules.jar` beside the TLC jar is rejected. Use a fresh output folder per
run. In restricted environments, run the required local executables with the host's
normal approval mechanism; a blocked launch is not a proof.

## Checking transport

TLC runs through the loopback RMI bootstrap in `scripts/java/`, whose sealed identity is
`scripts/java/build-lock.json`. A missing or changed helper fails the check; there is no
raw TLC fallback. SANY parsing runs directly. The build-only JDK is not a runtime
requirement. Through the helper, TLC listens only on `127.0.0.1`; raw TLC listens on
every interface.

## Tests

Rendering uses Pele 1.9.0 as specified in [render](render.md). Tests additionally use
fast-check 4.5.3, and the renderer tests install Pele, Mermaid 11.16.0 and jsdom 26.1.0
under `XDG_CACHE_HOME`; point it and npm's cache at task-local directories. The package
test command is in `scripts/package.json`; it serializes test files with
`--test-concurrency=1` on a shared host.

A skipped test is missing proof, not a pass. Without `TLA2TOOLS` naming the pinned jar,
the replay suite, checker and bootstrap tests and real-TLC acceptance skip. Native
renderer and SVG acceptance need `STATECHART_PELE`, the installed Pele module entry.
`STATECHART_EVIDENCE_DIR` optionally retains acceptance inputs, raw TLC output,
reports, charts and SVGs. `scripts/test/acceptance.feature` is wired by
`acceptance.test.ts`.

The upstream CLI/replay and recording tests require POSIX executable shebangs, process
groups and signals; on native Windows they skip. Native Windows acceptance uses real TLC
through PowerShell, Git Bash (`STATECHART_BASH`) and Node.
