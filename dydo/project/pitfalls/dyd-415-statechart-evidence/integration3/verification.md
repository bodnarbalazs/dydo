---
area: project
type: reference
---

# DYD-415 integration3 dependency closure

Author: code-writer/model: gpt-6.1-sol/high, commissioned by the existing
DYD-415 Ticket Captain. `commission.txt` retains the binding I1 FAIL and live
contract. `resource-authorization.txt` records the authorized LC relocation.
This is a bounded preflight correction, subject to independent correction review.
Overall DYD-415 acceptance remains FAIL / DYD-434-blocked.

## Custody and preserved history

The isolated `DYD-415-statechart` checkout began clean at
`bc5f120b6da58670def58e6403e55feb7fcc0267`, with reviewed
`b08ab4417aeb7fba61dd2e559379960a79b6b53d` an ancestor. `custody.json`
records the five custody checks and owned paths. Authored changes are confined
to integration3 and the minimal top-level discovery pointer.

Merge `590f2204e541ddc1de66d58fadc91001ed3d2009` retains both ordered parents:
reviewed b08ab441 and master `f3e0a161886621395f2dba47b689fba19f3bf3de`.
Runtime `0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6` remains unchanged; its
421-file statechart tree is `10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d`.
Catalogs remain 41 total / 13 engineering. No history rewrite occurred.

`historical-hashes.json` pins **740 files** at bc5f120b: fix1 through fix4,
integration1 and integration2, including their attributes, declarations, original
reviews and failed attempts. Every committed and current working byte remains
unchanged. The original integration1 whitespace failure (exit 2, 614 diagnostics)
is preserved. `final-identities.json` and the clean-commit audit recheck custody.

## Reference-derived closure and limits

I1 is a false acceptance in integration2: its 476 declared identities omit six
known TS assurance producers/configuration files. Their reviewed working hashes
were already retained in fix1 `producer-hashes.json`. Historical results remain
valid for the unchanged source; their omission broke promised drift rejection.

Before successor recipe edits, `prepare.py` writes `closure-declaration.json`
and `dependencies.json` from reviewed Git objects/receipts. It preserves the
sound 476 rows and adds six producers and 37 directly consumed retained scripts
or inputs. `closure-declaration.json` lists exact read/import edges, including
the TS source manifest, walked callables, platform Istanbul counters, final LCOV,
unfiltered static reports and native coverage/PDB join inputs.

A final import audit also found `gate_inventory.py:6` importing repository
`inventory.py` at load time. `complete.py` preserves the entire first successful
519-row attempt under `initial-attempt/` before declaring this additional edge
in `closure-addendum.json`. No material failure triggered a retry. That module
imports only standard-library modules. Its unused inventory functions' inputs
are outside the retained `dependency_cycles` call path and are not required.

The final recipe reads **`dependencies-complete.json`, 520 rows**:

| Class | Rows | Expectation provenance |
|---|---:|---|
| Runtime | 421 | Reviewed fix4 source-before receipt, statechart paths only |
| Configuration | 3 | Reviewed fix2 metric-tool source receipt |
| Native input | 7 | Reviewed extraction, exact C#, templates, selection and manifest |
| Proof input | 16 | Reviewed fix3 receipts/recipes and fix4 recipe modules |
| Metric tool | 25 | Reviewed native metric-source manifest and GateMetrics DLL receipt |
| Compiler | 1 | Reviewed compiler-binary receipt |
| Instrumenter | 3 | Reviewed Coverlet package and payload receipts |
| TS producer | 6 | Reviewed fix1 producer-hashes receipt, unchanged working expectations |
| Retained input | 38 | 37 reviewed retained Git objects plus inventory.py's reviewed Git object |

The six producer paths are `DynaDocs.Tests/coverage/` followed by
`js_metrics.cjs`, `typescript_join.py`, `gate_inventory.py`, `knip_reporter.mjs`,
`package.json` and `package-lock.json`. Each row carries its exact expectation
and provenance. None of the expectations is generated from candidate drift.

