# DYD-415 direct21 verification and twelve/setup decision

Captain/model: gpt-6-astra/high. 2026-10-31. SAME writer run38 completed, activeRunId null, pending0. HEAD remains `6e3fe7e8018130af6b8586ef0cf40c1474740864`, retained draft. This is captain inspection, not independent review or execution release.

## Verified result and identities

**Direct21 PASS at its exact source.** Captain independently hashed the handback and 67 nested path/hash bindings, parsed the raw unittest stderr against the pinned 21 unique native IDs, and verified 21 ok, no skip/non-ok, complete Ran21/OK, native exit0 and zero-byte stdout. Launcher/driver exit0; teardown reports no material resources created, and FIFO/Heavy/TEMP release receipts are true. Launcher stderr contains the expected106-second FIFO wait behind an LC run, not a test error.

| Artifact | SHA256 |
|---|---|
| Direct21 return-receipt.json | 161dad189631e3e6e833308eca57a9ab3a55dbc3dfe8f576d979c96ecfba2ea9 |
| Executed direct21 source pin | e368bc2746d4f10810ce6ae28cc812deced37aaf9691721d8644fd28203c8b47 |
| Archived execution release | 9cecd2a652a991fa2ab22ea9a1001253aa5e0b502b2d9ee3931d0af6ea01015d |
| Twelve source pin | d5a8066e4f8a51378868bff94b34cdfd29f37e2ae3edcefc8b2f5fd741dbae26 |
| Twelve source receipt | 270f6f21164b280bab1d946b88a35b3acdd1ecf6b6f4f7fbf3398fa850b46bd6 |
| Assurance/setup source pin | abc46d14bff0eb58f6f47231a757e8ce278c52497030fea9f3be878880c198bb |
| Assurance/setup source receipt | 9ff55d6b17aeb7d859ed9b72528cfa09f15e8c53af3a88f1674d4a002acc4885 |

All relative paths below are beneath `dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/`. Direct21 raw result is in `b3-janitor-review-fix-20261031a-CONTRACTS/`; new source roots are `b3-janitor-twelve-source/` and `b3-janitor-assurance-setup-source/`.

Independent read-only audit: twelve77 source rows/five external/two TEMP, assurance2193 source rows/five external, both9432 frozen before rows and5266 historical rows with declared aliases, both9464 after-custody rows matched. No new packet execution-release exists. Source/receipt pins, original production3524650b, canonical fixtureb9925b7b, payloads, association file and historical expected hashes remain intact. Audit: `C:/Users/User/AppData/Local/Temp/dyd415-captain-run38-audit-20261031.py`, successful output chunk4fd90c, exit0. No project import, test discovery, compiler or runtime ran during captain inspection.

The actual retention failure/success/ownership paths now have source-bound direct proof, including retain_work throws while safe_save succeeds and actual main propagation with an isolated lock. This supplies the direct-proof component of review blocker1; independent acceptance remains pending. Timeout kill/reap was not exercised in the passing real run, native child PID was not recorded by the unchanged subprocess.run wrapper, and unittest temporary-directory absence was not independently recorded per fixture. No broader lifecycle claim is made. Python3.13 unittest is not metric Python;16MiB remains post-completion rejection, not hard bound. No product/Docker/Node/Java process was launched.

## Twelve-case source assessment and decision

The proposed `--twelve` launcher is one external invocation containing twelve SERIAL native Node commands in one existing owned proof container, after one DATA extraction container. Each command selects exactly one original obligation and runs its unchanged canonical assertions/failure stimuli. Complete native TAP plan, selected/pass1, no selected skip, fail/cancelled/todo0 and exit0 are mandatory for every case. Strict original process census runs before/after each case and at the final boundary. First failure stops later cases.

This produces twelve real native summaries plus an explicit aggregate receipt, NOT one native pass12 summary and NOT a repair of the original incomplete run. It adds observations at every case boundary; it does not prove any interaction specific to keeping all twelve tests in one native test process. I recommend admitting this as the prospective twelve-selection completion/census gate because all distinct stimuli remain and every case plus the final namespace is checked. Admiral must explicitly accept this grouping; if one native test-process batch is a binding additional requirement, return source for that grouping before execution. The original exit-9/no summary/no final census remains FAIL/incomplete either way.

Source checks:
- Actual janitor close-before-CLI-exit is parsed, including ENOENT/no created PID distinguished from SIGKILL of an actual janitor. Worker close is required. I checked retained raw original event logs: all ten prior individually ok cases contain worker close before CLI exit; the two failed old signals contain neither, as expected. New canonical signal proof remains separately credited.
- Each native runner is65s bounded. Cancellation verifies direct child PID/starttime/PPID/own PGID before group SIGKILL and waits at most5s for direct reaping. Missing identity refuses the kill; timeout stays failure even if the runner exits late.
- Group kill cannot by itself reap a detached product worker, and no such claim is made. Strict census rejects live/zombie residue. Existing exact-owned private-PID container cleanup supplies failure containment only.
- Proof attach960s bounds the twelve70s wait/reap allowances plus margin; extraction120s, individual infrastructure calls20s. Synchronous filesystem/evidence operations are bounded operationally by outer containment, not a claimed universal per-call deadline.
- Existing cached-image/network-none/read-only input/toolchain/owned work-output mounts/private namespaces/resource/ID/nonce checks and retention-safe exact-lock release remain. Current daemon/cache identity needs a fresh read-only binding before release. No Docker probe occurred in this inspection.
- Input closure binds51 runtime files and retained Node/checker DATA; no Java, runtime copy, installation, network or new framework.

**Recommendation:** approve the grouping direction, then require the new helper12 assurance and fresh source/environment admission before releasing exactly ONE `python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-janitor-twelve-launch-20261031.py --twelve`. Do not run standalone SIGINT, separate Counter, B28/B/C22 or full real-TLC C.

