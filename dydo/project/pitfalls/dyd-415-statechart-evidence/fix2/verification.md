---
area: project
type: reference
---

# DYD-415 native adapter assurance

Writer: code-writer/model: gpt-6-astra/high. Commission: DYD-415 description at
2026-10-30T00:07:27.421Z, with the user's subsequent night-order steering to preserve
real capability, integration and parity gaps. This is scoped implementation evidence,
not an independent review PASS or whole-ticket acceptance.

The runtime candidate is `0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6` on
`DYD-415-statechart`. The five pre-edit checks passed (`pre-edit.json`). This hop adds
only `fix2/**`. The captain retains later master integration, DYD-430's project-wide
audit, status, review and release. No policy/inventory edit, runtime redesign, full
platform-suite repeat, subagent, global installation, push, PR or watch occurred.

## Proof layers and source identity

1. The retained [fix1 proof](../fix1/verification.md) supplies Windows/Linux functional
   suites, TypeScript assurance and actual PowerShell Add-Type lifecycle integration.
   These are earlier observed records, reused here; they are not newly run suites.
2. `measure.py` extracts exactly the bytes between the PowerShell here-string delimiter
   newlines. `windows-job.ps1` is 2,679 bytes, SHA-256
   `ea60a592b12c05ec61b6bd1ef77b2cc9ced422bfa8021df596af3b804da62a6c`.
   Its C# is 2,315 bytes at byte offset 82, SHA-256
   `d73528c22df450659cbb6ce42ced18bb3fe15d081b850278e6425187bce476ee`.
   Generated line 1 maps to PowerShell line 4. LF bytes and the absence of a final
   newline are preserved. `extraction.json` records the mapping; `StatechartJob.cs.exact`
   retains the bytes. An in-memory one-byte mismatch is rejected (`extraction-negative.json`).
3. The focused generated project compiles those unchanged bytes under SDK 10.0.300
   (compiler 5.6.0, recorded in SARIF),
   net10.0, C# 14, Debug, optimization off and portable PDBs. Nullable is disabled for
   the original Add-Type source, which has no nullable context. Repository warning/error,
   code-style, dead-member and selected Sonar settings are copied unchanged. The adapter
   and focused test builds both report zero warnings/errors. Compiler settings and
   package locks are retained beside the project templates.
   Templates carry `.template` suffixes; only the generated TEMP projects compile them.
4. Existing GateMetrics uses Roslyn 5.9.0, Mono.Cecil 0.11.6 and SonarAnalyzer.CSharp
   10.33.0.1635. The actual repository coverage campaign is AltCover 9.0.102 eager,
   not Coverlet. Native pre/post identities and the existing campaign's
   `_same_instrumented_map` check establish unchanged MVID, PDB, documents and source
   points while the instrumented DLL changes. `csharp_join.py::coverage_methods`
   validates original assembly hash, exact physical signature, token and native points.
   The PDB source checksum equals the extracted bytes; a wrong assembly hash is rejected.

This instruments a separately compiled exact-source assembly. It does **not** instrument
the original Add-Type assembly. No hand-translated adapter or successful-call stub is used.
Only `Attach` has a managed body. P/Invoke declarations and Windows' kernel implementations
have no managed coverage denominator; this is not coverage of kernel32.

Build success is not a claim of zero analyzer messages. `adapter-diagnostics.sarif`
retains ten diagnostics classified as `note`: CA2101 at extracted C# line 21 (PowerShell
24), CA1401 at C# 25/26 (PowerShell 28/29), CA1050 at C# 4 (PowerShell 7), and six
SYSLIB1054 LibraryImport suggestions at C# 21–26. None is a warning/error under the
copied repository settings, and none was suppressed by this hop. They remain for the
captain/reviewer; applying namespace/API/marshaling redesign is outside this commission.

## Focused behavior and instrumentation limitation

The corrected uninstrumented baseline passes **6/6**, zero skips. Tests prove last-job-handle
close kills the owned process, explicit termination preserves exit code 2, a closed owned
job is rejected, invalid PID and assignment to an exited owned process throw, and both
failure paths release their handles over 32 repetitions. The exited-process fixture holds
its process handle to prevent PID reuse and first proves `OpenProcess(0x101)` succeeds.
The two strict error-code assertions are separate tests, preserving errors 87 and 5.

The first instrumented run exposed error 0 where the uninstrumented invalid-PID case
reports 87. Its original UI-restricted-job fixture also unexpectedly permitted nesting
in both baseline and instrumented runs. That fixture assumption was replaced with the
exited owned process; it was not a runtime fix. `first-attempt/` retains the original
source and both failing runs. Error-code assertions were retained as separate cases so
they cannot prevent the cleanup assertions from running. The final comparison is
`native-test-parity.json`; `original-addtype-failures.json` records the bounded probes
through the unchanged original PowerShell script.

Native allocation exhaustion and failure to set the valid Job Object limit are not
forced. Their unvisited branches remain in the denominator. No foreign workload,
foreign handle, allocation exhaustion or unbounded process wait is used.

## Capabilities and disposition

`native-summary.json` holds the exact line/branch counts, per-method CC/cognitive/HCRAP,
unchanged thresholds and test outcomes. A numeric threshold result is distinct from
the instrumented test verdict. The known native-error parity failure cannot become a
passing assurance gate merely because coverage is high.

