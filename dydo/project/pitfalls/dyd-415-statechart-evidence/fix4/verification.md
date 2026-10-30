---
area: project
type: pitfall
---

# DYD-415 fix4: scenario sensitivity and portable Coverlet bookkeeping

This hop addresses round2 F3 and DYD-436 only. The source candidate is
`fb047aa7c41b97277aa75620677d221d882e4f5e`; runtime and original acceptance tests
remain at `0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6`. The five pre-edit checks
are in `pre-edit.json`. Author: code-writer/model gpt-6.1-sol/high.
The complete two round2 reviews, their audits, and the contract snapshot are
retained under `reviews/`; these historical records keep their original wording.

## Three missing semantic mutation pairs

`prove.py` archives the pinned candidate into owned normal Windows TEMP. Its
private Git index/refs use a read-only object alternate; no Git worktree is added,
and scratch commits never change captain history. The unchanged setup script
creates both host projections in the snapshot. Source import/meta paths remain
intact. Only the selected expectation in scratch `acceptance.test.ts` changes;
the original Gherkin, scenario names, models, invariant, host configuration and
runtime wiring stay unchanged.

| Scenario | Expected value changed | Required red and restored result |
|---|---|---|
| Correcting the model preserves the invariant | Report state count 3 to 4 | `ERR_ASSERTION`, match; restored pass |
| The chart budget preserves counterexamples | Shortest path 3 steps to 4 | `ERR_ASSERTION`, match; restored pass |
| Both hosts require user invocation | Expected Codex implicit-invocation value false to true | `ERR_ASSERTION`, match; restored pass |

Each command selects exactly one named test, with nonzero discovery and zero
skips. `scenario-ledger.json` binds the original/mutated/restored test hashes,
command logs, outputs and hidden-launch counts. `test-bytes/` retains the exact
test bytes. Raw TAP and actual TLC/report/chart artifacts accompany each run.
The fail-before-launch interceptor is the unchanged fix1 interceptor, inherited
through `NODE_OPTIONS`; it rejects missing `windowsHide` before launching.
This is launch-configuration evidence, not desktop-window observation.

The fourth scenario, “A broken model shows its shortest bug path”, reuses fix1's
one-step-to-two-steps red/restored pair. Its command discovers exactly one test;
its raw failure is an actual report assertion, followed by restored green.
The ledger records those receipts' hashes and unchanged source identity.
That pair is retained evidence inspected in this session, not rerun here.
These four pairs concern acceptance values. Full changed-code mutation remains
UNAVAILABLE and is a separate DYD-434 disposition blocker.

## DYD-436 superseding reproduction recipe

The historical fix3 finalizer asserts a nonempty writer-only dirty-path status.
That attestation describes its actual writer campaign; a clean fresh checkout
cannot meet it. The superseding `recipe/` separates that historical attestation
from repeatable measurement. Original fix1/fix2/fix3 artifacts are unchanged.

Copy all five `recipe/*.py` files together into a new normal TEMP directory.
`config.py` takes explicit source/work/evidence/metric/LC/TEMP roots. Driver,
finalizer and report load from the recipe directory, while every result is
written to configured fresh external evidence. Source must be clean, including
untracked files; all skills must match the actual runtime pin. Both work and
evidence destinations must be absent, disjoint and outside source; work must
be below normal Windows TEMP. Existing output is refused before execution.
Final bookkeeping accepts a clean checkout and checks it again.

From a clean reviewed checkout, with the already retained task-local GateMetrics
prerequisites, use this exact PowerShell recipe. Choose new directory names for
every attempt; these example paths must not already exist.

```powershell
$env:DYD415_SOURCE_ROOT = (Get-Location).Path
$env:DYD415_NORMAL_TEMP = 'C:/Users/User/AppData/Local/Temp'
$env:DYD415_COVERLET_WORK = "$env:DYD415_NORMAL_TEMP/dyd415-coverlet-fresh-work"
$env:DYD415_EVIDENCE_OUT = "$env:DYD415_NORMAL_TEMP/dyd415-coverlet-fresh-evidence"
$env:DYD415_METRIC_ROOT = "$env:DYD415_NORMAL_TEMP/dyd415-fix2"
$env:DYD415_LC_ROOT = 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47'
$recipe = "$env:DYD415_NORMAL_TEMP/dyd415-coverlet-fresh-recipe"
New-Item -ItemType Directory -Path $recipe -ErrorAction Stop
Copy-Item dydo/project/pitfalls/dyd-415-statechart-evidence/fix4/recipe/*.py $recipe
python "$recipe/driver.py" preflight
```

