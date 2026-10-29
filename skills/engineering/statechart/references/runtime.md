# Runtime and provenance

This folder vendors `pstack/skills/statechart` from `cursor/plugins` at
`1ba91fc25c0977741dd0e267849e6234b037ed0a`. The adjacent [LICENSE](../LICENSE)
preserves MIT Copyright (c) 2026 Lauren Tan. [Provenance](../PROVENANCE.md) records local changes.

The POSIX launcher selects Node 22.18–22.x or 23.6+, Bun 1.1.5+, or Deno 2+.
The PowerShell launcher selects Node with the same Node bounds. Native Windows needs
PowerShell and a local Java executable. Java 11+ can run TLC; the optional mise default
is Temurin 25. The pinned TLC jar is 1.7.4, SHA-256
`936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.

Download the jar from
https://github.com/tlaplus/tlaplus/releases/download/v1.7.4/tla2tools.jar
into a task-local directory and verify that hash. Set `TLA2TOOLS` to it and `JAVA` to
the chosen `java.exe` before invoking the launcher. Both paths may contain spaces.
Path-valued overrides resolve from the caller's directory; a bare `JAVA=java` uses PATH.
Use a fresh output folder per run. In restricted environments, run the required local
executables with the host's normal approval mechanism; a blocked launch is not a proof.

Rendering uses Pele 1.9.0 as specified in [render](render.md). Tests additionally use
fast-check 4.5.3, Mermaid 11.16.0 and jsdom 26.1.0. Set `XDG_CACHE_HOME` and npm's cache
to task-local directories when running the renderer tests. The package test command
is in `scripts/package.json`; it serializes test files with `--test-concurrency=1`
on a shared host. Coordinate the suite reservation first.

The upstream CLI/replay and recording tests require POSIX executable shebangs, process
groups and signals. On native Windows these cases report explicit skips. Native acceptance
uses real TLC through PowerShell, Git Bash and Node; differential and plain-TLC tests also
run against Java. POSIX tests must still run on a POSIX host before claiming cross-platform
proof. `scripts/test/acceptance.feature` is wired by `acceptance.test.ts`.

For native acceptance set `STATECHART_BASH` to Git's `bash.exe` and `STATECHART_PELE`
to the installed Pele module entry. `STATECHART_EVIDENCE_DIR` optionally retains inputs,
raw TLC output, reports, charts and SVGs. The feature runner requires explicit environment
prerequisites and reports a skip when they are missing.
