---
area: project
type: reference
---

# DYD-415 Coverlet native parity evidence

For the Ticket Captain. Writer: code-writer/model: gpt-6.1-sol/high.
Commission: `commission.txt`, the map-holder assurance amendment, and the subsequent
checkpoint assigning capability disposition to
[DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434/statechart-settle-readiness-with-recorded-powershell-assurance-gaps).
Final readiness remains blocked pending that disposition and independent review.
This crew supplies bounded proof; it does not adjudicate readiness.
The subsequent map-holder checkpoint reports DYD-434's tribunal complete: 3/3 prepared
a morning acceptance choice, with **no readiness authorization**. Acceptance of the named
PowerShell gaps and separate mutation disposition remain DYD-434-blocked. The captain
commissions the already-required fresh two-seat substantive review after this return;
known unavailable capabilities are not an automatic fix/review loop.

Observed Coverlet 6.0.4 result: baseline **6/6**, instrumented **6/6**, zero failures or
skips, preserving native errors 87 and 5. Both failure paths show zero handle growth
over 32 repetitions in each run. Native managed lines are **15/15 (100%)** and physical
branches **6/8 (75%)**; CC is **5 as an HCRAP input**, cognitive **5**, HCRAP **5**.
The unchanged four numeric gates are satisfied. This closes the observed native
instrumentation-parity gap for this separately compiled exact-source campaign only.
Historical AltCover failure, PowerShell gaps, and unavailable changed-code mutation remain.
The console self-reports `6.0.4.1+90b21079d4`; its pinned package contains net6.0 tool
payloads. The generated test target is net10.0, SDK 10.0.300, C# 14.0, compiler
`5.6.0-2.26230.102 (caa81fa4971f74880cdab61990cb1b11420939ec)`.

## Scope and proof layers

The five pre-edit checks are recorded in `pre-edit.json`: clean isolated captain checkout,
`DYD-415-statechart`, HEAD `f0cfa5064eeb544cf2a7438ebcfeb51ec90c6c35`, base ancestry,
and only the commissioned `fix3/**` path. Runtime remains pinned to
`0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6`. No runtime or test-logic change is authorized.

`input-identity.json` compares the C# excerpt, both project templates, and all six focused
tests byte for byte with fix2. `extraction.json` maps the 2,315-byte C# excerpt at byte
offset 82 back to `windows-job.ps1`: generated line 1 is PowerShell line 4. Its SHA-256 is
`d73528c22df450659cbb6ce42ced18bb3fe15d081b850278e6425187bce476ee`.
The source PowerShell SHA-256 is
`ea60a592b12c05ec61b6bd1ef77b2cc9ced422bfa8021df596af3b804da62a6c`.

The generated TEMP projects compile these original bytes. Fresh baseline and instrumented
runs use the same six focused test cases, including native errors 87 and 5 and both 32-repetition
handle-cleanup checks. Instrumentation selects `[Adapter]*`, with no source-line,
method, or branch exclusions inside that module, replacements, or assertion weakening.
`selection.json`, both TRX files, and `native-test-parity.json` retain discovery and every
outcome/output. Original Add-Type integration remains the separately retained
[fix2 proof](../fix2/verification.md) and [fix1 proof](../fix1/verification.md).
Compiled exact-source instrumentation does not instrument the dynamic Add-Type assembly.

## Native measurements and identities

`native-summary.json` is the measured numeric and per-test ledger.
`native-identity-join.json` records the actual Coverlet-to-native join. Raw Coverlet JSON
and OpenCover reports remain available alongside native before/instrumented/restored
assembly facts, physical method token/signature, MVID, PDB hashes, source checksum and
source locations. Fields missing from Coverlet's schema are reported explicitly; its
report is not passed to the AltCover-specific joiner or assigned synthetic AltCover fields.

The actual Coverlet JSON module key is `Adapter.dll`; its PDB document is the full
task-local `adapter/StatechartJob.cs` path. JSON supplies no native method token, and
OpenCover's `MetadataToken` is empty. Its module `hash` is a campaign GUID, not the
original assembly hash. The independent native facts supply physical token `100663303`,
signature, MVID, assembly hashes, PDB hashes and checksum-backed source points. The
recorded command selects the sole Adapter module; the report name/document/signature
join and native before/during inspection establish identity without invented report fields.