The six reviewed receipt hashes describe CRLF Windows working bytes, distinct
from their canonical Git blob hashes. Inventory.py has no retained working-hash
receipt: its expectation is derived explicitly from its reviewed LF Git object
by rendering LF as CRLF, before checking current bytes. Its canonical hash and
Git blob are recorded separately. Retained evidence uses byte-preserving Git
attributes and is hashed directly from reviewed Git objects.

The recipe retains exact-root, complete cleanliness, fresh output and disjoint
input/output protections. After checking every identity, it verifies that every
entry in the checked reviewed producer receipt has a matching declared source
row and expectation. This prevents an accidentally partial producer declaration.
Unrelated skill changes remain permitted. Portable tool locators require exact
copied bytes. The original excerpt offset/length/hash check remains in place;
the C# excerpt SHA-256 is
`d73528c22df450659cbb6ce42ced18bb3fe15d081b850278e6425187bce476ee`.

This is scoped applicability of retained TS reports and exact-source native
identity evidence. It neither reconstructs an installed npm/SDK/environment
dependency graph nor claims future toolchain equivalence. The driver exposes
only preflight and records `measurementExecuted:false`. Original Add-Type
integration, separately compiled exact-C# Coverlet measurement, and the PDB/native
identity join remain distinct historical layers. AltCover remains FAILED.

## Executed proof recipe

All material phases use a normal Windows TEMP supervisor, default live LC FIFO,
then nonwaiting Heavy, then exclusive `TEMP/dydo-fullsuite.lock`. Inherited
`LC_SUITE_SLOT_TICKET` is preserved. The supervisor waits for children and
propagates failure; every child launch requests `CREATE_NO_WINDOW`.
This proves launch configuration, not desktop-window observation.

The originally named LC worktree was absent; the first writer turn correctly
stopped without edits or probes. The captain authorized read-only
`C:/Users/User/Desktop/LC`, HEAD `36d79de8765ff4b703e9f0be892f93a8c3a9c649`.
The replacement suite_slot, machine_lock and test-runner documentation hashes
match the authorization and are checked before every admission. Imports disable
bytecode writes; LC is not modified. All reservation/lock/release receipts name
the real `C:/Users/User/AppData/Local/lc-test/suite-slot` namespace.

A shell wrapper was rejected before launch for variable expansion; no probe
executed. `prelaunch-denial.json` retains that event. A literal-path TEMP Python
launcher then starts the supervisor hidden with exact exit propagation.

Commands executed from the owned checkout:

```text
python .../integration3/prepare.py
python C:/Users/User/AppData/Local/Temp/dyd415-integration3-launch.py red
python C:/Users/User/AppData/Local/Temp/dyd415-integration3-launch.py probes
python .../integration3/complete.py
python C:/Users/User/AppData/Local/Temp/dyd415-integration3-launch.py probes-complete
```

The initial launcher/supervisor bytes for `red` and `probes` are retained in
`initial-attempt/`; final launcher accepts `probes-complete` instead of `probes`.
Each supervisor/recipe command receipt includes resolved arguments, hashes,
input HEAD, ticket, exit and raw streams. Copy the corresponding retained
supervisor/launcher to the named TEMP paths before reproducing a historical
phase. Recipes reject existing outputs and receipts rather than overwriting.

| Observation | Result | Evidence |
|---|---|---|
| Historical integration2 on clean committed js_metrics change and deletion | Both incorrectly accepted; two RED observations | historical-red.json |
| First declared closure | 9/9 methods, 116 outcomes, 519 identities; restored | initial-attempt/ and original recipe-outcomes.json |
| Completed closure | 9/9 methods, zero failures/errors, exit 0 | recipe-tests-complete.command.json and raw stderr |
| Each of six omitted producers changed and deleted in clean commits | 12/12 dependency-specific rejections | recipe-outcomes-complete.json |
| Each of 38 added direct inputs changed and deleted in clean commits | 76/76 dependency-specific rejections | recipe-outcomes-complete.json |
| Earlier seven identity classes changed and missing | 14/14 rejections | recipe-outcomes-complete.json |
| Remove each of six producer declaration rows | 6/6 completeness rejections | recipe-outcomes-complete.json |
| Clean integrated source; committed unrelated skill change | Both accepted | recipe-outcomes-complete.json |
| Dirty/nested source, foreign/overlapping/stale outputs | All eight rejected | recipe-outcomes-complete.json |
| Admitted final driver preflight | Exit 0, inherited parent admission, measurementExecuted:false | recipe-preflight-complete.command.json and receipt |

