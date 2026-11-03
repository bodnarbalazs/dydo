# B passed; C failed the remaining-process guard

code-writer/model: gpt-6.1-sol/high. Same author, retained draft and HEAD
`6e3fe7e8018130af6b8586ef0cf40c1474740864`. No commit, source correction,
retry, broader campaign or desktop Java launch.

The exact released launcher ran once per group, B then conditionally C:

```text
python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-bc-launch-20261031.py --group B
python -B C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-bc-launch-20261031.py --group C
```

Both bound the unchanged source pin
`8c4eef594e25a177aa0b8d11c13835ccc6a6d588e9c94b8864605887f8845473`.
B waited 170 seconds in live FIFO; C waited 151 seconds. Both used the exact
actual-host namespace, nonwaiting Heavy, owned TEMP lock and inspected
network-none Linux containment. No new network, compiler or dependency inputs.

## B: finite filesystem/mock PASS

Raw native results were checker16/16 and bootstrap12/12, all exact names once,
zero skipped/failed/cancelled, empty stderr, both command exits0 and launcher0.
There were zero real Java records. Namespace census after tests contained only
the worker PID1. No C invocation began until the raw names/counts, complete
source/archive custody, owned teardown and lock releases were confirmed.

[Before-C gate](../b3-bc-20261031a-B/before-C-gate.json) SHA256:
`5841fd35fed1fcab3782f9198fef97dd8c6afd2049938ad540361846f3811828`.
Its bound raw TAP, command/PID/stat records, input and extraction inventories,
toolchain verification and cleanup receipts remain under the B output.

## C: six test passes, overall FAIL

[Raw native TAP](retained/output/corpus-selected.stdout) contains exactly the
six selected cases: CounterInstance, CounterRules, MediaPlayerProps, Shadow,
the short temporal loop and normal-return/SANY-failure scratch cleanup. Native
summary:6 tests,6 pass,0 fail/skip/cancel; command exit0 and empty stderr.
Existing committed chart/report assertions and model/library checks were not
changed. This partial semantic result does not turn the C gate into a pass.

The worker then rejected its remaining-process census:

```text
RuntimeError: Owned namespace still has another process; retain evidence for independent container drain
```

The finite container and launcher returned1. No group-completed.json exists.
[Raw census](retained/output/processes-after.json) records worker PID1 plus five
`sh` processes, PIDs28,128,319,575,674, each state `Z`, PPID1, own process group/
session, empty cmdline. These are observed zombies, not evidence of five live
Java processes. The pinned guard rejects every other remaining process and did
so here. It was not weakened, normalized away or retried.

Source-only inspection finds the existing CLI creates a detached `/bin/sh`
janitor in statechart.ts and calls janitor.kill("SIGKILL") before process.exit
without awaiting janitor close/reaping. That is a causal candidate consistent
with five CLI cases and the five shell zombies; retained evidence does not map
each zombie PID to that spawn. No root-cause fix or further probe is authorized
or performed. Captain disposition is required for the concrete lifecycle failure.

Twenty sealed-bootstrap and seven separate SANY/XMLExporter argv records were
retained (27 total). An actual-host raw-file audit verified the stable contained
JDK endpoint, canonical helper plus isolated checker classpaths for bootstrap,
isolated checker-only SANY classpaths, original -I/TLA-Library/absolute model
inputs, and no raw tlc2.TLC main. Production TS checker-isolation source stayed
unchanged. No socket, Windows prompt, installed/native/npm or Java-assurance
claim follows from these argv records or the six partial passes.

## Independent owned cleanup and custody

All four owned containers (two per group) and both owned toolchain volumes were
removed. Every exact container/volume absence command returned0 with empty
stdout and stderr. FIFO, Heavy and TEMP-lock release receipts are present for
both groups; actual-host audit confirmed both exact owned FIFO tickets absent.
No foreign cleanup occurred. Registered TEMP scratch and launcher logs remain
retained as evidence; no scratch-deletion PASS is claimed.

[Return receipt](return-receipt.json) SHA256:
`33d4dc71d0f957f20b9418012868930696dbd00b8a650e1997b0244bfd8540bc`.
It binds raw B/C evidence, decoded process fields, all27 Java records, gate and
source pins. The actual-host read/hash audit repeated no tests, Java, Node or
Docker commands. It found zero mismatches across14 admitted source rows,
2,535 preservation rows,415 original snapshot rows,eight retained inputs, LC
pins, TEMP counterparts and unchanged HEAD. Original failed expectations,
old release-at-run bytes, runtime/catalog/history/payload identities remain.

## Direct-test source checkpoint, unexecuted

[Direct-test source pin](../b3-bc-contract-source/source-pin.json) SHA256:
`25427379b88cd7041d6fbd9526821a5cef3edb47b88837c7b3f6657ac27ed359`.

Eight source rows and2,718 preservation rows match. The new
DynaDocs.Tests/coverage/tests/test_statechart_rmi_bc.py contains22 source-declared
native unittest cases importing/calling the actual three successor modules.
Four-file closure, exact names/argv/expectations and three genuine proposed
associations are pinned. No test import/discovery/execution occurred. Existing
test-associations.json remains byte-identical; the proposal is not applied.

The checkpoint explicitly requires Linux's resource module and leaves final
Python producer applicability unresolved. The prior nine-contract envelope also
hardcodes another file/class/count: it cannot execute these22 unchanged. A
separately inspected bounded envelope adaptation and captain disposition of the
manifest/proof applicability remain required. No fake association or waiver.

All desktop Java/javac/install/probe, Windows/prompt access, Java assurance,
installed/native/npm/platform/static/coverage/current-master/independent-review
gates and PS-retirement/readiness/publication restrictions remain. Both settled
HITL threads remain NO CONTACT. Return only to captain/records with C FAIL and
this source checkpoint; no additional execution is started.
