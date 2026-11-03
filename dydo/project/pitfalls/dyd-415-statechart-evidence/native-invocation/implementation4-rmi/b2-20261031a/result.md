# B2 — nine contained socket/forwarding outcomes matched

code-writer/model: gpt-6.1-sol/high. HEAD remains
`6e3fe7e8018130af6b8586ef0cf40c1474740864`; retained draft, no commit or integration.
This is the one invocation released by the
[captain](https://linear.app/bodnar-balazs/document/dyd-415-b2-inspected-contained-socket-and-forwarding-execution-release-5b674e4936cf).
The immutable [source pin](../b2-source/source-pin.json) remains SHA256
`1b4e8df4ebe96d01f016823a5c0c393eb84b2b113ffb5f13b2e2c1e48764529e`.
No admitted input was edited or retried.

The normal actual-host approved command was
`python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b2-launch-20261031.py`.
[Launcher preflight](launcher.preflight.json) records actual Windows host
LOCALAPPDATA/TEMP and default live LC namespace. [Admission](admission.json)
records ticket `0000000001-abdfaa850ff27445`, owned FIFO booking, no inherited
ticket on entry, and nonce `116e7fdb6841465c`; child commands inherited the booked
ticket. Material work used FIFO, nonwaiting Heavy and the owned TEMP lock.
Both launcher and driver exited0. All host children were hidden.

Exactly two fixture compiles exited0 with empty stdout/stderr, using the pinned
build-only Linux JDK21.0.8+9 and release11/warnings-as-errors/no-processors flags.
The product helper was not rebuilt. [Fixture identities](retained/output/fixture-members.json)
record exactly three probe classes and one separate control class, all major55.
[JDK identities](retained/output/jdk-verified.json) match B1c's verified archive
and executable/module facts. [Outcome binding](outcome-pin.json) retains exact
compiler/process argv and raw-result hashes against the pre-execution plan.

| Process | Actual exit | Observed result |
| --- | --- | --- |
| bound | 0 | Numeric request41/reply42 and localhost request43/reply44; owned loopback listener |
| rmi | 0 | Local RMI echo round trip; owned loopback listener; owned object unexport assertion completed |
| nonlocal | 1 | Exact nonlocal-endpoint IOException; no TLC control entry |
| bind-failure | 1 | Address-already-in-use BindException; no fallback/control entry |
| existing-factory | 1 | Explicit cannot-replace IOException; control never entered |
| repeat-install | 1 | Factory-already-defined SocketException; no control entry |
| forward-0 | 0 | Exact six-argument order/UTF-8 bytes and stderr |
| forward-7 | 7 | Exact six-argument order/UTF-8 bytes and stderr |
| forward-throw | 1 | Exact argv, original stderr and IllegalStateException identity |

The [bound raw observation](retained/output/bound.stdout) records PID33,
FD6/socket inode259818, `127.0.0.1:35767`, represented as
`0000000000000000FFFF00000100007F:8BB7` in tcp6. The
[RMI raw observation](retained/output/rmi.stdout) records PID46,
FD8/socket inode265738 and `127.0.0.1:34643`, represented as
`0000000000000000FFFF00000100007F:8753`. Both observations retain both socket
tables, process stat and FD identities. Each contained one observed process-owned
IPv4-mapped loopback listener; no wildcard/nonlocal listener was observed.
Network-none alone was not used as the bind proof. Raw stderr preserves the
four expected exceptions; nonzero exit alone was not accepted.

Forwarding controls used only the byte-identical sealed helper and the disjoint
control class directory. They checked an empty argument, a space argument,
non-ASCII text, option-like text and the remaining arguments in exact order.
They are control-fixture evidence, not real TLC semantics. Neither the official
checker jar nor its compiler-only exploded representation was a B2 runtime
classpath input. The helper remains SHA256
`1772e71e2d82f470f115fa7e6d63205c388f84a0fa55537f120b9d53f3d3fbfa`;
B1c receipt SHA256
`60498308cfb5290f093a453e0ab20f4378ed9d35e1624304aa8d75ddad12c8ac`
remains unchanged.

The two new owned containers used the pinned cached Linux/amd64 Python image,
network-none, read-only root/input/toolchain after extraction, disjoint owned
work/output, no published ports/host namespaces/privilege/socket, cap-drop ALL,
no-new-privileges and the pinned resource bounds. Their raw create/inspect/start/
state receipts are retained. [Teardown](teardown.json) records both exact owned
IDs and the labelled JDK volume removed; all three absence stdout files are
empty with exit0. [Secondary release](secondary-released.json) and
[FIFO release](released.json) confirm Heavy, matching TEMP lock and FIFO release.
Task-local scratch remains as data; no scratch-deletion PASS is claimed.
[Post-custody](post-custody.json) and [custody binding](custody.json) retain exact
unchanged source/history and output identities.

No twelve-case Python contract test/discovery, product compile, real TLC/model,
SANY, native/TS/installed/readiness/platform or broader campaign ran. This isolated
JDK proof does not prove managed JRE/IDE execution or prompt-free Windows behavior.
Desktop Java/install/probes remain held; isolated Windows access and adopted
Java measurements remain unavailable without waiver. Real checker/model/library/
chart, lifecycle, installed, static/coverage, platform, independent-review and
current-master obligations remain open. No PowerShell retirement, release/version
bump, publication, PR/push/watch, security/global change or settled-channel contact.