| Native measurement | Observed result |
|---|---|
| Managed source lines | 15/15, 100% |
| Physical branch points | 6/8, 75% |
| Managed methods with coverage | 1/1 (`Attach`) |
| Roslyn CC / cognitive / HCRAP | 5 / 5 / 5 |
| Baseline tests | 6 passed, 0 failed, 0 skipped |
| Instrumented tests | 4 passed, 2 native-error parity failures, 0 skipped |

The native join uses source-line hits, not OpenCover's 23/25 sequence-point percentage.
It uses the eight physical branch rows, not the summary's additional method-entry count.
The two unvisited guard throws remain visible. OpenCover's classic CRAP is not HCRAP.
Both failure cleanup loops observed zero handle growth across 32 repetitions: baseline
372→372 / 376→376, instrumented 383→383 / 387→387 (`*.trx`).

PSScriptAnalyzer is task-local 1.24.0. The downloaded package is pinned to SHA-256
`e86c97d44bb1bc8a1de35e753b85ea1d938f6f9f881639a181507e079bca4556` and imported
by its task-local manifest path. `powershell-static.json` retains every default analyzer
rule, unfiltered diagnostics, native parser errors, parameter counts, command ASTs and
variable/assignment occurrences. The two script roots are declared by statechart's skill
entry and `scripts/windows.ts:9`; neither defines a function or exports a module.
The two new PowerShell proof scripts have a separate unfiltered analyzer section; the
new C# tests also have their own native Roslyn project report. Clone scanning includes
these test/proof sources alongside the runtime adapters.
The authored test summary selects `tests/AdapterTests.cs`; the unfiltered Roslyn report
also retains the test SDK's generated program under the task-local package directory.

The jscpd recipe uses the existing pinned 5.1.2 native executable directly with hidden
launch options. Both real reports and separate deliberate duplicate controls are kept.
The controls establish language processing; they are not mixed into the real denominator.
Clone floors remain 15 lines **and** 100 tokens. Native Roslyn namespace edges and the
PowerShell AST command inventory support the bounded dependency assessment.
The 12-line PowerShell launcher cannot contain a 15-line clone; its explicit source
identity is retained in the audit alongside the three eligible PowerShell inputs.

PowerShell **command** coverage remains the retained 23/23 Pester result. Executable-line,
branch, cognitive/CC and HCRAP measurements remain **UNAVAILABLE**, never inferred from
command percentages. Changed-code mutation remains **UNAVAILABLE** under the existing
policy. The earlier acceptance example mutation remains a separate proof claim.
The [capability decision](./capability-decision.md) names the missing measurements,
instrumentation parity finding, alternatives and smallest requested map-holder decision.

## Reproduce and inspect

All material commands are recorded in `*.command.json`, with exit, argv, cwd, hidden-launch
flag and live LC ticket. Run a normal TEMP copy of `driver.py` on Windows; it books the
default live FIFO, then nonwaiting Heavy, then creates the exclusive dydo lock. It preserves
the inherited ticket and holds admission until children exit. Dependency and build directories
stay under task TEMP. The first sandbox launch failed before material work because its
LOCALAPPDATA was redirected; admitted runs used the real Windows namespace.

```powershell
Copy-Item dydo/project/pitfalls/dyd-415-statechart-evidence/fix2/driver.py C:/Users/User/AppData/Local/Temp/dyd415-assurance2-driver.py
python C:/Users/User/AppData/Local/Temp/dyd415-assurance2-driver.py provision
python C:/Users/User/AppData/Local/Temp/dyd415-assurance2-driver.py measure
```

For a fresh reproduction, set `DYD415_ASSURANCE_WORK` to a new directory under TEMP first.
`instrument` resumes a verified uninstrumented assembly after a preparation failure;
`analyze` reprocesses retained counters without rerunning native tests. `baseline` preserves
the earlier diagnostic recipe. `bootstrap-driver.py` is the exact initial provisioner;
the current driver additionally validates the package hash and retains failed-attempt logs.
The first repeated preparation refused an existing `__Saved` directory before running tests;
the final recipe uses a fresh owned saved-output directory and retains the passing baseline.
Each instrumentation attempt also has a fresh counter/template directory, recorded in
`native-campaign.json`, so the final measurement cannot absorb the first attempt's counters.

`runtime-before.json` and `runtime-after.json` compare every tracked statechart TypeScript
and PowerShell byte to the pinned commit. Historical proof remains unchanged. Reservation
and release receipts retain ownership; raw evidence and prerequisites remain for review.
Direct tool launches use `CREATE_NO_WINDOW`, and every owned test process explicitly uses
`CreateNoWindow=true`. This is launch-configuration evidence, not a desktop-window observation.

## Compact walkthrough for the captain

Exact runtime: `0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6`; this hop changes its proof only.
User behavior remains the explicit `/statechart` invocation: check the same bounded TLA+
model/config, inspect its shortest bug path and chart, and clean up owned Windows processes
when the command finishes or dies. Native PowerShell entry:
`scripts/statechart.ps1 SPEC.tla --config FILE --view FILE --out DIR`, with task-local
`JAVA` and `TLA2TOOLS` as in the retained environment record.

Without rerunning a tour, inspect `../broken-powershell/report.txt`, the
[rendered chart](../broken-powershell/chart.svg), `../corrected/report.txt`,
and the [full prior proof](../fix1/verification.md). Reproduce only the focused adapter
measurement with the commands above. Review `native-test-parity.json`,
`original-addtype-failures.json`, `native-summary.json` and the capability decision before
assessing readiness. Runtime redesign and a waiver are not proposed.

## Related

- [Capability decision](./capability-decision.md)
- [Previous verification](../fix1/verification.md)
- [Testing strategy](../../../../guides/testing-strategy.md)
