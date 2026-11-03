# DYD-415 focused janitor and signal correction review contract

Contract: DYD-415 description as of 2026-10-31T12:36:24.076Z, supplemented by the verified SIGTERM return and this review-stage pin. This is the authorized fresh correction review after the two affected canonical signal cases passed. It is NOT whole-ticket acceptance or a declaration that the original twelve-case run became green. Do not turn the ten old ok records into a completed-suite PASS. Independently judge whether the retained proof supports any proposed correction acceptance and identify exact residual required gates.

Base/retained HEAD: 6e3fe7e8018130af6b8586ef0cf40c1474740864. Candidate is an explicitly authorized UNCOMMITTED overlay, not that commit alone. Exact live candidate:
- skills/engineering/statechart/scripts/statechart.ts SHA256 3524650b2b38a90388e9344d4761ab025793cfdd94b68e6bdd6c99a3f172a345
- skills/engineering/statechart/scripts/test/cli.test.ts SHA256 1d19080f30f16f861fdd15e7e4765f16929e1c5fc948afd2c5082f20ce875808
Do not use the entire HEAD diff as this correction's scope. Exact before snapshots under b3-janitor-correction-source/before/:
- statechart.ts SHA256 2305d329359666fd22e738a37aa6d204dc7ef135dcf87592a49cec7cc5a846ec
- cli.test.ts SHA256 171964e98064b2269fdeb62ded950cbf5abd76d186a0096fc2e03d155c336d00
Initial correction source pin30bbe7be70c9482971410d1ddef6764481da0828237c5d1d3a6096f8ec73efce binds production and original appended tests. Latest source pin e88f5eccc75941511b6ecc42548f355259a77d30c61433e0135fd9442352e9b0 binds the final test/readiness correction and envelope. All source and history frozen during review; no payload sealing or commit yet.

## Binding implementation contract

Admiral authorized the minimal POSIX janitor correction: attach completion/error observation at spawn with retained early outcome, preserve SIGKILL of the exact child and await actual close before controlled exit. Handle early close, spawn failure, cleanup exceptions and output/error/signal ownership explicitly. Preserve abrupt-parent EOF protection, detached group, Windows/native ownership, TLC/SANY/checker/model/report semantics. No timeout/PID-absence/kill-return substitute for reaping, init workaround, zombie exemption or general lifecycle framework.

Subsequent daytime authority: resolve the two failed signal boundaries; if evidence establishes a fixture issue, minimally correct the existing test preserving actual signals/output/exit assertions. Separate successful product drain from failed diagnostic containment. Missing close may produce FAIL/INCONCLUSIVE and exact-owned container teardown, never product PASS. No repeat healthy Counter/ten cases/B28/22/full C merely for progress. Fresh independent review follows affected proof; review FAIL/new safety/provenance/wider scope stops this lane for admiral disposition, no automatic correction/reviewer loop.

The fixture diagnosis established marker creation and explicit observation with zero original callback or SIGINT send in30s. It did NOT establish an intrinsic product SIGINT defect, all old timeout causes, or a general Docker/inotify cause. Shared fixture now observes fresh exclusive marker contents, retains child error/exit/close immediately, sends actual selected signal, and requires actual close/status/output/assertions; cancellation stays failed-case-only.

## Exact proof and preserved failures

All paths below are relative to:
dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/

- b3-lifecycle-20261031a/return-receipt.json: d8edbe86f510fd5dad3525c9c50c588d4477978cadeaf26f11137f7bb84d6bf5, original janitor identity residue RED. CLI7 owns worker14 and janitor15 separately; janitor PID/starttime becomes Z/PPID1. Exact libuv timing and all original C PIDs not established.
- b3-janitor-correction-source/checkpoint.md and production-test.diff: exact product/appended-test delta, frozen before snapshots, source-check.json, input-manifest/expectations and payload proposal.
- b3-janitor-correction-20261031a/return-receipt.json: cdedf5f1729f6f15e405c26235be0d22329de25204f54ad786d9651fd966754a. Uninstrumented Counter exact output/exit0/strict census passed. Original twelve-test command produced10 ok plus2 signal timeouts, then120s runner termination; no complete summary/final regression census. FAIL/incomplete remains immutable.
- b3-signal-containment-20261031a-SIGINT/return-receipt.json: 913947c1ee9f7ca78559e192af782f64aeb4396e84c3e1d0af74daf7eccb83ce. Single shared fixture gate diagnosis RED; fallback SIGTERM was cancellation, not canonical signal proof.
- b3-signal-fixture-fix-source/fixture.diff and checkpoint.md: only shared canonical signal loop changed after diagnosis. All pre-loop/post-loop source and other tests frozen.
- b3-signal-fixture-fix-20261031a-SIGINT/captain-return.json: b3e798a992807741701a235f8454610ddde1ad94920a7570488c1a54265db0ec. Native selected/pass1, fail/cancelled/skipped0; actual canonical SIGINT/130/null signalCode/close/exact outputs asserted. Raw janitor33 actual SIGKILL close before CLI20 exit130, strict onlyPID1 pre-teardown census. Captain hashed143 evidence/9108 custody files with zero mismatches.
- b3-signal-fixture-fix-20261031a-SIGTERM/captain-return.json: 712ee256192f78077ac976fe01338b4b773c20f218510994b7b3567b2bea3d31. Native selected/pass1, fail/cancelled/skipped0; actual canonical SIGTERM/143/null signalCode/close/exact outputs asserted. Raw janitor33 actual SIGKILL close before CLI20 exit143, strict onlyPID1 pre-teardown census. Captain independently hashed144 evidence/9261 custody files with zero mismatches (audit tool chunk e31d03).
Both signal runs used pinned Node22.23.2 DATA extraction in existing network-none Linux/amd64 container, actual CLI with existing Java-free replay and unchanged observer. Actual captured CLI streams/status/close are tested in canonical assertions; retained canonical.stdout is TAP, not separate CLI streams. Each had13 owned cleanup commands exit0, containers already exited0 before removal, container/volume absence and FIFO/Heavy/TEMP release. Observer synchronous writes and polling alter timing. No real Java.

