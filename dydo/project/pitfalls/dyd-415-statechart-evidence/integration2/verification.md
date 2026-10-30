---
area: project
type: reference
---

# DYD-415 integration evidence correction

Author: code-writer/model: gpt-6.1-sol/high, for the existing Ticket Captain.
Commission: `commission.txt`, expanded by `expanded-authorization.txt`, live
DYD-415 contract `2026-10-30T11:43:02.991Z`. This is a bounded correction and
preflight record. Independent integration review and DYD-434 acceptance remain
pending. This record grants no readiness or whole-ticket PASS.

## Identity and preserved failure

The clean isolated `DYD-415-statechart` checkout started at
`a18c2fcdbb9054880f5fa1179fdcf5e6f3309ffe`. Its conflict-free merge
`590f2204e541ddc1de66d58fadc91001ed3d2009` retains ordered parents
`b08ab4417aeb7fba61dd2e559379960a79b6b53d` and
`f3e0a161886621395f2dba47b689fba19f3bf3de`. No history was rewritten.
The final clean correction SHA is supplied in the captain return; gate receipts
name their input HEAD and document hashes rather than inventing a self-hash.

`historical-hashes.json` compares all **695** retained files in integration1 and
fix1 through fix4 with their exact Git bytes at `a18c2fc`. Its **36 integration1
files**, including `verification.md`, `stop.json`, drivers and all raw streams,
remain unchanged. `final-identities.json` rechecks these hashes at the final gate.
The new integration1 `.gitattributes` is the only addition in that folder.

The original whitespace command still records **exit 2 and 614 diagnostics**.
`diagnosis.json` records actual line endings and hashes: integration1 has no
space/tab-at-EOL violations after distinguishing terminal CR from content.
The parent `* -text` preserved CRLF receipt bytes without declaring CR-at-EOL
acceptable. The correction uses the prior fix4 convention locally:

```gitattributes
* -text whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol
```

Only CR at EOL becomes acceptable; actual trailing blanks, blank lines at EOF
and spaces before tabs remain checked. No receipt normalization or new `-diff`
exclusion was necessary. New authored files and JSON writers use explicit LF;
raw command streams retain their observed bytes under the same narrow rule.
The inherited raw `.txt -diff` convention retains the verbatim commissions.

## Catalog reconciliation

Only the authorized statechart registration and resulting totals changed in
`skills/README.md` and `skills/engineering/README.md`: **9 + 13 + 19 = 41**.
The statechart entry uses its own frontmatter description and user-invoked mode.
All imported entries, including verify-dydo, remain present. The other imported
master files remain untouched. `inventory.json` checks the actual canonical
inventory, exact engineering list, and both host projections without rerunning
setup. It retains the original projection receipt with matching SKILL and Codex
metadata hashes.

Integration1's setup command remains its original **24/24**, exit 0 observation,
with 41 skills and 33 hidden nested launches. Nothing here reruns or relabels
that observation. Original integration1 text describing outstanding catalog and
recipe scope is retained as dated history, superseded by this authorized hop.

## Reference-derived dependency closure

Before editing the new recipe, `prepare.py` wrote `dependencies.json` from
reviewed **b08ab441** Git objects and retained reviewed manifests. Expected
hashes never come from current candidate files. Each of its **476 rows** names
the exact path, SHA-256, identity class, location and reference provenance.

| Class | Declared dependency and expectation source |
|---|---|
| Runtime | All 421 statechart files from fix4 `source-before.json`, scoped to `skills/engineering/statechart/`; no other skill is required to match the old catalog. |
| Configuration | `Directory.Build.props`, `.editorconfig`, `DynaDocs.Tests/coverage/SonarLint.xml`, using fix2 `metric-tool-sources.json`. |
| Native input | Fix2 extraction metadata, exact C# excerpt, both project templates, six-test template, test selection and metric-source manifest, pinned to their reviewed Git objects. |
| Proof input | Retained fix3 driver/finalizer/report, package lockfiles, compiler/settings/tool/native join receipts, and fix4 recipe modules, pinned to reviewed Git objects. |
| Metric tool | Every source/config/lockfile in fix2's metric manifest plus the GateMetrics DLL hash accepted by fix3 `metric-tool-reuse.json`. |
| Compiler | SDK 10.0.300 `Roslyn/bincore/csc.dll`, SHA-256 from fix3 `compiler-binary.json`. |
| Instrumenter | Coverlet 6.0.4 package and the two recorded console/core DLL payloads, using fix3 package/payload receipts. |

Runtime pin remains `0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6`; the complete
statechart tree remains `10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d`.
The excerpt retains SHA-256
`d73528c22df450659cbb6ce42ced18bb3fe15d081b850278e6425187bce476ee`.
The new preflight also verifies the original byte offset/length extraction.
This is applicability of retained observations, not reconstruction or execution
of an installed SDK/package dependency graph or a new instrumentation campaign.
Original Add-Type integration and the separately generated exact-C# assembly,
PDB/native identity join and Coverlet counters remain distinct proof layers.
Historical AltCover remains **FAILED**; exact-source Coverlet remains supported.

