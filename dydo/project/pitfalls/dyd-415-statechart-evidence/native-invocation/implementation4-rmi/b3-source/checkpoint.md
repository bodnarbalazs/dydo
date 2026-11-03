# B3 finite real-checker preparation — no execution release

code-writer/model: gpt-6.1-sol/high. Same author, retained draft, HEAD
`6e3fe7e8018130af6b8586ef0cf40c1474740864`. Authority is the
[captain B3 brief](https://linear.app/bodnar-balazs/document/dyd-415-b2-observed-limits-and-b3-real-checker-source-preparation-96d3fc708d2a).
No production source, dependency/build lock, payload or model/expected-output
bytes change. No commit or master integration occurs. B1/B2 are not rerun.

[Source pin](source-pin.json), [inputs](inputs.json), [commands](commands.json),
[expectations](expectations.json), [dependencies](dependencies.json) and
[test inventory](test-inventory.json) define four separately selectable groups.
Every group requires a later captain release naming its group and exact source
digest. `execution-release.json` is deliberately absent. TEMP driver and normal
launcher counterparts contain the same source bytes as this packet. No proof
module was imported, and no test/discovery, Java, Node, compiler, installer,
download or Docker command ran during preparation.

| Group | Finite scope | Preparation result |
| --- | --- | --- |
| A | Four Transport configs, raw official reference then sealed bootstrap: eight JVM invocations maximum | Prepared independently of Node; source expectations await actual proof |
| B | Existing checker16 and bootstrap12 filesystem/mock tests in two serial Node commands | Blocked: retained pinned Linux Node archive unavailable |
| C | Exactly four named corpus cases, short temporal loop and cleanup case: six selected tests | Blocked by the same Node prerequisite; no skip/pass substitution |
| D | Existing B2 Python12 plus new B3 Python12, each through native unittest discovery | Prepared independently of Node; 24 source-declared cases, no observed discovery |

## Dependencies and custody

The retained Temurin JDK21.0.8+9 linux-x64 archive is reused at its independently
pinned digest; source-only hashing confirms its bytes. The unchanged official
TLC1.7.4 jar and sealed helper retain digests `936a2620…` and `1772e71e…`.
The B1c receipt `60498308…` and B2 source pin `1b4e8df4…` remain immutable.
The cached Python image identity is inherited from B2's actual receipt; this
checkpoint does not query Docker or claim newly observed image availability.

A bounded read-only search of retained normal-TEMP `dyd415*` scopes visited
15,727 directories at maximum depth7, pruning source/snapshot/build outputs and
dependency package trees. No Linux Node archive candidate was found. This is a
bounded retained-input finding, not a whole-disk absence claim. Groups B/C need
the commissioned `node-v22.23.2-linux-x64.tar.gz`, SHA256
`b294a556e639d64338823920e5866c21c02741742d2e1529ee1a225c1ec9252a`.
A path or host Windows Node is not a substitute. No new download/adoption is
authorized. The host driver refuses B/C before booking; a retained Node input
and a new dependency/source inspection pin are required before their release.
A and D are not blocked by that absence.

All maintained B3 Python modules have genuine tests in
`DynaDocs.Tests/coverage/tests/test_statechart_rmi_b3.py`. The existing B2 test
file and all twelve cases remain byte-identical. Only five real associations
are added to the existing manifest. Its exact old bytes are preserved under
`before/test-associations.json`; the earlier B2 source pin still describes those
original bytes. [Association delta](association-delta.json) explicitly maps this
authorized current-manifest change; no historical pin is rewritten and no
blanket custody exemption is used. [Preserved inputs](preserved-inputs.json)
bind all other unchanged draft/source/history bytes and B2 raw outcomes.

## A: semantic parity and passive observation

Eight exact argv arrays are pinned. Each invocation gets a new registered
case-owned directory with a plain `checker` child containing only a byte-exact
copy of official checker DATA, no companion. Original model cwd, absolute
config/module inputs and `TLA-Library` remain `/input/models`; temp/metadir are
disjoint case outputs. Raw official `tlc2.TLC` is an explicitly labelled Linux
reference control. Bootstrap uses only the sealed helper and that case's
official jar. Neither B2's stub nor the exploded compiler view is present.
There is no runtime/JRE copy or supported raw-checker fallback.

Both variants use one-worker BFS, fingerprint0, final liveness checking and
existing deadlock/config behavior. The exact official EC ExitStatus class
constants and retained corpus exit receipts are recorded as source evidence.
Required facts are good/TypeOK completion with four distinct states;
BelowTwo invariant exit12 and shortest `0 -> 1 -> 2` via Next; NeverFour exit13
with the fair `0 -> 1 -> 2 -> 3 -> 0` temporal cycle; action-bound exit0 with
BelowTwo holding and only two distinct states. Reference/bootstrap comparison
includes generated/distinct counts, actual action labels, values, temporal/
invariant identity, back-state and stuttering facts. Exit parity alone cannot
pass. Raw outputs are retained; no raw-output normalization is applied.
Only explicit semantic extraction omits timing/seed/PID/statistics presentation;
it preserves the compared state/action/rule facts. New unexpected outcomes stop
later invocations without retry or expectation correction.

The passive observer samples the actual bootstrap PID's stat, FD targets and
both tcp/tcp6 tables. It matches socket inodes to process FDs, checks PID
starttime across each sample, decodes IPv4 and mapped IPv6 and rejects observed
owned wildcard/nonlocal listeners. A thread takes at most400 samples over four
seconds, with ten-millisecond observation intervals. It does not signal, delay,
instrument, replace or rerun the model. At least one of the same four finite
bootstrap invocations must yield an owned loopback listener. If all finish
without one, that fact is missing proof and the group fails; B2 cannot fill it.

## B/C: actual production TS paths, exact selectors

B uses the unchanged filesystem/mocked-argv cases, with observed expectations
16 and12, zero skipped selected cases and zero real Java launches. These are
not Java behavior proof. Missing/changed helper, source/destination/companion,
links, ownership, discovery and original include/cwd cases remain strict.

C selects only CounterInstance, CounterRules, Shadow and MediaPlayerProps
committed-chart/report cases, the short named temporal-loop case and the named
normal-return/SANY-failure cleanup case. The complete original corpus data and
directory membership are retained in the read-only snapshot so native discovery
does not invent a reduced inventory. Unselected tests must be filtered rather
than executed; selected skips or absent selected names fail. There is no
`STATECHART_UPDATE`, replay, rebaselining, full corpus run or optional renderer
download. The selected CLI/worker path imports local compiler/Mermaid modules,
not the Pele SVG renderer; MediaPlayerProps' view has no external imports.

All model/library/view/expected output bytes are pinned, including Login used
by cleanup and the dynamic Cycle/Broken model definitions in corpus.test.ts.
Committed report/chart equality stays in the existing tests. CounterInstance
requires its C!Up/C!Back loop, CounterRules qualified M!Jump and Step paths,
Shadow local/instance name semantics and initial-only notices, and
MediaPlayerProps state/view/temporal facts. The short loop asserts three repeats
and its original report path; cleanup requires no leftover owned `statechart-`
folder after success and SANY failure.

C uses the real existing `test/java.ts` recording shim for the four corpus
cases and a proof-only `java_record.py` exec shim for the actual JDK endpoint.
The latter retains exact argv/cwd/PID, forwards args unchanged, copies no Java,
and atomically caps real Java launches at64. Product SANY stays direct
`tla2sany.xml.XMLExporter` with original `-I`, `TLA-Library` and absolute model
input; TLC goes through the real TS isolation helper and sealed bootstrap.
No native SANY readiness claim follows. The source tar assigns executable modes
only to those two recording scripts; no canonical source mode/byte is changed.
Node subprocesses use only the later pinned Linux binary on owned child PATH.
Per group the ceiling is A8 JVMs, B2 Node test processes/zero JVMs, C1 top-level
Node test process with the six source-bounded tests and at most64 JVMs (including
recording shim execs), D2 Python unittest commands/zero JVMs. No broader top-level
commands or automatically selected sibling groups are permitted.

## Owned resource recipe and remaining limits

Each future group is a separate single launcher invocation and disjoint output
root. Actual host LOCALAPPDATA/TEMP/default-live namespace guards, readable LC
hashes, inherited ticket semantics, FIFO -> nonwaiting Heavy -> exclusive owned
TEMP lock remain. No environment spoof, private namespace or foreign cleanup.
The worker clears ambient Java/Node injection/replay/update variables in its own
children and supplies only pinned env/PATH/CWD/classpaths. All host children are
hidden; the launcher remains alive until the driver exits.

Exactly two fresh nonce-labelled containers and one owned toolchain volume are
planned per group: extraction, then finite worker. The first extracts the
retained JDK and a tar of hash-checked source data into the owned volume. The
worker mounts that volume read-only and verifies source membership identities
and the JDK against its archive. Node extraction is not enabled while its input
is unavailable. Container root and `/input` are read-only, `/work` and `/output`
are disjoint writable owned paths, no home/project is writable. The pinned
Linux/amd64 image, pull-never, network-none, no ports/host namespaces/privilege/
socket, all caps dropped, no-new-privileges,128PIDs/1GiB/twoCPUs remain. Each child
has120seconds and32MiB output bounds; the host container stage has1200seconds.
Owned process groups are drained on timeout/output failure. Host cleanup is
independent of worker success, verifies actual IDs/nonce, removes only owned
containers/volume and confirms absence. Source, raw outputs and scratch data
are retained; uncertain creation/cleanup retains the owned TEMP lock.

A's manually isolated data directories do not exercise the production TS
isolation helper. B tests that helper with inert checker data; C runs it with
real SANY/TLC. None is npm/native/installed delivery or managed JRE setup proof.
Desktop Java/javac/install/probes remain held, no isolated Windows target is
established, and Java assurance remains unavailable without waiver. Existing
native filesystem20, lifecycle, platforms, installed, final static/coverage,
current-master and independent-review obligations remain. No PS retirement,
readiness, PR/push/watch, version/release/global change or settled-thread contact.
