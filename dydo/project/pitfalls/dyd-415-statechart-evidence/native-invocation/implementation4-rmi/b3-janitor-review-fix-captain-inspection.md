# DYD-415 captain source inspection and consolidated execution proposal

Captain/model: gpt-6-astra/high. 2026-10-31. **PROPOSED ONLY: no execution release, acceptance or review PASS.**

## Candidate and inspection

Retained HEAD: `6e3fe7e8018130af6b8586ef0cf40c1474740864`; authorized uncommitted draft. Same writer run37 completed; no replacement crew. Governing source-only disposition and ownership pin: https://linear.app/bodnar-balazs/document/dyd-415-authorized-review-correction-source-only-ownership-pin-c796a2740484.

Packet: `b3-janitor-review-fix-source/` under `dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/`.

| Identity | SHA256 |
|---|---|
| Source pin | e368bc2746d4f10810ce6ae28cc812deced37aaf9691721d8644fd28203c8b47 |
| Source-data receipt | 3d42770da6b5b09a07ca6e0cb5970d68cf1c8449cd56e41d6fae1208f747fd4e |
| Successor driver.py | 953bd2b280de3dc4c2a385e6aa5fd34a6a40c68d33d811893408959683f700b7 |
| Successor launcher.py | dc97c5b0f8d0299e3eadf29c18c564d585f7c171989d97b1c06203de3a5581ee |
| Successor worker.py | d90ae5536266ad74423a85b61db809ec380c0235e8ea088b664c56f767486c54 |
| Direct unittest source | 4b290261dd9e73244e314580c0c2333e8968630bd04f2cc510491b8f23340704 |
| Current canonical cli.test.ts | b9925b7bd9313f4b2963bd21c868fe4422cd30b193224df08f6ca20f7da9bb5f |
| FROZEN production statechart.ts | 3524650b2b38a90388e9344d4761ab025793cfdd94b68e6bdd6c99a3f172a345 |

Independent read-only captain audit verified 93 source rows, five external rows, both TEMP counterparts, 9,406 before-custody rows with the declared fixture alias, 9,428 after-custody rows, 5,266 historical rows with their existing snapshot aliases, and 65 obligation path/hash references: zero mismatches. Exact atomic-marker-only fixture substitution verified. Production, payload, dependencies, associations and historical evidence remain unchanged. Audit script: `C:/Users/User/AppData/Local/Temp/dyd415-captain-reviewfix-source-audit-20261031.py`; successful source/hash audit tool chunks 9d9fb0 and 42050d. No project imports, discovery, tests, static checks, compiler, container or product runtime were executed.

## Source assessment

The successor's actual main-finally path calls `retain_and_release`. Its local retained flag becomes true only after retain_work returns successfully. Lock deletion additionally requires confirmed cleanup, no evidence-write errors and exact original owner bytes. Successful error logging cannot satisfy retention. Changed/missing owner bytes fail. Original failure remains nonzero. These are source findings, awaiting execution proof.

The 21 direct contracts call the actual helper and main path using isolated temporary locks and mocked material boundaries. They cover retention failure with successful logging; healthy retention; evidence-write failure; prior evidence error; cleanup uncertainty; changed/missing ownership; original failure priority; actual main propagation; release/host/selection refusals; private-PID admission; and native completion validation. Older B/C22 tests are not substituted for these. Current driver/launcher/worker and their imported shared-containment/foundation dependencies remain maintained inputs. Three association proposals remain unapplied.

Readiness now writes the complete marker to an exclusive `.pending` file and renames it in the same fresh owned directory. The existing plain-file/type/content/deadline checks and real signal/output/close/status assertions remain unchanged. Before bytes are retained. Earlier two signal passes certify their former fixture, not these new bytes.

## One consolidated decision requested from admiral

### 1. Recommend one direct21 execution

Exact command, cwd repository root:

```text
C:/Users/User/AppData/Local/Programs/Python/Python313/python.exe -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-janitor-review-fix-launch-20261031.py --contracts
```

Captain considers this finite host-only envelope suitable for the inspected fixed tests: actual live LC FIFO -> nonwaiting Heavy -> exclusive normal-TEMP lock; hidden isolated Python unittest child; 180-second timeout kills/reaps that direct child; all subprocess-producing test boundaries are mocked. Require exact 21 native IDs from commands.json, 21 ok, no skip, terminal Ran21/OK, exit0, empty stdout and retained raw evidence.

Limit explicitly accepted by this recommendation: this is NOT private-PID/network-none/Job descendant containment. The 16MiB per-stream check rejects after completion; it is not a hard disk-write cap. Fixed tests contain no streaming workload or unpatched child creation. Native Python3.13 is used only for these unittest contracts, not as the adopted metric Python. No Docker command or product process is needed. Release validation still requires its Docker-identity metadata field: any contracts-only release must label that value as retained identity/not freshly probed, never claim current Docker validation. The ambient-route-refusal test reads the real release before refusing; this is a released-envelope test, not a standalone no-release invocation.

