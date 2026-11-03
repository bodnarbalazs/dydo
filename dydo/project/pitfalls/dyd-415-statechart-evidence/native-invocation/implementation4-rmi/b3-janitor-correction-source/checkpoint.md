# POSIX janitor correction: source checkpoint, not released

code-writer/model: gpt-6.1-sol/high. Captain/records only. The controlling
[captain pin](https://linear.app/bodnar-balazs/document/dyd-415-authorized-posix-janitor-correction-source-and-proof-pin-7574738690f8)
authorizes only this source checkpoint. HEAD remains
6e3fe7e8018130af6b8586ef0cf40c1474740864 on DYD-415-statechart in the retained
isolated worktree. Its existing authorized dirty draft is preserved; no commit,
integration, test import/discovery, container, Node, Java, build or install occurs.
No execution-release.json or prospective output directory is created here.

## Correction and failure ownership

Only live statechart.ts and the appended section of test/cli.test.ts change.
[Source delta](production-test.diff) is against the byte-exact [before](before/)
snapshots, not against an earlier committed product version. Before-custody.json
predates these edits; source-pin.json and source-check.json bind the final bytes.

startJanitor attaches error and close listeners in the same synchronous turn as
spawn. Its non-rejecting close promise retains the early outcome: there is no
unhandled rejected promise while waiting for a worker. A close before deliberate
drain is an ownership failure even with exit0. The POSIX worker wait races this
retained close outcome; an ownership failure kills the existing worker group,
awaits its exit/error and returns error2 without publication. Missing worker PID
or a synchronous janitor-spawn exception also follows explicit failed-ownership
handling. Neither failure starts another worker or accepts checker success.

Normal drain keeps the original SIGKILL on that exact ChildProcess, then awaits
its real close. Already-closed children use the retained settled promise and do
not wait for a late listener. A kill exception is retained while close is still
awaited. No kill return, PID-absence check, timeout, process-group poll, init or
zombie exemption substitutes for close. The test PID-absence assertion is only
supplemental evidence after observing close, in an owned namespace.

POSIX cleanup tries both existing stage/tmp removals and still drains the janitor
when either throws. Errors are combined with the original failure rather than
silently replacing it. If actual close never arrives the product does not assert
success; a future proof deadline fails and independent exact-ID containment
teardown retains the evidence. This packet introduces no new product timeout or
supervisor.

The publication rename still occurs while the janitor is protecting abrupt
parent death. Then stage/tmp cleanup and actual janitor close precede POSIX
report/stdout publication and controlled exit. A cleanup/drain failure after
rename can leave completed chart files owned by this request, but emits no
success report or Wrote message and exits2. It does not delete a caller's output
to fake atomic rollback. The cleanup-error regression asserts this exact limit.
An output collision still preserves the arriving caller file and reports error2.

## Windows and checker boundary

The startWorker body and the EOF shell script/argv/options remain byte-identical.
The worker remains detached; its janitor is a separate detached direct CLI child,
not a child of the worker. The Windows guardWorker call and signal wait, guard
close, stage/tmp cleanup and report-before-cleanup ordering are preserved. The
Windows finally body is the original sequence minus the already-inapplicable
optional kill on its undefined POSIX janitor. Shared additions retain the original
fault for POSIX aggregation and gate delayed report emission to POSIX only.
source-check.json records these finite textual comparisons; they are source
checks, not Windows runtime proof.

worker.ts, windows.ts, run.ts, checker.ts, all Java/helper bytes, dependencies,
locks, chart/report fixtures and every other maintained source stay unchanged.
No TLC, SANY, model, renderer, native or npm semantics change is claimed.

## Exact prospective proof

commands.json pins one future normal-host launcher and two contained stages:
Node-DATA preparation, then one proof worker. The proof worker first executes
the corrected actual Counter CLI without any preload, using the original replay
route, and then one native Node test command selecting exactly the twelve names
in input-manifest.json. This extra uninstrumented Counter invocation is necessary
to compare the actual red route without an observer changing its scheduling;
the selected Counter test independently observes close-before-report/exit.
No historical healthy B28/22 suite or full real-TLC C campaign is repeated.

The twelve native cases are two successful corpus verdicts (Counter0 and
CounterInstance1), view-load error2, SIGINT130, SIGTERM143, janitor spawn failure,
unexpected early janitor close, worker spawn failure, cleanup exception,
combined spawn/cleanup exceptions, kill exception and caller-output collision.
expectations.json enumerates each exact title, exit, injection and assertion.
Existing cli.test.ts prefix is byte-identical and no old assertion is weakened.
Only the two admitted corpus directories are present; all unselected tests must
be omitted by native name selection or marked solely as name-pattern skips.
All twelve selected cases must produce unique native ok records, with no selected
skip, duplicate, unselected execution or non-ok record, exit0 and empty stderr.

The appended test generates a scratch-only --import preload for the actual CLI.
In observe mode it forwards spawn/rename/rm/stdout unchanged and records real
ChildProcess spawn/error/exit/close and controlled exit. It is not installed in
production. Early-close mode SIGKILLs the actual janitor immediately; a waiting
view prevents a normal worker-success race. Other modes substitute a missing
executable for the exact child, throw at the real stage rm, throw after actually
SIGKILLing the janitor, or create a caller file just before the real rename.
The preload instruments only statechart.ts, not its worker/replay descendants.
It changes scheduling; it proves observed event order, not the original libuv
timing. No assertion merely checks that kill was called. Raw event logs and real
child close/worker exit plus strict namespace census are all required.

Counter's uninstrumented proof keeps the previous bounded /proc observer:
120s, 60,502 samples, 2ms interval, 1s post-exit observation, PID/starttime/PPID/
PGID/session/argv/raw stat. Sampling can miss short-lived children and is not an
exact libuv clock. The native preload supplies separate exact event-order
evidence. Both guards reject every remaining process, including adopted zombies;
there is no waitpid/reap of adopted children or finally-only ownership claim.

The fixed environment permits only existing test/java.ts replay. RECORD, UPDATE,
PLAIN, classpath/JVM injection and ambient Node/Python hooks are absent. JAVA is
the exact executable replay script; STATECHART_JAVA is /no-runtime/java-forbidden.
Official TLC and sealed bootstrap are verified DATA, not executed. No Java/JDK
is supplied, and PATH plus /usr,/opt,/toolchain are checked before invocation.
Only the admitted Node archive's bin/node is materialized inside owned POSIX
containment; full member metadata and hash are checked first. No host runtime
copy, download, JDK, npm extraction or install is needed or authorized.

The source/import/mode/fixture closure is in input-manifest.json; retained DATA
and old snapshot authority are bound there too. Source checks only parse the
new Python recipes and literal TS imports, never import or discover tests.
Native TS type/static proof remains owed.

## Future resource and cleanup envelope

driver.py/launcher.py minimally adapt the admitted lifecycle envelope and reuse
the hash-pinned b3-source/driver.py containment/retain methods. Live default
LC/t3-f7ffe8ea FIFO, then nonwaiting Heavy, then exclusive normal-TEMP
dydo-fullsuite.lock. Real Windows LOCALAPPDATA/TEMP guards run before booking;
no environment replacement, private namespace, foreign cleanup or global setup.
The two exact TEMP counterpart paths are in source-pin.json. They are source
copies only, not launched by this checkpoint.

Future containers retain the pinned Python Linux/amd64 image, pull never,
network none, read-only root/input, dropped capabilities/no-new-privileges,
128 PIDs, 1GiB, 2 CPUs, no ports/host namespace/privilege/socket. Inspect exact
image/ID/nonce/settings before each start. The source volume is read-only for
the proof worker. Inner child deadlines are120s, direct-child drain5s, each
regular file16MiB; existing outer attach ceiling1200s. First failing command,
guard, source/identity/bound or cleanup stops, retains raw failure and independently
drains only exact owned container IDs/volume. Uncertain ownership keeps the
owned TEMP lock. Existing LC external-host lifecycle limitations are not silently
replaced by a new runner. New driver/launcher/worker direct tests and coverage
are explicitly owed in assurance-proposal.json; older contracts do not cover them.

## Historical applicability and abrupt-parent limit

The immutable attribution RED return-receipt is
d8edbe86f510fd5dad3525c9c50c588d4477978cadeaf26f11137f7bb84d6bf5.
CLI7 had two direct children: worker14 and janitor15; janitor15 targeted worker14.
The unique janitor identity joined Z/PPID1 in raw samples. Precise libuv order
and the original five C process identities were not established. C remains FAIL.
No old result, archive, source pin, expectation or 5079-row manifest is rewritten.

historical-applicability.json maps only the two intentionally changed live files
to their exact before snapshots for verifying old preservation rows. This is
historical byte custody, never evidence that the old run executed the correction.
All other preservation and before-custody rows must match their original paths.
Old installed/payload and old lifecycle proof are superseded for new lifecycle
claims. Other exact-input B1/B2/B3 facts keep their bounded historical meaning.

The existing Java-free SIGKILL tests kill the CLI with its worker/janitor detached.
Python PID1 in the present owned container does not reap adopted children: the
retained attribution directly demonstrates the orphan zombie risk. Killing the
CLI removes the only process that can await its janitor close; the unchanged EOF
shell still kills the worker group and removes stage/tmp. An abrupt-parent run
under this topology cannot honestly establish strict final no-other-process drain
and is not selected. No predictable rerun, --init/subreaper, zombie exemption or
changed cleanup assertion is proposed. Source preservation supports continued
EOF/group/scratch applicability, not certification of abrupt-parent behavior
after this correction. A proper reaping POSIX host/approved containment and
platform proof remain access/acceptance obligations; no setup is commissioned.

## Payload and review handback

payload-proposal.json and payload-proposed.diff contain only the proposed
statechart.ts bytes/hash row and matching ExpectedHash, against the still-unchanged
payload.json/payload.items.props pinned by the captain. No payload file is edited.
The current payload intentionally does not describe this source draft yet.
Old installed proof cannot be relabeled for it. Additional hash matches found
outside those two live files are historical evidence/snapshot identities, not
another current payload declaration; they remain immutable.

Captain inspection precedes any execution. Only after focused GREEN does captain
commission a fresh independent review: close/error/early outcome, failed worker
ownership, exception aggregation, signal/verdict/output ordering, unchanged
Windows/EOF topology, real-child proof strength and finite envelope safety.
No review is self-commissioned. Unexplained proof/review FAIL or scope/safety issue
returns without retry/correction loop. Windows isolated access/prompt proof,
Java assurance, installed/native/npm/platform/static/coverage/current-master and
whole-candidate review remain owed. Desktop Java/javac/install/probe HOLD remains
absolute. No PS retirement/readiness, mutation, commit/merge/watch/PR/push,
version/tag/release/publication. DYD434 remains Done. Both settled HITL threads
stay NO CONTACT.