`preflight` is the lightweight check: real FIFO, nonwaiting Heavy, exclusive dydo
lock, source/runtime checks, fresh destination validation, runtime hashes and
release receipts. It launches no dotnet, native tests or instrumentation and
does not create the work destination. Its output explicitly records
`measurementExecuted: false`. To run a future actual six-case measurement, use
new absent work/evidence destinations and `python "$recipe/driver.py" measure`.
Preflight output cannot be reused as measurement output. The complete retained
GateMetrics tree is required at `DYD415_METRIC_ROOT`, not just a copied DLL;
the driver checks its source manifest against fix2 before measurement.
Source contains the retained fix2 templates, extraction, selection and Roslyn
records used by the recipe. Coverlet 6.0.4/package SHA remains pinned. SDK
first-use safeguards and task-local caches are inherited from fix3.

The fresh-path bookkeeping check is exercised by five focused tests in
`recipe/test_recipe.py`: clean source and external output/module wiring;
existing work/evidence refusal; source/output overlap refusal; unrelated dirty
source refusal; and a committed wrong-runtime rejection independent of
dirtiness. Scratch refs/files are restored. A separate admitted `preflight`
invocation exercises actual driver/module wiring and output/release bookkeeping.
The clean-source test also executes the exact historical dirty-status predicate
in isolation against the actual empty status: exit 1/AssertionError is retained
in `recipe-tests.stdout`, before the corrected preflight succeeds. It executes
no preceding native/report work from the historical finalizer.
Its complete results are retained in `recipe-preflight/`. No instrumented output
is fabricated or replayed, and the corrected measurement mode was not executed
in this hop. The supported six-case Coverlet measurements remain fix3's
historical observations, not new measurements of this portable recipe.

## Execute this bounded proof

From the captain checkout at the pinned source candidate, copy `prove.py` to
normal TEMP and run it. It requires a fresh fix4 attempt (never overwrites
command records) and the existing Java/TLC task prerequisites named in the
commission. It executes six selected scenario cases, five bookkeeping tests,
one preflight, docs and whitespace; no platform suite or native campaign.

```powershell
Copy-Item dydo/project/pitfalls/dyd-415-statechart-evidence/fix4/prove.py C:/Users/User/AppData/Local/Temp/dyd415-fix4-prove.py
python C:/Users/User/AppData/Local/Temp/dyd415-fix4-prove.py C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f
```

The driver uses the default live Windows LC FIFO and retains inherited tickets.
Material phases take nonwaiting Heavy then exclusive normal
`TEMP/dydo-fullsuite.lock`; the standalone recipe inherits FIFO and takes its
own secondary locks after the outer driver's locks are released. Parents live
until children exit; Python launches use `CREATE_NO_WINDOW`. Reservation,
phase release and final release receipts record ownership. The initial sandbox
submission failed to create its FIFO ticket; it executed no proof child. The
authorized elevated submission uses the same live queue and locking protocol.
The first admitted attempt stopped before setup/scenarios because Git rejected
a CRLF-terminated object-alternate path. `attempt1/` preserves the exact driver
and raw failed command. The corrected driver writes explicit UTF-8/LF path bytes;
no material scenario or native campaign was repeated from that attempt.

`source-before.json` and `source-after.json` bind every tracked canonical skill
file. `historical-unchanged` checks original skills and fix1/fix2/fix3 records
against the source candidate. Docs and whitespace raw outputs carry their exits.

## Walkthrough and disposition limits

Read `scenario-ledger.json`, then the three `*-red.stdout` assertion excerpts
and `*-restored.stdout` TAP summaries. Inspect `recipe-tests.stderr` and
`recipe-preflight/preflight.json`, `runtime-before.json`, `runtime-after.json`
and `released.json`. Review `config.py` plus the narrow driver/finalizer changes
against fix3; `report.py` is an unchanged copy. Read the final source and release
receipts before handing the candidate back for fresh review.

DYD-436 is corrected pending review. Mandatory PowerShell executable-line,
branch, cognitive and HCRAP measurements (and CC input) remain UNAVAILABLE for
both maintained scripts; full changed-code mutation remains UNAVAILABLE.
DYD-434 still blocks readiness pending its authorized morning disposition.
There is no independent CC ceiling. Historic AltCover remains FAILED native
semantic parity; supported Coverlet proof remains distinct and retained.

The Sol review's janitor nit is recorded/deferred: `windows.ts:18-20` creates a
cleanup promise that can reject before its caller attaches a consumer; the
late-consumer interval appears at `statechart.ts:133,145,162`. It is a
source-based rare-failure observation, without runtime reproduction. This hop
does not promote it to a demonstrated defect or readiness blocker and changes
no runtime behavior. Captain retains its walkthrough/user disposition.

No subagents, push, PR, watch, readiness claim, rebase, amendment or squash.

## Related

- [Supported Coverlet historical proof](../fix3/verification.md)
- [Initial correction proof](../fix1/verification.md)
- [Top-level verification](../../dyd-415-statechart-verification.md)
- [Testing strategy](../../../../guides/testing-strategy.md)
