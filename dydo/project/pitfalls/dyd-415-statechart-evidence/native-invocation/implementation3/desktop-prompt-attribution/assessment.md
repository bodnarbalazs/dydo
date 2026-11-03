---
area: project
type: reference
---

# Statechart desktop-prompt attribution and bounded mitigation assessment

This source-only checkpoint continues the retained dirty POSIX draft on
`6e3fe7e8018130af6b8586ef0cf40c1474740864`. It records a material limit of the
currently pinned TLC launch, not runtime acceptance. No Java, JVM, installer,
probe, copied-runtime launch or dialog reproduction ran for this checkpoint.
The captain's affected-launch hold remains binding.

## Attribution custody

`captain-attribution.json` preserves the captain's packet byte for byte, SHA256
`fc0f86f4a84258beea5cf1ec5ec381290e7a6e20178b416c94992d34b5e102c0`.
Its six readable HKLM firewall rules are TCP/UDP Query User pairs for exactly
three Temurin executable paths: the installer staging directory, the final
managed directory and the copied readiness-test dependency directory. The rules
contain `Defer=User`, inbound Allow and Public profile values. They strongly
support three prompt opportunities; neither their creation times nor individual
dialog screenshots are available. Eventlog/CIM access was denied; no elevation
was requested. UAC, firewall and harness authorization must not be conflated.

The packet binds retained stage admissions at 22:28:17 UTC (managed Temurin),
22:31:52 UTC (installed resume) and 22:38:58 UTC (installed Light), on October30.
These are stage times, not exact Java-launch or prompt timestamps. Exact retained
Java/install argv are in the adjacent historical command receipts:

- `managed-temurin/managed-java-install.command.json`
- `managed-temurin/actual-java-version.command.json`
- `installed-resume/authorized-resume.command.json`
- `npm-counterexample/java-barrier-compile.command.json`
- `installed-light/light.command.json`

Those paths are relative to the parent `implementation3` directory. The barrier
compiler used the existing IDE `javac.exe`; that does not establish the reported
Temurin dialogs' trigger. The captain reported a separate IDE JBR rule pair.
No OS security rules, PATH or unrelated processes were changed.

## Exact pinned checker path

`tlc-route-selected.json` retains offline class identities, raw Code bytes and
instruction-boundary-decoded references from the exact jar SHA256
`936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.
Its manifest names tag `v1.7.4` and revision
`5a47802b5c391f59ecdd44117981f4ff8c0656ba`. No class was loaded or executed.

1. `TLC.process` selects ordinary `ModelChecker` on its `isBFS()` branch.
   The maintained `run.ts` verdict/chart routes and native readiness fixture do
   not select DFID; they request one worker, fp0 and final liveness checking.
2. The configuration-taking `ModelChecker` constructor invokes
   `FPSetFactory.getFPSet` at byte offset16. This happens before worker creation;
   a single worker does not avoid fingerprint-set construction.
3. The factory either constructs `MultiFPSet`, loads a configuration-selected
   implementation, or constructs the default `MSBDiskFPSet`. The custom loader
   requires the result to be an instance of `FPSet`.
4. All bundled breadth-first fingerprint-set implementations, including memory,
   disk, off-heap and multi-set variants, inherit from `FPSet`. Its sole
   constructor starts `2a b7 00 01`; offset1 resolves exactly to
   `java/rmi/server/UnicastRemoteObject.<init>()V`. There is no conditional before
   this superclass call and no socket-factory argument. Selecting another
   bundled implementation cannot bypass it. This is the ordinary RMI export
   mechanism, independently of any JMX configuration.
5. DFID uses a different `MemFPIntSet` hierarchy, but its constructor explicitly
   rejects checking liveness properties and points callers to breadth-first
   mode. It is not a replacement preserving statechart's existing semantics.
   Simulation also selects a different checker operation and is not exhaustive
   model checking. Neither is adopted.

This establishes a source-level obstacle for a no-export route through the
unchanged pinned breadth-first CLI. It does not claim an exhaustive audit of all
upstream versions or JVM configuration, an observed listening socket, or a
one-to-one mapping from a Java launch to each reported dialog.

## Minimal mitigation and limits

The retained source change in `StatechartReadyTests` makes the successful case
reuse the verified fixture's stable dependency root. Only the corruption case
copies dependencies, and its changed renderer must reject before Java starts.
This removes the routine successful test's fresh Java path by construction;
the changed test has not run under the launch hold. Its assertions and strict
before/after dependency-byte comparison remain intact.

Stable runtime reuse limits new executable-path prompt opportunities. It does
not remove initial RMI export in either installer readiness or normal checking.
User-scoped archive installation and hidden consoles likewise do not constrain
that listener. `java.rmi.server.hostname` only advertises endpoint identity and
is not accepted as evidence of a loopback-only bind.

No supported no-listener option was established for the current pinned
breadth-first launch. A source-supported local-only route would have to control
the server socket before RMI export; it would still be a listener and its effect
on Windows prompts is unproved. That requires a separately pinned mechanism and
proof disposition. No wrapper, checker patch, JVM flag, dependency-lock change,
firewall exception, allowance-masked reproduction or acceptance waiver is
implemented or proposed as a proved fix here. The concrete return is source
infeasibility of solving the listener through the current launch's fingerprint
selection or stable-runtime reuse alone.

## Verification limits

`custody.json` binds the copied packet, selected bytecode evidence, offline reader
and the initial source witnesses. `final-source-check.json` binds the final
documentation after narrowing the branch description to the directly decoded
`isBFS()` call. The source-only custody check verifies exact
input hashes and exclusive new-file writes. This is not a product test or a static
gate. No runtime, native, platform, coverage or aggregate static campaign ran.
The POSIX draft remains incomplete and uncompiled; previous raw failures and
historical evidence are unchanged. Affected Java/probe/install sequences remain
held until explicit captain disposition. Static, coverage, PS retirement and
readiness holds also remain.

## Related

- [DR062](../../../../../decisions/062-statechart-invocation-manages-prerequisites.md)
- [Installed milestone evidence](../verification.md)
