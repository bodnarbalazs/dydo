# Single SIGINT diagnosis: readiness-notification RED; containment confirmed

code-writer/model: gpt-6.1-sol/high. Captain/records only.

One unchanged invocation under the [inspected release](https://linear.app/bodnar-balazs/document/dyd-415-inspected-single-sigint-diagnostic-execution-release-00ecde0d6eb3).
HEAD6e3fe7e8018130af6b8586ef0cf40c1474740864 plus frozen draft.
Source pin b22f7212427b5aa4c2c28dad132839e1912434708e358913449a80f1d74cb4cb.
Release a79f8dbfe9445c6fcc9c78162d96bf66cbaa112fdbb572b5f1f839893629fdab.
Inspection567edaf207d67647ca1b32f15df32a7fa8f99aa439c2e3a7b5c782227f104ed9.
All three were preserved as byte-exact at-run snapshots in this output.

## Actual original outcome

Native actual selector: diagnose original fs.watch controlled SIGINT boundary.
One test, pass0/fail1/skipped0, native exit1; driver/launcher exit1.
Error: Original signal boundary exceeded30000ms.
Native case duration30242.933452ms; native runner exited and was reaped normally.
No native50s/Python65s/attach120s emergency timeout was reached.

The view wrote ready and independent polling observed it.
Raw events contain ZERO original fs.watch callbacks, ZERO original signal-send attempts/returns,
and ZERO SIGINT handler entries. The deadline record says signalled:false, readyObserved:true.
The original SIGINT output/exit/close assertions were never reached.
This establishes a current fixture notification/send boundary failure before signal delivery.
It does not establish a production SIGINT shutdown defect or a Docker/inotify implementation cause.
It does not retroactively attribute both original signal timeouts.

No retry or second signal case was run.
SIGTERM below was only the pinned failed-case cancellation path.

## Raw timeline

Times are milliseconds relative to fixture-start147306503230252ns.
The [raw event log](retained/output/signal-SIGINT/events.jsonl) retains full stat/starttime/argv.
Synchronous logging and readiness polling alter timing.

| Relative ms | Actual witness |
| ---: | --- |
|177.635|Worker27 logs ready-created|
|184.290|Fixture13 independently observes5-byte ready|
|30022.717|Original boundary fails; signalled:false, readyObserved:true|
|30039.551|Separate cancellation SIGTERM send attempt to owned CLI20|
|30046.394|Identity-checked send returns true|
|30046.652|Same CLI20 enters original SIGTERM handler|
|30067.709|Worker27 exit, SIGKILL|
|30088.954|Worker27 actual close, SIGKILL|
|30160.370|Janitor33 exit, SIGKILL|
|30172.359|Janitor33 actual close, SIGKILL|
|30181.227|CLI20 records controlled exit143|
|30209.974|Fixture13 observes direct CLI exit143, null signal|
|30221.817|Fixture13 observes actual direct CLI close143|
|30227.759|Failed body records exited/closed:true; original signalled:false|

The extra existing killGroup shell35 (PPID20) also exited0 and closed before janitor drain.
No descendant escalation or direct CLI SIGKILL cancellation was needed.
CLI stderr is the cancellation-only SIGTERM stopped-the-check message.
No stdout data was captured; its absent file was not replaced with synthetic empty evidence.
The raw product outcome remains FAIL_OR_INCONCLUSIVE.

## Exact child attribution and census

| Role | PID | starttime | PPID | PGID | Session |
| --- | ---: | ---: | ---: | ---: | ---: |
|Owned native runner|7|14730644|1|7|7|
|Actual CLI|20|14730651|13|7|7|
|Detached worker|27|14730659|20|27|27|
|Detached janitor|33|14730660|20|33|33|

Fixture observer13 owns CLI20 through its real ChildProcess.
Worker27 and janitor33 are sibling direct CLI children; janitor argv targets27.
Fixture13 parent/starttime was not separately sampled; no fully sampled root ancestor chain is invented.

Before host infrastructure cleanup, both raw [initial](retained/output/processes-before.json)
and [final](retained/output/processes-after.json) strict censuses contain only PythonPID1.
No live/zombie exemption or init/subreaper was used.
The census is independently retained and not inferred from Docker removal.
Cancellation close/census cannot make the unattempted original SIGINT case pass.

Post-cancellation host scratch inspection found empty parent/tmp and no published out.
These are cancellation facts, not successful original output assertions.
Owned scratch and original raw files remain retained.

## Containment, admission and locks

Normal host approval used the real LOCALAPPDATA/TEMP and default live LC FIFO.
The sole invocation waited210s, then owned ticket0000000009-ff018f0451f777b8.
Admission started2026-10-31T11:51:39.326024+00:00; nonce00f56048979e4345.
No queue bypass or foreign process/lock cleanup.

The pinned DATA preparation container exited0; only admitted bin/node and53 declared files were materialized.
The proof container exited1 after the failed native case.
Each actual daemon/context/image/container/mount configuration passed the pinned checks before start.
Private PID/IPC/network-none/cap-drop/read-only configuration remains in the raw inspect outputs.
No Java/JDK/npm, host runtime copy, download/install or settings change.

Exact owned IDs:

- DATA preparation: b6832385cd0ea323b71d8b76c99be9960ee52c2ed89391b5b691fc6651cbcdee
- Proof: 8927b556d26ee180b66d4bb9194a0a3adb1a2d36061dbeba2d001cadded29138
- Volume: dyd415-rmi-b3-signal-containment-00f56048979e4345-node

The [raw containment receipt](emergency-containment.json) records13 commands, all exit0.
Both containers were already exited on cleanup inspection, so no docker stop was needed.
Exact-ID rm and host ps absence succeeded for both.
Exact-labeled volume inspect/remove and final empty volume-list absence succeeded.
This confirms infrastructure containment only.
[Release](released.json) and [secondary release](secondary-released.json) receipts confirm FIFO, Heavy
and the exact normal TEMP ownership lock released. No foreign resource was targeted.

## Custody and decision

Post-run independent read-only hashes matched20 source,2 TEMP,5 external,6831 before-custody,
5266 historical,53 input snapshot and2 retained DATA rows: ZERO mismatches.
HEAD/branch and all source/payload/canonical-test/association/prior-receipt identities remain unchanged.
The [return receipt](return-receipt.json) binds134 raw invocation artifacts plus exact source/release identities,
full timeline, source applicability and containment commands.

Actionable captain decision: pin a minimal fixture-only correction to the readiness gate at
skills/engineering/statechart/scripts/test/cli.test.ts:1461.
A bounded explicit readiness acknowledgement/observation can trigger the same real selected signal
without depending on the unobserved original callback. Preserve130/143/no-output/actual-close assertions
and distinct failed-case cancellation. This is a proposal for source inspection, not an implemented fix
or a retrospective readiness-notification/platform claim.
No production change is established by this unsent SIGINT.
Captain decides whether another diagnostic boundary is necessary; none is chained here.

Original C FAIL, attribution RED and focused FAIL/incomplete remain immutable.
Counter is narrow normal-drain evidence; ten partial ok records are not GREEN.
Abrupt-parent/PID1, desktop Windows Java HOLD, isolated Windows/prompt, Java assurance and
installed/native/npm/platform/static/coverage/current-master/final independent review remain.
No source fix, review, commit/PR/push/merge/watch/readiness/PS retirement/release/version/tag/publication.
Both settled HITL conversations remain NO CONTACT. STOP at this handback.