The two new unittest source files contain12 tests calling actual new admission/retention/TAP/lifecycle/group-identity/timeout seams with material calls mocked. They remain unexecuted. The passing direct21 proves its old successors, not these new modules. Current launcher supports only `--twelve`; commands.json proposes helper discovery but provides no separately pinned host envelope. Therefore I do NOT recommend executing the bare discovery command outside admission. Authorize SAME writer to adapt the existing finite host-only direct-contract envelope into a narrowly pinned successor for this exact12 helper selection, with isolated locks, complete native IDs/result, timeout/direct reaping and evidence retention. Preserve all executed envelopes. No new generic runner/framework and no rerun of healthy21.

## Retained compiler discovery and exact prerequisites

The earlier inference from missing global tsc/coverage/viewer trees was incomplete: local statechart node_modules existed in the preserved custody. The new source packet corrects the live account without altering earlier receipts.

Retained local identities:
- TypeScript5.9.3 (also declared in approved viewer lock).
- @types/node22.18.0 -> undici-types6.21.0; viewer instead locks @types/node22.20.2.
- fast-check4.5.3 -> pure-rand7.0.1, needed by differential.test.ts in the unchanged strict statechart tsconfig.
-2089 retained installed member hashes;54 maintained TypeScript files in whole-project scope.
- Native Node path `C:/Users/User/AppData/Local/fnm_multishells/10620_1793288379755/node.exe`, raw SHA256 `0d0f5e39f9f3d9587bc19f73eab3c2c9c4903fd02d6dbf9c853dd81b3d95fad4`, file metadata22.23.2; not newly probed.

The installed hidden lock carries declared registry/SRI values. Hashing preserved members establishes exact current bytes, NOT archive-to-installed provenance. There is no tracked statechart compiler/package lock. This is not an authorization to copy viewer dependencies or claim a reproducible supported npm setup.

**Recommend explicit admission of this retained exact local closure for one finite diagnostic typecheck of the current overlay**, distinct from reproducible package installation and aggregate static acceptance. No new dependency bytes or version substitution are needed for that bounded command. If stronger archive provenance is required before this use, the precise missing step is an admitted registry-artifact/member comparison; do not describe raw custody as that proof.

Prospective argv (not released):

```text
C:/Users/User/AppData/Local/fnm_multishells/10620_1793288379755/node.exe skills/engineering/statechart/scripts/node_modules/typescript/bin/tsc --noEmit --project skills/engineering/statechart/scripts/tsconfig.json
```

This is the real local compiler/config, not viewer typecheck, a new npm script or a test execution. Source closure and a finite host admission/output/deadline/cleanup envelope must be pinned first; reject ambient Node injection rather than silently modifying global settings. Pin compiler non-emission/config, exact runtime and library inputs, no download/package scripts, and preserve result even if type errors arise. No immediate invocation is released by this inspection.

The same existing host-envelope adaptation can carry this exact second selector if admiral admits the toolchain, without a general framework. Helper tests and compiler results must remain separate. Any unexpected failure stops its lane and returns a concrete disposition, not a correction loop.

Metric setup remains genuinely missing:
- Adopted CPython3.12.10 venv absent; requirements.lock pins eight package versions but no wheel/archive hashes or admitted interpreter artifact identity.
- Coverage/node_modules absent; existing280-row npm lock present. Adopted Node22.13.0/npm exact host identities not established.
- Viewer/node_modules absent; approved pnpm lock exists, but its setup is viewer scope and not statechart typecheck.
- Existing per-source TS analyzer is not a compiler or aggregate-policy closure; TypeScript-outside-viewer inventory gap remains.
- Actual associations/discovery/metric reach for maintained imported shared-containment/foundation remain owed. New helper tests outside normal coverage/tests require explicit selection; associations alone do not run them.

No install, new dependency lock, acquisition, metric substitution or blanket exclusion is recommended implicitly. A reproducible statechart setup would require an explicit compiler/@types/node and complete fast-check/transitive lock decision. Exact3.12.10/Node22.13.0 package acquisition/setup needs its own bounded provenance/installation disposition under existing mechanisms, not a new measurement project. Ask for that source acquisition/setup pin only; no immediate downloads or installs.

## Consolidated requested disposition

1. Credit verified direct21 at its exact source; no replay and no review PASS.
2. Accept the explicit serial-native-summary twelve-selection strategy, subject to helper assurance, exact identity and fresh contained-environment checks before one future finite release.
3. Authorize SAME-writer source-only adaptation of the existing host envelope for exactly helper12 and, if admitted, retained local statechart typecheck. Captain pins exact successor paths first; inspected source precedes execution. Preserve original12 source/runtime evidence and all executed drivers.
4. Admit or reject the precise retained compiler closure with the provenance limitation above; keep reproducible npm setup and metric acquisition as distinct unresolved decisions. Permit source-only pinning of existing approved metric acquisition/setup inputs if desired, no install/new dependency or metric-policy change.

This return deliberately releases nothing. Captain will route the next authorized stage rather than ask Balazs routine product questions. No new review until required correction-stage proof is complete. Review FAIL remains binding overall: original12 replacement proof, applicable static, successor helper assurance and remaining required review closure are open.

Desktop Java/javac/install/probe HOLD remains absolute. Isolated Windows/prompt access, unavailable Java assurance, abrupt-parent strict-drain environment, installed/native/npm/platform/current-master/aggregate static/coverage/HCRAP/CI/whole-candidate review remain open. No PS retirement/readiness waiver, new tooling/security/target/init/subreaper/zombie exemption, release/version/tag/publish/rollout/child merge-watch, commit/push/PR or settled-HITL contact. Overnight OFF.

