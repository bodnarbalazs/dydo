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

## Held local transport draft

The [Java source and build recipe](../scripts/java/readme.md) describe the
statechart-only loopback RMI bootstrap authorized for source authoring by the
[captain pin](https://linear.app/bodnar-balazs/document/dyd-415-rmi-bootstrap-pre-code-pin-and-windows-access-checkpoint-4da014a5815f).
The earlier source-only checkpoint had an unbuilt lock and no helper artifact.
TLC calls require a sealed helper and cannot fall back to
raw TLC. SANY parsing remains direct, separate from TLC's BFS fingerprint export.
The build-only JDK is not a runtime installation requirement.

Desktop Java, compiler, readiness, installer and checker launches remain held.
Normal host command approval does not authorize firewall or administrator dialogs.
An isolated POSIX source/build proof cannot settle Windows prompt behavior.
The initial source checkpoint recorded a missing native-equivalent pinned-jar
check in the standalone TypeScript override. Its successor below records the
authorized source correction; execution remains unproved.

The [closure source pin and B1 result](../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031a/result.md)
record the successor draft: explicit, discovered and direct TLC inputs now require
the fixed jar digest, with actual pinned-jar replay data and rejection tests.
Those changed Node tests remain unrun. B1 failed before FIFO admission in a
sandbox-redirected namespace; no helper tests, compiler, Java or container ran.

## Checker data isolation draft

The [runtime isolation source pin](https://linear.app/bodnar-balazs/document/dyd-415-runtime-checker-jar-isolation-source-ownership-and-input-915cb37a9408)
authorizes the source boundary recorded in the
[source checkpoint](../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/runtime-isolation/checkpoint.md).
Explicit, discovered and direct checker inputs require the fixed official digest.
A file, directory or link named `CommunityModules.jar` beside the supplied or
resolved jar is an unsupported unpinned dependency and produces an error before
checking. No companion is downloaded, modified or silently omitted. The retained
accepted recipes declare no companion; this does not describe every user model.

After outer scratch ownership is established, native readiness and TypeScript
checking create a new plain `checker` child containing only a byte-identical
`tla2tools.jar`. Each SANY or TLC launch rechecks the original input, destination
hash, plain ancestors and exact single-file membership. Java and its runtime
tree are never copied by this boundary. The compiler-only exploded view is a
different representation and cannot become a runtime input. Original model
paths, caller configuration interpretation, `TLA-Library`, XMLExporter's `-I`,
and checker working directories retain their meanings. Existing lifetime owners
retain cleanup authority; the data helpers have none.

B1b's twelve helper tests passed before the first compiler failed on the TLC
manifest's missing classpath sibling. Those receipts remain historical. The
runtime isolation and newer Node/native tests remain unexecuted. The later B1c
compiler result and artifact binding are recorded below; all desktop Java,
Windows access and Java measurement holds remain.

## Inspected build binding

[B1c](../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031c/result.md)
passed 21 helper tests and produced two byte-identical, warning-clean release11
builds. The candidate helper is 1,825 bytes, SHA256
`1772e71e2d82f470f115fa7e6d63205c388f84a0fa55537f120b9d53f3d3fbfa`,
with only the two major55 classes. The canonical lock records its immutable
receipt and the distinct pre-sealing lock identity. The
[binding checkpoint](../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/seal-source/checkpoint.md)
lists the explicit payload row delta; no dependency version or unrelated row is changed.

This is built-not-runtime-validated. The finite B2 transport/forwarding source
packet awaits captain inspection before execution. It cannot establish real
TLC/model semantics, installed readiness, NativeAOT integration, managed JRE
behavior or prompt-free Windows operation. Existing PowerShell paths remain.