The report joins the exact module/document and physical method signature to original
native metadata, checks the token during instrumentation, and validates PDB source points
and source bytes. Instrumentation rewrites IL offsets, so native point comparison separates
source locations from rewritten offsets. `restoration.json` verifies exact assembly/PDB
restoration and unchanged test binary identity after the campaign.

Native Roslyn metrics use the retained pinned GateMetrics executable and validated tool
source manifest (`metric-tool-reuse.json`). `roslyn-project.stdout` supplies fresh semantic
facts for the byte-identical source. CC is an HCRAP input; **there is no standalone CC <=20
gate**. The unchanged numeric gates are line coverage >=80%, branch coverage >=60%,
cognitive complexity <=20, and HCRAP <=20. HCRAP is
`CC^2 * (1 - line coverage)^3 + cognitive`. Numeric thresholds and semantic test parity
are reported separately.

Only `Attach` has a managed executable body. P/Invoke declarations and Windows kernel
implementations are outside the managed denominator. Native allocation exhaustion and
failure to set the valid Job Object limit are not forced; unvisited managed branches
remain in the denominator.

## Retained gaps and disposition

The historical AltCover 9.0.102 campaign remains unchanged: baseline 6/6, instrumented
4/6, with errors 87 and 5 observed as 0. This separate Coverlet campaign does not rewrite
that observed failure. See its exact new outcomes in `native-test-parity.json`.

PowerShell executable-line/branch coverage, CC as an HCRAP input, cognitive complexity,
and HCRAP remain **UNAVAILABLE** for `statechart.ps1` and `windows-job.ps1`.
Command coverage and PSScriptAnalyzer findings remain separate retained evidence.
Full changed-code mutation remains **UNAVAILABLE**. The earlier acceptance example
mutation is a distinct claim. `capability-disposition.json` preserves these facts and
links the pending DYD-434 applicability decision. No testing-strategy, Decision Record,
gate inventory, or policy is changed here.

## Reproduce the bounded proof

Use Windows and the retained task-local fix2 GateMetrics prerequisites. Copy `driver.py`
to normal Windows TEMP and execute its `measure` mode. For a fresh campaign, set
`DYD415_COVERLET_WORK` to a new directory under normal TEMP and change `OUT` in the TEMP
copy to a fresh owned evidence destination; the recipe refuses to overwrite command
records from an existing attempt. Keep `OLD` pointed at the retained fix2 evidence.
Project templates
carry `.template` suffixes so the repository does not compile this evidence.

```powershell
Copy-Item dydo/project/pitfalls/dyd-415-statechart-evidence/fix3/driver.py C:/Users/User/AppData/Local/Temp/dyd415-assurance3-driver.py
python C:/Users/User/AppData/Local/Temp/dyd415-assurance3-driver.py measure
```

`*.command.json` retains exact argv, cwd, exit, hidden-launch setting and inherited live
LC ticket. `coverlet-package.json` pins the actual version and package SHA-256;
the canonical archive SHA-256 is
`48ad640bc67eb52e0f95c2f80f6368b24088033c5792e834d7ec4ae2ae543217`.
`coverlet-payload-identity.json` compares the installed console/core DLL bytes with
the canonical pinned archive contents.
`compiler-version.stdout`, `compiler-binary.json`, `compiler-settings.stdout`,
`dotnet-info.stdout`, package locks, and templates record
framework/compiler/instrumentation settings. The native instrumented test process exit
is preserved separately from the Coverlet process exit.
The driver invokes `finalize.py child` while its reservation and locks remain held;
the child validates live FIFO inheritance and the parent lock owner, joins retained
counters, and checks evidence. It launches no native test run. For a relocated reproduction,
point its `OUT` and imported driver at that evidence destination.

The first admitted attempt installed Coverlet successfully, then stopped before build
or tests because the initial receipt guard expected one archive and `.store` retained
both versioned and unversioned archive names. `executed-setup-driver.py`, `failure.json`,
the original command logs, and `released.json` retain that actual attempt. The correction
selects the canonical `coverlet.console.6.0.4.nupkg`. The bounded continuation command was:

```powershell
python C:/Users/User/AppData/Local/Temp/dyd415-assurance3-driver.py resume
```

It reuses the existing installation and validates the prepared source/templates against
fix2 before continuing. `resume-*.json` retains its separate driver identity and resource
receipts. The first attempt ran zero native tests; the continuation does not repeat an
earlier native campaign or reinstall the tool.

The exact executed continuation is retained as `executed-campaign-driver.py`, matching
`resume-driver-identity.json`; the initial snapshot matches `driver-identity.json`.
The current reproduction driver additionally enforces the now-recorded package hash and
sets `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` and `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`.
These post-measurement provisioning safeguards do not change the C#, tests, compiler
options, or instrumentation command; no native test was rerun for them.

The initial SDK first-use banner said it installed a development certificate. A post-run
read-only inventory found only the pre-existing localhost certificate dated
2026-02-10, predating this task; the certificate store was left untouched
(`sdk-certificate-metadata.json`). SDK sentinels remain under the task-owned CLI home
(`sdk-first-use-sentinels.json`). First-use did append this task's unique
`dyd415-fix3/dotnet-home/.dotnet/tools` directory to user PATH. `cleanup-sdk-path.py`
removed exactly that entry, preserving every other entry and the registry value type;
`sdk-path-cleanup.json` records the completed rollback. No task tool-path entry remains.

Every material operation books the default live Windows LC FIFO, then takes Heavy
without waiting and exclusively creates `TEMP/dydo-fullsuite.lock`. The parent holds
admission until children exit. Normal Windows LOCALAPPDATA/TEMP are explicit because
the harness redirects them. Reservations and release receipts retain ownership. Direct
child launches use `CREATE_NO_WINDOW`; the unchanged six tests use `CreateNoWindow=true`
for their owned idle children. This is launch-configuration evidence, not desktop-window
observation. Dependencies/caches remain task-local. No full suite, subagent, global tool/module install,
push, PR or watch is part of this hop.

## Compact walkthrough for the captain

Read `native-test-parity.json`, `native-summary.json`, `native-identity-join.json`, and
`capability-disposition.json` before assessing the remaining gaps. Reproduce only the
six-case adapter campaign above. Supported user proof remains runnable through the
retained [fix1 walkthrough](../fix1/verification.md), including original PowerShell
launching, real TLC failure/correction, shortest bug paths, and rendered charts.
No tour or platform suite was repeated to obtain these adapter measurements.

For a separately booked future product walkthrough, use the original local prerequisites:
`JAVA=C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe` and
`TLA2TOOLS=C:/Users/User/AppData/Local/Temp/dyd415-upstream/tla2tools.jar`
(jar SHA-256 `936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`).
From the repository root, pass this exact argv through the same FIFO/Heavy/dydo wrapper
with `CREATE_NO_WINDOW`, using a fresh `--out` path:

```text
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File skills/engineering/statechart/scripts/statechart.ps1 dydo/project/pitfalls/dyd-415-statechart-evidence/broken-powershell/Safety.tla --config dydo/project/pitfalls/dyd-415-statechart-evidence/broken-powershell/Safety.cfg --view dydo/project/pitfalls/dyd-415-statechart-evidence/broken-powershell/view.mjs --out C:/Users/User/AppData/Local/Temp/dyd415-walkthrough-broken-new
```

The retained broken case exits 1 and reports the shortest one-step bug path. Replace
all three `broken-powershell` input directories with `corrected` and choose a different
fresh output directory; the retained corrected case exits 0 under the same invariant.
Inspect `report.txt`, `chart.mmd`, raw `tlc.out`, and the retained rendered `chart.svg`.
These are reproduction instructions and earlier observed outputs, not newly executed
TLC runs from this hop.

## Related

- [Prior native assurance](../fix2/verification.md)
- [Prior capability decision](../fix2/capability-decision.md)
- [Supported platform proof](../fix1/verification.md)
- [DR048](../../../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md)
- [Testing strategy](../../../../guides/testing-strategy.md)
