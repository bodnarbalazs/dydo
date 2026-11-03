---
area: project
type: reference
---

# RMI entry audit - source checkpoint only

Authority is the [captain pre-code pin](https://linear.app/bodnar-balazs/document/dyd-415-rmi-bootstrap-pre-code-pin-and-windows-access-checkpoint-4da014a5815f).
The retained [export-site inspection](export-sites.json) and
[entry inspection](entry-audit.json) read the exact TLC1.7.4 jar as ZIP/class bytes.
They did not execute inspected code, a JVM, compiler, test or collector.

## Pinned input and entry classification

The inspected jar has SHA256
`936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.
The decoded constructor/export references identify three classes calling
`UnicastRemoteObject.<init>()V`: `tlc2.tool.fp.FPSet`, distributed `TLCServer`
and distributed `TLCWorker`. The distributed server also calls the one-argument
`LocateRegistry.createRegistry(int)`. No decoded explicit per-object RMI
factory constructor/export overload or direct server-socket creation was found
by that scan. The raw `SocketFactory` matches include unrelated bundled
JavaMail client factories; those are not RMI export sites. This is a finite
reference audit, not a general dynamic call-graph or sandbox proof.

`tlc2.TLC.process` selects local `ModelChecker` and
`FPSetFactory.getFPSetInitialized` for the supported BFS invocation. The source
argv fixes `-workers 1 -fp 0 -lncheck final`; generated configuration supplies
model semantics, invariants/properties, action constraints, metadata and dump
paths. The public chart interface does not pass arbitrary TLC launch options.
No supported call selects the separate distributed server/worker main classes,
simulation, DFID or debug mode. The bundled FPSet implementations inherit the
same no-argument exporting superclass path. `FPSetConfiguration` does consult a
system-property-selected implementation and `FPSetFactory` loads an implementation
class: its ordinary classpath must stay restricted to the sealed helper and
pinned TLC inputs. This is not assurance for additional classpath/agent injection.

`StateMonitor.main` is a separate JMX client entry using
`JMXConnectorFactory.connect`; it is not selected by statechart. Local TLC
registers its model-checker management wrapper. That does not establish an
externally enabled JVM management agent: injected JVM management options are
outside this source-only transport boundary. The observed fingerprint-set
constructor, not an assumption about JMX, explains the ordinary RMI export.

`run.ts` verdict and ChartRun, native `StatechartReady.TlcAsync`, and the
maintained `test/tlc.ts`, corpus and differential TLC routes now name
`dydo.statechart.LoopbackTlc`. SANY remains direct
`tla2sany.xml.XMLExporter` or native `tla2sany.SANY`. Entry inspection found no
RMI references in those two SANY classes; it is not a transitive claim about
arbitrary supplied modules. SANY performs semantic extraction, while the audited
fingerprint-set export belongs to TLC BFS checking.

## Material input-boundary gap: standalone TypeScript

`Services/Statechart/StatechartPrerequisites.cs:Tlc` rejects an explicit jar whose
raw hash is not the pinned TLC hash. The installed/native path sets `TLA2TOOLS`
on its owned product child from those verified dependencies.

In contrast, `scripts/run.ts:findTlc` returns `path.resolve(TLA2TOOLS)` without
checking bytes. `tlcInvocation` checks the helper against its sealed lock but
only appends the caller's jar to the classpath. Therefore standalone source/PS
checking can select a different jar. A different `tlc2.TLC` implementation could
export through an explicit per-object factory, outside the global factory's
control. No such jar was created or executed: this is a concrete source
selection gap, not a runtime counterexample or arbitrary-extension sandbox claim.
The affected standalone checking boundary is **not established** and remains
held; do not describe all supported launches as pinned or loopback-proved.

Closing this in `run.ts` is within source ownership, but preserving genuine
replay assertions needs additional test-path authority: `test/cli.test.ts` sets
the deliberately nonexistent `TLA2TOOLS="tla2tools.jar"` in two replay launch
fixtures. `test/adaptation.test.ts` also exercises synthetic mise discovery.
Those paths are outside this pre-code pin's affected-test list. A production
test-mode/hash bypass would violate the invariant, so none was added. Captain
must pin the exact replay-input/test treatment before that boundary changes.
The existing direct callers and new launch-resolution tests do not assert that
an arbitrary jar is safe; they cannot stand in for the missing rejection case.

## Factory limits and fail-closed behavior in authored source

The two original product classes use the public `RMISocketFactory` API. The
server constructor takes explicit IPv4 `127.0.0.1`, never a wildcard fallback.
The client rejects all names except numeric `127.0.0.1` and `localhost`, mapping
both directly to the numeric constant. Existing global factory, installation,
bind and connection errors propagate. After successful setup the bootstrap
passes the original `String[]` unchanged to TLC; there is no alternate checker.
Compilation, actual bound addresses, local RMI exchange, exits and semantic
fixtures remain unexecuted. Advertised hostname alone is not the bind invariant.

OpenJDK API/implementation source witnesses retained by the captain are:

- RMISocketFactory source SHA256 `04d58d81a97c3499cca535ec61b594b1ab86a3cb4da33389db23dd935b417e36`.
- TCPEndpoint source SHA256 `4b5a6a21073693e192743adbdf2bd7479803caa54c8120e4d397bdb935779f22`.

The public factory affects the default endpoint path; an explicit per-object
factory takes precedence. Arbitrary Java model extensions, altered jars,
wrappers, JVM agents and external JVM management activation are not sandboxed.
No OS policy, firewall changes, security manager or private reflective hooks
were introduced. Stable Java reuse reduces executable-path churn but does not
prove absence of first-use security prompts.
