# Source checkpoint: bounded signal diagnosis and separate failure containment

code-writer/model: gpt-6.1-sol/high. Captain/records only.

Prepared under the [daytime captain contract](https://linear.app/bodnar-balazs/document/dyd-415-daytime-bounded-signal-diagnosis-and-failure-containment-480155d05caf), read fully.
Same retained worktree/branch, HEAD6e3fe7e8018130af6b8586ef0cf40c1474740864 and authorized dirty draft.
This is source preparation, not an execution release or runtime result. No production/canonical-test/payload/association/history edits.

## What changed in this successor

The previous blocked packet e7b93e25a63831fbbb3030f909215f7325405c15984d369e5ad2e42c8bd02209 stays byte-identical.
This separate packet adapts its actual-CLI, one-signal diagnostic and the existing owned-container envelope.
It does not implement a product or canonical-test correction.

The original fs.watch callback remains the only proof-signal send route.
The waiting view still watches the original proof directory, writes ready and awaits indefinitely.
An outside-directory creation log, independent2ms readiness observation, callback entry,
real signal-send attempt/result, scratch-only original-handler entry, PID/starttime/PPID/PGID/session/argv
and actual child exit/close are separate witnesses. No handshake is proposed.
Exact generated waiting-view content/hash for each signal is in input-manifest.json.
instrumentation.diff shows the original canonical fixture versus scratch-only diagnostic, and prior versus current scratch preload.
Canonical tests are unexecuted reference DATA; no import/discovery is performed.

The preload forwards the original handler, this and arguments and observes actual ChildProcess objects.
It never fabricates signal delivery, close or reaping.
Logging and2ms observation alter timing; logging failure cannot suppress the original handler.
A ready file cannot prove callback delivery. A successful send return cannot prove handler execution.
A missing handler without send evidence remains ambiguous.
No original scheduling/libuv order or Docker/inotify root cause is inferred.

## Product outcome and failure containment

Original assertions remain: real SIGINT130/SIGTERM143 with null terminating signal,
empty stdout, exact stopped-the-check stderr, no published output/staging/tmp,
actual janitor identity and SIGKILL close before controlled exit, worker completion,
successful cleanup-return paths absent. PID absence is supplemental after close.

Each original boundary is30s. On failure the diagnostic closes its watcher/timers,
then bounds controlled owned-CLI SIGTERM5s, source/identity-proven descendant group cancellation5s,
and exact direct-CLI SIGKILL/owned-pipe closure/actual-close observation5s.
PID/starttime/ancestry/argv/PGID/session must justify descendant signaling.
No unproved PID is killed. Missing actual close remains FAIL/INCONCLUSIVE.
Only on an already-failed path may the direct handle be unrefed after owned pipes close,
so the async diagnostic can settle; that does not certify close or reaping.
No cleanup result can turn a prior case failure into PASS.

The native50s and Python65s+5s runner bounds are emergency paths, not ordinary case cancellation.
Python records raw result and unchanged strict census when available.
Any other live, orphan or zombie PID remains rejecting; no exemption/init/subreaper.
The actual CLI has TWO sibling direct children: worker and janitor; janitor targets worker.

The driver finally independently attempts existing exact-ID/nonce-owned stop/remove/host-absence
and exact-labeled volume removal. That is FAILURE CONTAINMENT only.
Product outcome is retained separately in product-outcome.json (or host stop.json on earlier infrastructure failure).
emergency-containment.json never supplies product close or strict clean-census proof.
Its operations do not depend on ordinary evidence writes. If evidence cannot persist or absence cannot
be established, return concrete uncertainty and retain the exact normal TEMP ownership lock.
FIFO/Heavy context managers release normally; no manual queue/lock artifact removal.
Owned scratch/raw evidence remains retained.
Host death, daemon loss, filesystem failure or privileged interference cannot be made into a teardown guarantee.

## Finite commands, not released

commands.json pins two alternatives, each exactly ONE invocation selecting ONE signal:
python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-signal-containment-launch-20261031.py --signal SIGINT
python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-signal-containment-launch-20261031.py --signal SIGTERM

Future release.signals must equal the single selected signal, not a two-case campaign.
After one result, captain decides whether the other boundary is necessary.
No automatic chain/retry, Counter, ten partial ok cases, B28,22-contract or real-TLC C repetition.

Per signal: one owned DATA-preparation container and one proof container.
Only pinned Node bin/node is materialized from the retained official archive, inside the owned volume.
The53 source/DATA files and exact archive member inventory are checked.
No Java/JDK/npm/host runtime copy, network/download/install.
STATECHART_RECORD/UPDATE/PLAIN/ambient injection are absent; the fixed replay shim cannot select real Java.
Official checker/helper jars remain DATA only.

Bounds: original30s; cancellation5+5+5s; native50s; Python runner65s plus5s direct reap;
each container attach120s emergency; Docker administrative/containment commands20s each;
at most16 emergency cleanup commands,340s planning ceiling.
Container regular files have16MiB RLIMIT_FSIZE and captured CLI strings are bounded;
host Docker responses have finite command/contained-output bounds, not an OS host filesystem quota.
Timers require a responsive event loop; loss of responsiveness reaches runner/attach containment
and stays failed/inconclusive. No broad1200s ordinary cancellation.

## Containment identity and remaining administrative prerequisite

shared-containment.py is byte-exact b3-source/driver.py24f0afe8f437cef9dc0fc8a3d5cf98540f39af0bf6f45e129f12c2e53549c100.
Its JDK/reference/model/suite helpers are not called.
The successor tightens pre-start checks and makes exact-owned cleanup independent of log writes;
it adds no supervisor, framework, service or metric mechanism.

Both future containers use existing cached Python linux/amd64 image
sha256:6a9220edda062e9223ea0784740a535987299420b96fbfd520b89bbd3e772485.
Before each create/start, bind image, exact64hexID/name/nonce, private PID namespace,
private IPC, network none, read-only root/input/proof volume, cap-drop ALL,
no-new-privileges,128PIDs/1GiB/2CPUs, no privileges/host devices/ports/socket/control route,
and exactly four mounts with exact owned sources/types/RW/propagation.
No host/other-container PID namespace, init, restart, extra volume or foreign writable source.
Only /toolchain is writable during preparation; proof volume is read-only.

Actual LC live namespace/ticket identity, FIFO -> nonwaiting Heavy -> exclusive normal TEMP
dydo-fullsuite.lock remain unchanged. Host namespace guards refuse redirected LOCALAPPDATA/TEMP.
Complete Python/import-origin/namespace-package and literal TS source closure are in closure.json.

A retained Docker daemon ID/current-context record was NOT found.
A Docker protected-config source read was blocked before execution; no workaround was attempted.
No Docker or daemon/context probe was run here.
The future driver FAILS CLOSED before any engine resource operation unless captain supplies
an actual read-only dockerIdentity tuple in the administrative release: exact executable hash,
already-current contextName, local named-pipe dockerEndpoint and all seven nonempty daemonFields.
It rechecks current context/endpoint/daemon plus cached image before starts.
Explicit --context selects only that already-current pinned context, never changes config/transport.
This is a remaining source-release identity prerequisite, not a claim Docker access is unavailable.
Captain inspection and separate ADMIRAL execution disposition are still required.

## Custody, assurance and next disposition

inventory.json and before-custody.json preceded source authoring.
before-custody.json binds6831 preexisting tracked/untracked draft and RMI/evidence files.
preserved-inputs.json and historical-applicability.json are byte-exact copies of the prior5266-row
manifest and its original two-file snapshot mapping; expectations were not regenerated.
The current production/test identities remain those frozen by correction pin30bbe7be70c9482971410d1ddef6764481da0828237c5d1d3a6096f8ec73efce.

Two exact optional TEMP source counterparts were exclusively created and hash-checked, never run.
The reserved optional assembler remains absent.
source-pin.json binds final source/fixture/import/command/dependency identities;
after-custody.json records the final independent read-only checks.
No execution-release or run/output directory is created.

Shell command-analysis rejected two manifest-authoring commands before execution;
direct file patches completed those manifests instead.
Read-only AST parsing and hashes import no project module and execute no generated program.
Only source-copy authoring used normal actual-host approval; no runtime capability was exercised.

New wrappers' genuine direct-contract tests/native discovery/static/coverage remain OWED.
assurance-proposal.json records actual-method association intent only; no manifest/policy edit or fake bridge.
Old B28/22 and mock controls do not certify these wrappers or Java semantics.
No fresh review is self-commissioned; captain owns review after focused GREEN.

Original C FAIL, attribution RED and focused FAIL/incomplete remain immutable.
Counter normal-drain evidence is narrow; ten partial ok records are not GREEN.
Abrupt-parent/PythonPID1 proof remains unestablished and is not selected.
Desktop Windows Java HOLD, isolated Windows/prompt, Java assurance and all installed/native/npm/platform/
static/coverage/current-master/final independent review gates remain.
No readiness, PS retirement, release/version/tag/publish/rollout, commit/PR/push/merge/watch or crew.
Both settled HITL threads remain NO CONTACT. Return captain/records only.