`recipe/config.py` checks the exact resolved checkout root and a completely clean
Git tree, then every declared dependency. Missing or changed input fails.
Portable metric/tool locators may point to copied inputs, but every byte must
match the reference expectation. Fresh work must be below normal Windows TEMP;
work/evidence must be disjoint from one another, the checkout and tool inputs.
Existing outputs are rejected rather than cleaned or overwritten. The new
`recipe/driver.py` is **preflight-only**, requires a live admitted parent, and
records `measurementExecuted:false`; it exposes no measurement mode. Historical
fix1 through fix4 recipes and measurement receipts remain untouched.

## Bounded execution and proof recipe

The supervisor is copied byte-for-byte to normal Windows TEMP and invoked as:

```text
python C:/Users/User/AppData/Local/Temp/dyd415-integration2-checks.py probes
python C:/Users/User/AppData/Local/Temp/dyd415-integration2-checks.py final
```

Both phases use LC's unredirected live `suite_slot.book` FIFO, retain any inherited
ticket, then take nonwaiting Heavy and exclusively create normal
`TEMP/dydo-fullsuite.lock`. The parent stays alive until every child exits;
all subprocess launches use `CREATE_NO_WINDOW`. This is a launch-configuration
claim, not a desktop observation. `*-reservation.json`, `*-locks.json` and both
secondary/FIFO release receipts retain custody. No foreign lock/process is
removed. `queue-access.json` records the initial sandbox denial before admission;
the unchanged driver then ran with narrowly escalated queue/Git access.

The probes copy 2,761 index paths into a new owned disposable Git checkout,
preserving working bytes, and create a clean snapshot containing the integrated
catalog and new preflight. Its identity is in `probe-input.json`. Only copied
dependencies are mutated; the source checkout, retained evidence and installed
tools are never mutated. Scratch commits make relevant source drift clean so
identity rejection is tested independently of dirty-tree rejection.

| Command or assertion | Observed result | Evidence |
|---|---|---|
| `python .../integration2/recipe/test_recipe.py` | Exit 0, 7/7 test methods, zero failures/errors | `recipe-tests.command.json`, raw stdout/stderr |
| Changed and missing input in each of seven classes | 14/14 expected rejections | `recipe-outcomes.json` |
| Clean integrated source; committed unrelated verify-dydo change | Both accepted | `recipe-outcomes.json` |
| Dirty source, nested checkout root, stale work/evidence, foreign/overlapping outputs | All rejected | `recipe-outcomes.json` |
| Restore every scratch mutation | Clean; all 476 dependencies exact | `recipe-outcomes.json`, `probe-restored.json` |
| Admitted `recipe/driver.py --source ... --work ... --out ... --normal-temp ... --metric ... --lc ... --tools-map ...` | Exit 0; inherited admission; measurement not executed | `recipe-preflight.command.json`, `recipe-preflight.json` |
| Inventory and retained projections | 41 total, 13 engineering; both exact | `inventory.json` |

The full resolved arguments are retained in each command receipt. Scratch is
retained at the owned path in `probe-input.json`; no process survives the driver.
The bounded tests do not constitute full changed-code mutation assurance.

## Final gates and remaining holds

All substantive recipe, catalog, explanation and pointer edits precede `final`.
`document-input.json` pins the exact final authored documents. That phase runs
only `dydo.exe check`, then stages owned evidence and runs
`git diff --check a18c2fcdbb9054880f5fa1179fdcf5e6f3309ffe` for the correction
and `git diff --check f3e0a161886621395f2dba47b689fba19f3bf3de` for the complete
ticket against pinned master. `docs.command.json`,
`whitespace-correction.command.json`, `whitespace-ticket.command.json` and their
raw streams supply the actual exits; `final-result.json` records those outcomes
after locks are released. This document is the input to those checks and is not
rewritten afterward to predict or manufacture a passing result.

No statechart scenario, native measurement, setup, platform or full campaign is
rerun. All retained proof applicability and limits in integration1 remain valid
with the two explicitly corrected catalog/preflight exceptions above. The next
gate is the captain's fresh independent correction/integration review.
DYD-434's PowerShell executable-line/branch/cognitive/HCRAP evidence (CC as an
input, no standalone CC20 gate) and separate full changed-code mutation remain
**UNAVAILABLE** and block acceptance. Human acceptance/master landing holds are
unchanged. No push, PR, watch, walkthrough, mode change or publication occurred.

## Related

- [Historical stopped integration](../integration1/verification.md)
- [Reviewed fix4 correction](../fix4/verification.md)
- [Historical Coverlet proof](../fix3/verification.md)
- [Top-level verification](../../dyd-415-statechart-verification.md)