Only after explicit disposition may captain create the administrative release bound to this source pin, with contracts=true and signals=[], verify identities and no writer overlap, then deliver to SAME writer. First failure stops; no repair/retry. Real retention/cleanup uncertainty keeps the exact owned TEMP lock. Recovery belongs to the original recorded writer with case-specific captain/admiral disposition after ownership, absence and evidence inspection; no stale-lock deletion.

### 2. Preserve the original12 command obligation; decide its precise repeat exception

The source-bound obligation-table.json maps all twelve exact selectors, native records, expectations, actual close/error records, unchanged-source applicability and replacements. Ten original individual ok records remain partial; original runner exit-9, absent terminal summary and absent final census remain FAIL/incomplete. Both later signals remain individually PASS at their recorded source. Neither their sum nor container removal supplies the original command-level result.

A single new-marker SIGINT can test the changed shared readiness branch, but cannot establish clean census after the ten distinct fault scenarios. No prospective command can reconstruct the destroyed original namespace. If the binding requirement is a completed twelve-case invocation with strict post-command census, the smallest selection that demonstrates that exact obligation is the same twelve selectors once on the corrected fixture, with a complete terminal result and strict census. It excludes the separate Counter replay, B28, B/C22 and real-TLC C. This requires an EXPLICIT exception to the current no-repeat-ten restriction and a source-pinned successor envelope; the present single-signal worker does not implement it.

**Recommendation:** keep that completed-selection requirement; authorize only source preparation of its one finite successor twelve-selector command, with all original assertions and strict guard, rather than declare equivalence or waive the missing census. Inspect that packet before any such execution. Do not run the optional standalone SIGINT first if a twelve-selector successor is selected: the latter would cover the atomic readiness change and avoid redundant proof.

If admiral instead defers that command obligation, the already-pinned optional command is the same launcher with `--signal SIGINT`, exact selector `POSIX janitor closes before controlled SIGINT exit with no output`. It is proposed only for changed-marker evidence, not correction acceptance. It requires fresh read-only daemon/context/data identity binding and existing owned network-none private-PID checks before release. Native30s, runner65s plus direct5s, attach120s; readiness10s/close10s/failed-cleanup5s; kernel16MiB stream limits. Actual SIGINT130, exact streams, actual janitor close and strict onlyPID1 pre-teardown census remain mandatory. Emergency teardown is failure containment only. No SIGTERM replay is automatically admitted.

### 3. Keep correction static prerequisites open; admit only source planning of the existing setup route

Exact-overlay assurance is not runnable with current prerequisites:
- Adopted Python3.12.10 metric environment at `dydo/_system/.local/static-gates/python/Scripts/python.exe` is absent. Existing command is that interpreter plus `DynaDocs.Tests/coverage/gate_adapter.py --stack python --gate static`. Host3.13 is not its replacement.
- Coverage and viewer node_modules are absent; no tsc is on PATH. Statechart package has fast-check and its test script, not an installed/pinned compiler and @types/node closure.
- Existing statechart tsconfig supports `tsc --noEmit --project skills/engineering/statechart/scripts/tsconfig.json`, but the exact compiler/type closure needs a source pin. Viewer typecheck does not reach statechart.
- Existing coverage js_metrics.cjs supports TypeScript per-source metric work and its package lock has tool identities, but those packages are absent. Metric collection is not a TypeScript compiler result; inventory also reports TypeScript outside viewer/src scope.

Recommend SAME writer source-only identification/pinning of the repository's existing prerequisite/setup route and exact applicable commands, without installing, substituting dependencies, creating tools, changing policy or excluding maintained evidence drivers. Return any unsupported requirement explicitly. Direct21 or runtime PASS cannot close this blocker. No new review until binding correction-stage gates actually close.

## Standing truth and next checkpoint

All three review blockers remain OPEN: retention implementation awaits direct proof; original12 completion/census awaits explicit disposition and proof; applicable static/direct assurance is incomplete. Readiness nit is source-corrected but changed-fixture execution unproved. No correction acceptance or full gate PASS.

Next checkpoint after admiral disposition: SAME writer direct21 receipt if released, and any expressly authorized successor source plan. Captain owns source/environment inspection and evidence classification. No automatic runtime/review loop.

Desktop Windows Java/javac/install/probe HOLD is absolute. Missing isolated Windows/prompt target, unavailable Java metrics, abrupt-parent strict-drain limitation under current nonreaping PID1, installed/native/npm/platform/current-master/aggregate-static/coverage/HCRAP/CI/whole-candidate review remain open. No security/new-target setup, init/subreaper, zombie exemption, metric tooling project, PS retirement, readiness waiver, release/version/tag/publish/rollout or child merge/watch. Overnight OFF. Both settled HITL channels remain NO CONTACT.

