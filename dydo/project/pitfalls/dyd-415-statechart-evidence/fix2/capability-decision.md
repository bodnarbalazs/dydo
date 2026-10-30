---
area: project
type: reference
---

# DYD-415 remaining capability decision

For the Ticket Captain and map holder. This is a requested decision, not an exception,
gate PASS, or whole-ticket acceptance. Runtime source remains pinned to `0ddbbf3`.

## Exact missing measurements

The two maintained PowerShell files are
`skills/engineering/statechart/scripts/statechart.ps1` and `windows-job.ps1`.
Pester 3.4.0's retained 12/12 and 11/11 **command** results do not measure:

- Executable source-line coverage per script, with the unchanged 80% floor.
- Branch coverage per script, with the unchanged 60% floor.
- Per-method/script-entry cyclomatic and cognitive complexity, including cognitive <=20.
- Per-method/script-entry HCRAP <=20, requiring genuine line coverage and both complexities.

PSScriptAnalyzer 1.24.0 adds supported static diagnostics; it supplies none of these
coverage or complexity fields. Native PowerShell AST inventories are evidence of syntax,
parameters, references and command dependencies, not a substitute complexity metric.
The separately compiled exact C# here-string has its own native measurement layer;
its result cannot be assigned to the surrounding PowerShell script.

Changed-code mutation is also **UNAVAILABLE**. The existing repository policy records
dotnet/python/node mutation as pending DYD-103 and no adopted TypeScript mechanism.
This hop adopts neither a PowerShell nor an embedded-C# mutation campaign. The retained
acceptance example mutation is a different claim and remains separate.

## Native instrumentation parity

The first exact-source .NET baseline preserves `Win32Exception.NativeErrorCode=87` for
PID zero. With AltCover 9.0.102 eager instrumentation, the same assertion observes 0.
The original failed run is retained in `first-attempt/`. This is an observed instrumentation
parity failure, not evidence that the unchanged runtime needs redesign. Strict error-code
assertions remain in the focused tests; lifecycle and handle cleanup assertions run separately
so a parity failure cannot prevent their measurement. The final per-test comparison is in
`native-test-parity.json` and the original Add-Type probe in `original-addtype-failures.json`.

Coverage counters and valid native identity joins do not erase a failed semantic assertion.
Closing this gap requires an instrumentation mechanism that preserves the native last-error
observable, followed by the same focused baseline/instrumented comparison. A task-local
Coverlet probe is a possible alternative, not an executed or passing adoption here. Do not
modify `Attach` or remove its error-code assertions to accommodate the measuring tool.

## Alternatives and smallest decision

1. Keep the current fail-closed disposition. Commission a narrowly scoped, reviewed
   PowerShell measurement adapter with native executable-line and branch attribution,
   a specified PowerShell cognitive/CC model, script-entry accounting, and method/source
   identity joins. Then calculate HCRAP from those native facts. Merely upgrading Pester,
   importing PSScriptAnalyzer, or translating command percentages does not supply this
   evidence. Changed-code mutation needs its own reviewed adoption; this commission
   does not authorize universal mutation adoption.
2. If the map holder intends DR048 section 2's missing-mechanism treatment to govern
   this delivery, obtain an explicit applicability decision naming these **two scripts
   and these fields**, with a separately authorized testing-guide gap record and owner.
   The decision must reconcile that older skip-and-record language with the current
   coding standards/testing guide's fail-closed UNAVAILABLE rule, and state what readiness
   means while changed-code mutation is unavailable. This writer cannot infer that decision
   from permission to adopt tools.

Recommendation: retain UNAVAILABLE until the map holder settles that exact applicability
question. Do not lower 80%/60%/20, label missing measurements passed, or request another
full platform suite to address a measurement capability gap. No policy or inventory file
was edited by this proof hop.

## Related

- [Proof and capability ledger](./verification.md)
- [Previous platform and TypeScript proof](../fix1/verification.md)
- [DR048, section 2](../../../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md)
- [Current testing strategy](../../../../guides/testing-strategy.md)
- [Current coding standards](../../../../guides/coding-standards.md)