There are **118 final outcomes**, including **102 changed/missing rejections**;
subcases are not additional unittest methods. New input probes assert the exact
dependency path/class message, record clean committed HEADs and changed/missing
identities, then restore bytes and commit. A dirty/root/prerequisite failure
cannot satisfy those assertions. The scratch identity differs from the source
candidate and is never presented as its Git commit.

`probes-complete-input.json` records the copied index paths, clean initial
scratch HEAD/tree, and executed recipe hashes. Ordinary ignored paths are force
added only in the owned copied scratch, preserving all copied index inputs.
`probe-restored-complete.json` records the final clean scratch, equal initial
and final tree, and 520 matched dependencies. `recipe-outcomes-complete.json`
also retains every restored dependency hash, including copied tools. Installed
tools and the source checkout were never mutated. Earlier scratch and first
attempt remain available; no foreign process/lock was touched.

## Final checks and unchanged acceptance holds

After coherent recipe/declaration, explanation and pointer edits, run:

```text
python C:/Users/User/AppData/Local/Temp/dyd415-integration3-launch.py final
```

This performs only installed dydo 3.5.1 `dydo.exe check`,
`git diff --check bc5f120b6da58670def58e6403e55feb7fcc0267`, and
`git diff --check f3e0a161886621395f2dba47b689fba19f3bf3de`.
`document-input.json` pins this exact document and the top-level pointer before
validation. Command receipts/raw streams and `final-result.json` retain actual
exits and document identity; this document is not rewritten after that phase.

After the one owned fix commit, a normal-TEMP copy of `committed.py` runs as
`python C:/Users/User/AppData/Local/Temp/dyd415-integration3-committed.py`.
It launches its supervisor hidden and checks both explicit base-to-final-SHA
whitespace ranges, clean custody, original history, relevant identities,
unchanged documents and exact executed recipe bytes. Its receipt is
`C:/Users/User/AppData/Local/Temp/dyd415-integration3-committed-check.json`,
outside Git to avoid a circular self-hash. The captain return supplies final SHA
and actual exits. All final operations retain normal FIFO/Heavy/dydo custody.

Local attributes follow integration2's narrow CR-at-EOL rule. They preserve raw
streams and still check real trailing spaces/tabs, EOF blanks and spaces before
tabs. No historical attributes or receipts change.

Prior source-bound setup 24/24, catalog/projection, platform, native and four
scenario-value results remain historical evidence under their existing limits;
their runtime/configuration identities remain unchanged. No setup, scenario,
native instrumentation, platform or broad measurement campaign was repeated.
These bookkeeping cases do not constitute full changed-code mutation.

DYD-434's PowerShell executable-line/branch/cognitive/HCRAP evidence and separate
full changed-code mutation remain UNAVAILABLE human acceptance choices. CC is an
HCRAP input, not a standalone CC20 gate. The deferred janitor nit is unchanged
and unproven at runtime. No whole-ticket PASS, readiness, waiver, PR, push,
watch, landing, publication, mode change or walkthrough is claimed or performed.

## Related

- [Historical integration2](../integration2/verification.md)
- [Preserved stopped integration](../integration1/verification.md)
- [Reviewed fix4](../fix4/verification.md)
- [Historical exact-source Coverlet proof](../fix3/verification.md)
- [Top-level verification](../../dyd-415-statechart-verification.md)