SIGINT administrative release changed for SIGTERM: old9c0ad5ff… release exact bytes preserved in SIGINT/execution-release-at-run.json; successor52fa5adc… in SIGTERM archive. Historical custody maps ONLY that old live release row to its archive. Source pins/receipts themselves immutable. Do not print huge full receipts; parse keys and selected records.

## Review rubric and gates at this stage

Use reviewer skill, resources/code.md: four axes contract, standards, gates, security. Read actual complete code around each hunk, actual tests and raw evidence. Judge early/error/close retention, worker ownership, kill/close ordering, exception aggregation, signal/verdict/publication semantics, source-preserved Windows and abrupt EOF/group behavior, fixture race removal without weakened assertions, and containment evidence strength. Check all12 originally required scenarios without disguising the incomplete original suite. Read applicability claims critically.

This is a read-only, source-and-retained-evidence correction review; NO runtime probes or test imports/discovery/builds/containers/Java/Node/signal delivery/CI/network. Source/hash/git reads are permitted. If a concrete doubt needs a runtime experiment, return exact need/selector/seam to captain; do not launch it. No authoring fixes, no producer/metric tooling. One fresh independent seat, later correction round; gpt-6-astra/high under existing Codex review/provider disposition. No writer active; maximum one review seat, no subdelegation.

Two affected signal proofs are GREEN individually. They do not convert the failed original twelve-case suite into GREEN; do not silently lower any required gate. Evaluate whether the proposed narrow correction is acceptable on this evidence or what precise additional proof is binding. A missing required stage gate is a finding, not an automatic waiver or automatic full rerun. No existing static/coverage policy changed. Wrapper direct assurance and applicable TS/static proof remain owed; classify stage applicability explicitly. Broader final aggregate static, coverage, installed/native/npm/platform/current-master/CI/whole-candidate review remain open and no readiness/merge claim is being reviewed.

Abrupt-parent strict-drain proof remains unmet under nonreaping PythonPID1; source-preserved EOF/group logic is not actual platform certification. Do not prescribe init/subreaper/zombie exemption or run the known unsuitable proof. Report preserved source behavior and concrete limitation honestly.

## Return and holds

Writer return: code-writer/model gpt-6.1-sol/high, canonical SIGTERM PASS selected1/fail0 with exact close/census/cleanup/custody, no retry/extra execution, review captain-owned. There is no whole implementation-complete claim.

Return ONE binding code-review PASS/FAIL with exact overlay identities, scoped reviewed outcome, each finding severity/axis/file-line/consequence and actionable requirement, plus explicitly unclosed proof/assurance/access gates. PASS must not claim whole statechart/installed/abrupt-parent acceptance. FAIL stops this lane for admiral disposition; no correction loop or replacement reviewer.

Reviewer owns no repository files. Return block in final to captain only. No Linear posting/other agent messages necessary. Both settled HITL channels NO CONTACT: mcp:launch:eb8d1357aa77668c91539ab7da7f3b4860fae964956d6d091c988ff1fc48da2b and mcp:launch:2323766d4169746fa10da5826b9ce9985b05e8f45a6fbbc0a47abcfdaa4da1f1 (also honor full settled wording identity supplied by captain; never contact any HITL thread).
Desktop Windows Java/javac/install/probe HOLD absolute. Missing isolated Windows/prompt target, unavailable adopted Java metrics, abrupt-parent environment unresolved. No global security/firewall/settings, new target provisioning, publication/release/tag/version, PS retirement, readiness waiver, child merge/watch, source edits or commit/push/PR. Overnight OFF; DYD434 Done.

