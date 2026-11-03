# DYD-415 canonical signal fixture correction — source checkpoint

code-writer/model: gpt-6.1-sol/high. SOURCE ONLY, NOT RELEASED.
Authority: https://linear.app/bodnar-balazs/document/dyd-415-witnessed-readiness-gate-correction-and-affected-signal-proof-b303b1e0e131

HEAD remains 6e3fe7e8018130af6b8586ef0cf40c1474740864, branch DYD-415-statechart, retained dirty draft. The explicit contract supersedes generic clean-tree/commit/test workflow here. No product decision or further user approval was required for this source preparation.

## Actual edit and custody

Only skills/engineering/statechart/scripts/test/cli.test.ts changed live. Before: 62712 bytes, SHA256 80dd910974c3f97563a92747d03444fa95d0bd5e2fee2b9ad9b316c3dcce40f8. After: 64411 bytes, SHA256 1d19080f30f16f861fdd15e7e4765f16929e1c5fc948afd2c5082f20ce875808.
Exact before bytes are before/cli.test.ts; fixture.diff contains the entire two-hunk change, entirely within the shared SIGINT/SIGTERM loop. Text comparison confirms both outside-loop prefix and suffix unchanged. No import changed. git diff --check for the live file returned exit0.

Production statechart.ts stays SHA256 3524650b2b38a90388e9344d4761ab025793cfdd94b68e6bdd6c99a3f172a345. Payload declarations and associations remain unchanged; no payload amendment is needed for a test-only edit. The original production correction pin30bbe7be... is retained, with its old test identity historical rather than relabeled as this fixture.

The preexisting finite inventory/before-custody/before-status and exact before snapshot were retained. The 6994 immediate before-custody rows were rehashed: only the single authorized live test identity differs. That row maps explicitly to the exact before snapshot in preservation-map.json. All5266 historical rows were checked against unchanged expectations and original two-file mapping, zero mismatches. This includes2089 ignored node_modules files outside the immediate tracked/untracked inventory: a join against only the6994 rows initially lacked those observations; separate read-only hashing completed them with zero drift. No old expected hashes were regenerated.

## Fixture correction

The waiting view still creates a real fs.watch handle and awaits indefinitely. It writes the ready marker exclusively with flag wx, using the fresh mkdtemp basename as content. The parent has no readiness notification watcher: it explicitly observes a plain file and exact fresh content, while rejecting retained early child error/exit/close. Sleep10ms only yields between fact checks, never implies readiness. Error/exit/close listeners attach immediately after spawn, before asynchronous waiting, and retain non-rejecting early outcomes.

Ready observation has10s and selected-signal close observation has10s. Each pending sleep is awaited and settled; there is no outstanding watcher or timeout race in the parent. The real owned child.kill(selectedSignal) call occurs once after verified readiness and must return true. Original130/143, null signalCode, empty stdout, exact stderr, absent output/stage/tmp and drainedJanitor actual-close-before-exit identity assertions remain.

Any assertion/deadline error is retained. Failed-case fallback SIGTERM is separate; it cannot reach or satisfy original assertions. It waits up to5s for the already-attached actual close even when sending fails/throws, and aggregates cleanup failure with original failure. No unconditional product close claim on a failed diagnostic; missing close remains FAIL/inconclusive for independent infrastructure containment. There is no fabricated close, init/subreaper, PID-absence success or arbitrary PID kill.

## Prepared finite execution, held

commands.json pins two separate one-signal invocations of the new source-bound normal-TEMP launcher, each selecting exactly one actual canonical native test:
- POSIX janitor closes before controlled SIGINT exit with no output
- POSIX janitor closes before controlled SIGTERM exit with no output

No diagnostic copy substitutes for the canonical file. Exactly one native selected ok/no selected skip/duplicate/non-ok, exit0/empty native stderr, exact actual CLI event log and strict namespace census before teardown are required per invocation. Other registered tests may only be explicit native pattern exclusions. No full file/healthy ten/Counter/B28/22/real-TLC repetition is admitted.

The51-file source/corpus/helper DATA snapshot has30 TS files and62 literal relative import edges, all targets present. Counter is replay input for these two signal tests; CounterInstance and checker/bootstrap jars remain DATA only. input-manifest.json binds modes, exact files, original member manifest and retained official checker/Node archive identities. There is no Java executable, JDK/npm install/download or host runtime launch.

The existing contained envelope is minimally adapted: two owned network-none containers per separately released invocation, first prepares only pinned bin/node and the snapshot in a fresh labeled volume, second mounts it read-only. The frozen extraction/census/environment foundation907d... and shared containment24f0... are hashed before future import; original functions that run Counter/twelve cases or JDK stages are not called. closure.json maps literal imports, dynamic seams, generated original preload/view templates, mount settings, exact process ownership and every failure cleanup path.

Actual-host guards fail before FIFO booking on redirected LOCALAPPDATA/TEMP. Existing live LC FIFO -> nonwaiting Heavy -> exclusive normal TEMP dydo-fullsuite.lock, hidden infrastructure children and independent exact-ID+nonce stop/remove/absence are retained. Captain must bind actual current Docker executable/default context/local endpoint/seven-field daemon tuple before release; per-start cached image/config/mount/ID inspections remain mandatory. No Docker query or start occurred in this turn.

Canonical waits total at most25s plus scheduling/assertion overhead, under native30s; runner65s plus direct reap5s; host attach120s is emergency-only.16MiB hard evidence-file bounds/128PIDs/1GiB/2CPU are retained. Emergency direct-runner kill or container removal can contain failure but cannot prove product close or strict clean census. Uncertain cleanup/absence/evidence retains the ownership TEMP lock, no foreign cleanup.

## Return and remaining proof

TEMP driver/launcher are exact source copies at the two named paths in source-pin.json; the optional assemble counterpart is absent. None was executed. No execution-release.json exists. No new maintained framework or metric tool was created; successor wrapper direct tests/coverage/static assurance remain owed, explicitly proposed without manifest changes.

proof-applicability.json separately proposes finite missing-marker/early-completion negative failure oracles for captain inspection only; neither is authored, selected or run. Existing notification RED is retained regression evidence. Source assertions and lexical import checks are not runtime proof; no syntax/compiler/import/discovery check was run.

Original C FAIL, attribution RED, focused FAIL/incomplete and executed SIGINT913947c1... remain immutable. Fallback SIGTERM remains cancellation evidence. Ten prior ok records are partial only. Canonical instrumentation changes scheduling. Abrupt-parent EOF/detached topology is source-preserved but current nonreaping PythonPID1 cannot certify strict abrupt-parent drain; none is scheduled. Desktop Windows Java HOLD, isolated Windows/prompt, Java assurance, installed/native/npm/platform/static/coverage/current-master/applicable CI/whole-candidate/final review gates remain owed.

Captain source/identity/cleanup inspection and a separate exact source-pin-bound finite execution release are next; no automatic chain/retry. Fresh independent review is captain-owned after required focused GREEN. No test/import/discovery/Node/Docker/Java/compiler/probe/signal/network/extraction/install/runtime/generated driver/assembler execution, review, commit, PR/push/merge/watch or publication occurred. Both settled HITL threads remain NO CONTACT.
