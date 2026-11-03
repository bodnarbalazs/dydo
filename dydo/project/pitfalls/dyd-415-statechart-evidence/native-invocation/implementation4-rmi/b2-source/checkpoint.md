# B2 finite socket/forwarding source packet — not released

code-writer/model: gpt-6.1-sol/high. Same author and retained draft, HEAD
`6e3fe7e8018130af6b8586ef0cf40c1474740864`. The
[captain pin](https://linear.app/bodnar-balazs/document/dyd-415-inspected-b1c-artifact-binding-and-finite-b2-source-checkpoint-52e50ff5ac1a)
authorizes this source checkpoint only. No execution or discovery occurred.

[Source pin](source-pin.json), [container inputs](inputs.json) and
[command/outcome inventory](commands.json) bind actual bytes, concrete compiler
and runtime argv, disjoint classpaths, exact diagnostic/exit expectations and
retained input locations. [Preserved inputs](preserved-inputs.json) bind unchanged
history and the current source draft outside the explicit sealing delta.
`execution-release.json` is deliberately absent: the launcher and driver require
an explicit `RELEASED_B2` record naming this exact source-pin digest before
material work. No environment variable substitutes for captain release.

The sole [host driver](driver.py), [normal-file launcher](launcher.py) and
[contained worker](worker.py) are maintained Python. Normal TEMP counterparts
of driver/launcher have identical bytes. The launch command, if later released,
is `python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b2-launch-20261031.py`
through the normal approved host-command path. All host subprocesses are hidden.
There is no desktop Java/javac invocation and no remote dispatch or global setup.

The driver rejects redirected `LOCALAPPDATA` or TEMP and checks the actual default
LC live namespace/API and commissioned readable LC hashes. No LC Git metadata
claim is made. It uses `suite_slot.book` with inherited-ticket semantics, then
nonwaiting Heavy, then exclusively creates `TEMP/dydo-fullsuite.lock`. It never
rewrites the environment, supplies a private Namespace, deletes foreign queue
artifacts or waits on Heavy. Source/history/JDK/helper pins are checked before
admission and after owned cleanup. A failure stops the finite sequence without
retry or substitution; any partial raw evidence remains.

The retained hash-pinned JDK archive is reused, not downloaded. Exactly two new
owned containers are planned: archive extraction into an owned labelled volume,
then one bounded worker using that volume read-only. Each gets the exact cached
Linux/amd64 Python digest, `--pull=never`, `--network none`, no ports/host
namespaces/socket/privilege, all capabilities dropped, no-new-privileges,
128 PIDs, 1 GiB, two CPUs and a read-only root. Mounts are the new owned snapshot
`/input` read-only, disjoint `/work` and `/output` writable, and `/toolchain`
writable only during extraction and read-only during the worker. No project or
home writable mount is used. Actual returned container IDs/image/config/labels
are checked before start. Only matching owned IDs and volume may be stopped or
removed; absence is confirmed before releasing the matching TEMP lock. Raw
observations, compiler output, class identities and failed-stage receipts are
retained independently of the worker's final success. Task-local scratch is
retained as data; there is no scratch-deletion PASS claim.

The worker revalidates every input and the entire extracted JDK against the
retained official archive using the unchanged, already inspected build helper's
`verify_jdk` function. It never calls that helper's build/compiler entry point.
It separately compiles exactly three probe fixture files and one control fixture
file with warning-as-error release11 flags, no processors/implicit sources, and
disjoint output directories. The sealed product jar is never recompiled.
`/work/empty` makes the control compile independent of any ambient classpath.
Product classes, control classes and official checker data remain distinct.
Neither the exploded compiler TLC view nor the official TLC jar is a B2 runtime
classpath input: none of these nine controls needs actual checker linkage.
The official jar's retained identity is still checked for custody; B2 makes no
real TLC semantic claim. There is no CommunityModules sibling search in B2.

| Process | Required outcome |
| --- | --- |
| `bound` | Exit0; actual `127.0.0.1` listener; numeric request41/reply42 and localhost request43/reply44; real socket observation |
| `rmi` | Exit0; exported RMI echo round trip; process-owned loopback listener observed; owned object unexported |
| `nonlocal` | Exit1; precise nonlocal-endpoint IOException; no control entry or wildcard fallback |
| `bind-failure` | Exit1; precise BindException for occupied port; no fallback |
| `existing-factory` | Exit1; explicit cannot-replace IOException; control on classpath must never enter |
| `repeat-install` | Exit1; explicit factory-already-defined SocketException; no TLC entry |
| `forward-0` | Exit0; exact empty/space/non-ASCII/option-like argv order and UTF-8 bytes, exact stderr |
| `forward-7` | Exit7; the same byte/ordering/stderr assertions |
| `forward-throw` | Exit1; the same argv plus original control stderr and IllegalStateException identity |

Both `/proc/net/tcp` and `tcp6`, PID, `/proc/self/stat`, FD symlink/socket inode
ownership and raw table rows are retained. IPv4-mapped IPv6 loopback is decoded
using native-endian words. Wildcard/nonlocal listeners fail; observing no owned
listener is missing proof. The Python interpreter also checks these observations.
Network-none alone cannot satisfy the bind invariant. The
[fixture delta](fixture-delta.json) preserves the prior exact SocketProbe bytes
and binds only the added actual client request/reply assertions and bounded I/O
timeouts. Product Java is unchanged. Fixture timeouts bound failure, not test
ordering or a polling/sleep campaign.

The real unittest file
`DynaDocs.Tests/coverage/tests/test_statechart_rmi_b2.py` contains twelve named
contract cases for these maintained drivers: exact classpaths, fixture compiler
inputs, endian/mapped/nonlocal decoding, required owned dual-table evidence,
actual exchange markers, explicit failures, byte-exact forwarding, thrown
errors, redirected host/release guards, containment rejection and foreign-owner
cleanup rejection. Three genuine manifest associations use this existing
discovery root. No fake bridge/collector/policy change was made. Planned later
discovery is `python -B -m unittest discover -v -s DynaDocs.Tests/coverage/tests
-p test_statechart_rmi_b2.py`; it is not part of the nine JVM processes and
requires a separate explicit release. Source counting is not observed discovery.

B2 cannot establish managed JRE/IDE behavior, NativeAOT/npm/installed integration,
actual good/bad/temporal/action TLC/model/library/chart behavior, prompt-free
Windows operation or Java coverage/static metrics. All remain separate owed
boundaries. No isolated Windows target is confirmed, desktop launches remain
held, and missing Java measurements have no waiver. Static/coverage, platforms,
cleanup, review, current-master gates and readiness remain open. No commit,
PowerShell retirement, PR/push/watch or settled-thread contact occurred.
