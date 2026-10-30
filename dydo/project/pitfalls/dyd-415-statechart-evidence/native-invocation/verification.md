---
area: project
type: reference
---

# DYD-415 native ownership engineering milestone

Author: code-writer/model: gpt-6.1-sol/high, existing sole writer under the
DYD-415 captain. This implements the early G1/G2 modules at pinned merge
`3527ef642d9c0e1a4298cd13f22a53cf8a6a1a15`. It does not deliver the installed
`dydo statechart` command or establish whole-ticket acceptance.

The captain pin, `commission/captain-pin.txt`, adopts the exact
`commission/implementation-contract.txt`, SHA256
`700effa99c32e235f69e814fc80875426b9ad902e5b3f681912a9c4165e66391`, and
dependency lock, `commission/dependency-lock.json`, SHA256
`7bdbd8727178911b1b9501285d21c16edbc86ef87b7884433088fb4509c280ad`.
The [durable contract](https://linear.app/bodnar-balazs/document/dyd-415-native-implementation-contract-3527ef64-captain-pin-9b39d6231488)
and latest captain custody amendment, `2026-10-30T18:19:34.254Z`, govern.
`commission/communication-amendment-pin.txt` retains the later communication
amendment: this crew returns only to its existing captain.
The selected managed Node/JRE/TLC/Pele archives remain unexecuted selections.
DYD-434 is Done; older recorded holds and failures remain historical bytes.

## Implemented seam

`Services/Statechart` contains the version1 ready/ownership/scratch-registration/GO
ordering gate, compile-time Windows job/notification interop, and POSIX UTF-8
spawn/exec/group-custody primitives. No worker release precedes both ownership
and registration acknowledgments. The acknowledgment is a protocol phase:
production directory creation, identity registration and deletion are not yet
implemented. No public command consumes these new modules in this milestone.

Windows keeps its sole kill-on-close job handle in the native owner. The
independent observer receives only a noninheritable completion-port handle,
stays outside the checking job, and treats uncertain drain as failure. Normal
termination also queries authoritative active-process accounting.

The POSIX owner starts a native child with `posix_spawn`, outside the .NET
process reaper, and retains it with `waitid(WNOWAIT)` until group drain. A gated
idle anchor remains alive after checking-worker exit, watches owner EOF and
kills its own group on owner loss. The independent owner stops/drains the group
if the anchor dies. Reaping releases custody only after drain; later operations
are rejected. Fixed `/bin/ps -axo pgid=,stat=` inspection rejects malformed or
failed observations and excludes zombies. macOS behavior is not proved here.

Five focused xUnit files exercise protocol ordering/replay/EOF, real Win32
assignment errors, unrelated-process nonmembership, completion timeout,
UTF-8/null-termination/NUL rejection, and platform refusal. Native interop files
are associated with their real callers' tests; five ABI-only structures contain
no maintained behavior. Existing algorithms, PS adapters, runtime source,
catalogs, all previous evidence and imported master content are preserved.

## Observed gates and raw evidence

Every admitted attempt used the commissioned read-only LC queue/machine-lock/docs
hashes, default host live namespace, inherited ticket, then nonwaiting Heavy and
exclusive-create normal TEMP `dydo-fullsuite.lock`. Each driver stayed alive
through children; child commands use `CREATE_NO_WINDOW`. Settings prove hidden
launch configuration, not actual desktop no-flash observation. No LC Git
metadata claim is made. Admission, teardown and release JSON files sit beside
each gate. Owned source snapshots and source-bound inputs are retained.

| Gate | Actual command/result | Evidence |
| --- | --- | --- |
| Prerequisites | `gate-driver.py preflight preflight-live`, exit0; .NET10.0.300, Windows native toolchain and existing Docker server | `g1/preflight-live/preflight.json` |
| Windows G1 | `gate-driver.py g1 g1-handle-string`, AOT publish0, native cases0, 7/7 | `g1/g1-handle-string/native-outcomes.json`, `aot-binary.json` beside it |
| Actual Linux G2 | `gate-driver.py g2 g2-input-monitor`, owned Docker Linux x64 AOT publish0, cases0, 7/7 | `g2/g2-input-monitor/native/native-outcomes.json`, parent `docker-custody.json` |
| Focused Light | `gate-driver.py light module-light-final`, discovery0, tests0, 18 passed/0 failed/0 skipped | `g1/module-light-final/test-results/statechart.trx`, parent `focused-tests.command.json` |

Windows cases cover errors87/5, owner loss before assignment, loss after assignment
before registration, loss after registration before GO, loss with three native
checking descendants, and normal explicit termination/accounting-zero. Processes
are observed through held handles. All observed checking processes stopped.
**Abrupt-close independent notifications remained ambiguous.** Those outcomes
are `ambiguous-retained`, never scratch-cleanup PASS. Normal explicit termination
produced `empty-observed`. The fixture never deletes scratch and uses no Java.

Linux cases cover exec PID/Unicode/spaced argv/environment/exit17, exec SIGTERM,
CLI EOF, anchor loss, cleanup-owner loss, and both CLI/cleanup-owner loss after
the checking worker exits while a descendant remains alive. pidfds pin test
observations. All observed descendants stopped. Drain/reap paths refused replay;
owner-loss cases make no cleanup claim. Unique labelled Docker container/tag
removal returned0; shared base layers were retained. Host teardown found no
remaining owned processes.

## Failed attempts and corrections

Nothing below is relabelled PASS:

- Initial admission failure, `g1/admission-failure.json`: the sandbox remapped
  LOCALAPPDATA. The driver now refuses any namespace except the commissioned
  host live root; authorized normal escalation supplies the real host context.
- First Windows compile, `g1/g1-first/diagnosis.json`: `CS1503`, no nint overload
  for `WriteLineAsync`; the fixture serializes the port handle with `ToString()`.
  Native cases had not started. Corrected attempt uses a fresh output.
- First Linux attempt, `g2/g2-first/diagnosis.json`: console input blocked before
  `Task.WhenAny` could monitor anchor exit. The fixture moves blocking console
  reads onto tasks. Production ownership code did not change for that retry.
- First Light pre-admission failure, `g1/module-light-porcelain-diagnosis.json`:
  `strip()` removed the first porcelain status-column space. `rstrip('\r\n')`
  preserves columns. No test child or admission occurred. Corrected first Light
  passed16 tests; two direct test gaps then motivated the final18-test run.
- First milestone docs check, `g1/milestone-final/docs.stdout`: exit1 with14
  missing-extension errors for local links to JSON/TXT/TRX. The correction uses
  literal receipt paths, preserving the failed output. Closure repeats only
  source binding, docs and whitespace, reusing prior native/Light/red evidence.
- First corrected docs closure, `g1/milestone-docs-corrected/diagnosis.json`:
  docs passed; whitespace exit2 identified CR-at-EOL in five retained JSON
  artifacts. Exact-path attributes recognize those bytes without normalization
  or global suppression. Original diagnostics and artifact hashes are retained.

The exact tested fixtures/drivers differ between stages. `tested-inputs.zip`
beside each input receipt preserves that attempt's exact snapshot bytes; the
milestone check verifies every entry against its original receipt. Current
production Windows/common bytes bind to G1, POSIX/common bytes to G2, and all
current modules/tests bind to final Light. Later fixture additions are not
represented as the earlier tested binary.

## Reproduction and remaining proof

Use the existing Python3.13 interpreter, approved normal host context and the
commissioned LC root from the captain pin. From this worktree, with fresh unique
attempt names, run `python -B dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/launch.py MODE ATTEMPT`.
Modes are `preflight`, `g1`, `g2`, `light`, `checks`, `close`. The wrapper copies the
driver to a fresh normal TEMP directory and launches it hidden. Earlier gate
argv invoked the same file driver directly from this evidence directory; their
receipts retain that fact. No namespace override is supported. Existing output
or source/hash drift stops before execution. G1 requires the observed Windows
native toolchain; G2 requires the already available Docker server. No global
installation or other-owner process/container/lock changes are performed.

The driver currently pins precommit HEAD3527ef64. A captain replay on a successor
must explicitly rebind the source checkpoint; it must not weaken its source
checks or overwrite retained attempt directories. Archive input receipts are
the authority for replay of an older exact fixture. The source archive holds
sources, not a claim that a future compiler produces the same binary.

`checks` performs absence-of-implementation compile rejection in a disposable
copy, exact tested/current source binding, retained-byte custody, installed
unchanged `dydo check`, and whitespace against both3527ef64 and master1318e93e.
The compile rejection is not a behavioral red verdict. Focused builds use the
unchanged repository warnings-as-errors, strict nullability and Sonar analyzer
configuration. Broad adopted static/coverage gates remain owed at the coherent
implementation stages; no new measurement adapter or policy was introduced.
`close` repeats the bounded docs/whitespace/custody closure after an explained
documentation correction; it executes no tests, native cases or red rebuild.

Still owed: actual installed command/resolver/payload/projection/approval/resume,
DR062/navigation, production scratch identity and deletion, real npm outer
lifetime and Java descendants, cancellation/publication/foreign sentinels,
the final V0–V10 selections and one adopted changed-stack coverage producer.
No command registration, payload generation, prerequisites installation, DR or
runtime integration is claimed in this milestone. The existing PS paths stay
supported. Actual linux-arm64, osx-x64 and osx-arm64 execution is unproved;
cross-publish is not runtime proof. Actual Windows/Linux x64 G1/G2 primitives
are not full V4/V5/V7 package acceptance. Fresh independent review and later
whole-ticket integration remain captain-owned.

## Related

- [Statechart verification history](../../dyd-415-statechart-verification.md)
- [Integration3 historical record](../integration3/verification.md)
