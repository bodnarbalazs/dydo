---
area: project
type: reference
---

# DYD-415 current-master integration evidence

Author: code-writer/model: gpt-6.1-sol/high, for the existing DYD-415 captain.
Commission: DYD-415 description as of `2026-10-30T11:30:16.521Z`, retained verbatim
with the writer brief in `commission.txt`. This is local integration preparation.
DYD-434 still blocks acceptance; this record grants no readiness or whole-ticket PASS.

**Stopped on a new combined-gate failure:** whitespace exited **2**, reporting
614 trailing-whitespace diagnostics in generated integration JSON receipts.
Discovery and docs passed; whitespace did not. No fix or retry was attempted.
The proof successor preserves this failed attempt for the captain's disposition.

## Exact integration and custody

The clean `DYD-415-statechart` checkout at
`C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f` started at
`b08ab4417aeb7fba61dd2e559379960a79b6b53d`. `origin/master` was reconfirmed at
`f3e0a161886621395f2dba47b689fba19f3bf3de`; the merge base was
`8553172dfaa7601d34f6b884c10350183f9faaa5`. Neither `MERGE_HEAD` nor `index.lock`
existed. The two sides changed 1,147 and 73 paths respectively, with zero overlap.

Executed once:

```text
git merge --no-ff --no-edit -m "DYD-415 merge: integrate pinned master f3e0a161" f3e0a161886621395f2dba47b689fba19f3bf3de
```

Git's `ort` strategy completed without conflicts, exit 0. Merge candidate:
`590f2204e541ddc1de66d58fadc91001ed3d2009`. Its ordered parents are:

1. `b08ab4417aeb7fba61dd2e559379960a79b6b53d`
2. `f3e0a161886621395f2dba47b689fba19f3bf3de`

`identity.json` records all 73 incoming paths and their matching master/merge Git
entries. The merge differs from the reviewed source in exactly those paths.
Canonical statechart, both formal-verification references, notices, control-flow
and glossary references, changelog and all retained evidence have identical Git
objects to `b08ab441`. No conflict resolution or runtime edit was needed.
The later proof commit authors only `integration1/**` and the minimal discovery
pointer in the top-level verification document. Its SHA is the captain return's
final candidate; it preserves the merge and both parents without amendment.

## Shared seams and upstream applicability

Master adds `verify-dydo` and changes unrelated skills and catalogs. The combined
setup discovers **41 skills**, creating two local projections for the new skill.
Master's imported catalog text describes its 40-skill set and does not enumerate
this branch's added statechart. That textual count/list discrepancy is retained
for the captain/reviewer: imported files remain verbatim, and discovery uses the
filesystem rather than those README counts. No global installation was performed.

The setup algorithm and its test are identical at both parents and the merge.
The fresh combined discovery check therefore covers the added canonical skill
and its interaction with both flat host roots. Statechart still resolves to
`skills/engineering/statechart` in both roots, with
`disable-model-invocation: true` and `allow_implicit_invocation: false` intact.

The imported `.NET` runner change rejects `--filter` after the runsettings `--`
separator. Retained statechart Node runs bypass that runner; the historical
Coverlet campaign's direct `dotnet test` command also bypasses it and contains
no misplaced filter. Its test/source inputs are unchanged. The new verification
driver is a separate CLI journey driver; it was neither launched nor substituted
for the statechart drivers. No affected runtime dependency justifies another
native, scenario, Linux, Windows, viewer or full platform campaign here.

Master's safe `dydo merge` command handles reviewed PRs into feature branches.
The imported merge resource explicitly retains local Git for other merges;
this pinned master-into-ticket merge is local and has no PR. Imported guard,
mode and overnight content was preserved without execution or authored edits.

## Retained proof applicability ledger

All rows below are **historical observations inspected and source-bound here**,
not newly executed tests. Runtime pin:
`0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6`; canonical statechart tree:
`10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d`. That complete tree is identical
at the runtime pin, reviewed `b08ab441` and merge. All **421** statechart working
file SHA-256 values match the retained fix4 `source-before.json` manifest.

| Retained proof and command witness | Candidate/source binding | Applicability and limit |
|---|---|---|
| Windows `node --disable-warning=ExperimentalWarning --test --test-concurrency=1 test/*.test.ts`; Linux container runs `npm test`; [fix1 record](../fix1/verification.md), `windows.json`, `linux.json` and `linux.sh` | Final fix1 runtime is `0ddbbf3`; original suite, acceptance tests, launchers and package command are unchanged | Source-bound Windows/Linux functional and TLC/shortest-path/budget proof retained, including original platform skips. No suite rerun or new platform claim. |
| Strict TS via `node .../typescript/bin/tsc -p skills/engineering/statechart/scripts/tsconfig.json`; fix1 coverage/static commands in `coverage-gate-final.json` and `static-audit-final.json` | Same six maintained TS runtime modules, tests, configurations and retained tool records | Scoped TS/static/coverage/HCRAP evidence retained. Original unfiltered vendor diagnostics and nonzero exits remain as recorded; no new global assurance PASS. |
| Original Add-Type failure/cleanup integration; [fix2 record](../fix2/verification.md), `original-addtype-failures.command.json` | Unchanged `windows-job.ps1`, embedded C# and original tests | Original native integration remains its own evidence layer. |
| Direct `dotnet test .../Adapter.Tests.csproj -c Debug --no-build --no-restore ...`; Coverlet 6.0.4 command in fix3 `coverlet.command.json`; [fix3 record](../fix3/verification.md) | Fix3 source candidate `f0cfa5064eeb544cf2a7438ebcfeb51ec90c6c35`; runtime `0ddbbf3`; exact extracted C# SHA-256 `d73528c22df450659cbb6ce42ced18bb3fe15d081b850278e6425187bce476ee` | Separately compiled byte-identical C# baseline 6/6 and instrumented 6/6, errors 87/5 preserved, lines 15/15, branches 6/8, cognitive/HCRAP 5 retained. This does not instrument the original Add-Type assembly. |
| Historical AltCover campaign; fix2 `instrumentation-audit.json` and [fix3 comparison](../fix3/verification.md) | Same exact source; original reports unchanged | Semantic parity remains FAILED; successful Coverlet does not relabel AltCover. |
| Three selected `node --test --test-name-pattern=... acceptance.test.ts` RED/restored pairs plus retained fix1 pair; fix4 `scenario-ledger.json` and `*.command.json` | Fix4 source `fb047aa7c41b97277aa75620677d221d882e4f5e`, reviewed candidate `b08ab441`; runtime/original acceptance bytes unchanged | Four scenario-value pairs retained. Binding bounded correction review PASS and DYD-436 Done remain the captain's supplied disposition. These pairs are separate from full changed-code mutation. |
| `python .../recipe/test_recipe.py` and `python .../recipe/driver.py preflight`; fix4 `recipe-tests.command.json`, `recipe-preflight.command.json`, `recipe-preflight/preflight.json` | Same fix4 source and unchanged five recipe modules; historical preflight checked the then-current entire skills manifest | Historical 5/5 bookkeeping and clean external preflight remain valid observations. Measurement mode was not rerun. Exact whole-skills preconditions cannot be asserted for this merged host catalog. |

The [binding fix4 review](https://linear.app/bodnar-balazs/document/dyd-415-fix4-independent-correction-review-b08ab441-b36aa2082e81)
is the supplied independent review, not this writer's verdict. `identity.json`
names the **11 changed pre-existing skills/catalog files** relative to fix4's
whole-skills manifest; master also adds the new verify-dydo files. Historical
global manifests and clean-source recipe preconditions therefore must not be
presented as freshly passing the integrated tree. Scoped statechart identities
remain exact. No historical receipt was edited or replayed as new execution.

## Fresh combined checks

Windows, Python 3.13.1, Node **22.23.2**, installed `dydo` **3.5.1**.
`tools.json` records resolved executables and SHA-256 values. The dydo executable
is `C:/Users/User/.dotnet/tools/dydo.exe`, SHA-256
`a03823fd118f4e8c30cf72a8f0674e6321bc1fa3375352c54273404b56ca5654`.
This is an identified installed documentation validator, not a claimed build of
the merge. Check-command/validation source was unchanged by the integration;
a current-source build is unnecessary for this bounded docs check.

| Command/check on merge HEAD | Result | Raw evidence |
|---|---|---|
| Exact parents, disjoint paths, imported Git entries and scoped identities | All asserted; no mismatch | `identity.json` |
| `node --test DynaDocs.Tests/coverage/tests/setup-skills.test.cjs` | Exit 0; 24/24, zero skips/failures | `selection.json`, `setup-tests.command.json`, `setup-tests.stdout`, `setup-tests.stderr` |
| `node setup-skills.mjs` | Exit 0; 41 skills, two projections created, zero deleted links | `setup.command.json`, `setup.stdout` |
| Both statechart projections and explicit-invocation metadata | Both resolve canonical source; metadata intact | `projections.json` |
| `dydo.exe check` against the completed evidence document and pointer | Exit 0; 854 files, zero errors/warnings. Subsequent failure-report additions were not rechecked. | `docs.command.json`, `docs.stdout`, `docs.stderr`, `document-input.json` |
| `git diff --check 590f2204e541ddc1de66d58fadc91001ed3d2009` after staging authored evidence | Exit 2; 614 trailing-whitespace diagnostics; STOP | `whitespace.command.json`, `whitespace.stdout`, `whitespace.stderr` |

`checks.py` was copied unchanged to normal Windows TEMP and run in two phases:

```text
python C:/Users/User/AppData/Local/Temp/dyd415-integration1-checks.py discovery
python C:/Users/User/AppData/Local/Temp/dyd415-integration1-checks.py docs
```

Both phases use the existing LC checkout's default live `suite_slot.book` FIFO,
preserve inherited tickets, then take nonwaiting Heavy and exclusively create
normal `TEMP/dydo-fullsuite.lock`. The parent remains alive to child exit;
Python children use `CREATE_NO_WINDOW`. The unchanged setup test's 33 nested
`spawnSync` launches receive `windowsHide:true` from `setup-hidden.cjs`; the
preload changes only console visibility, never arguments, assertions or results.
`setup-launches.jsonl` audits these launches. This proves launch configuration,
not desktop-window observation. `*-reservation.json` and `*-locks.json` retain
ticket and ownership receipts; discovery also has explicit release receipts.
Whitespace's nonzero exit unwinds the docs lock/context but skips its normal
post-phase release receipts. Read-only inspection after process exit confirmed
the owned docs FIFO ticket and normal TEMP dydo lock were absent; `stop.json`
records that observation. No other owner's lock/process was removed. No
measurement adapter, installation or mode setter ran.
`discovery-driver-delta.patch` retains the only subsequent driver edit: stage the
authored evidence and check whitespace from the merge before the unused docs
phase executes. The discovery phase and its test logic did not change or rerun.

## Whitespace failure and stop

The first diagnostic is `discovery-locks.json:1: trailing whitespace`.
The JSON writer uses Python's default Windows text newlines (CRLF). The existing
parent evidence `.gitattributes` preserves exact bytes with `* -text`; unlike
historical fix1's local attributes, integration1 has no `cr-at-eol` whitespace
attribute. `git check-attr text eol` reports text unset and eol unspecified for
the generated receipts. Those facts explain the observed CR-at-line-end
diagnostics without implying any runtime mismatch. The raw failed command,
stdout and stderr remain unchanged. No normalization, attribute change, threshold
override, hidden fix or gate retry was performed.

The captain must authorize any correction and its focused recheck. The final
proof commit contains this failure-report update after the docs check; that
update is not claimed as documentation-validated. The failed gate and its
post-check reporting are returned explicitly rather than labeled passing.

## Remaining gates and hold

First resolve the new integration-evidence whitespace failure through the
captain. The captain then commissions the fresh bounded integration review on
the clean successor. The inherited overall acceptance hold remains DYD-434:
PowerShell executable-line/branch/cognitive/HCRAP evidence, with CC as input,
and separately full changed-code mutation remain UNAVAILABLE. CC20 is not a
standalone gate. No waiver, tooling expansion or acceptance decision was made.
The source-based janitor early-rejection nit remains deferred and unproven at
runtime. No walkthrough, push, PR, watch, master landing or publish occurred.
Any later PR/CI/readiness and human master landing remain captain/user work.

## Related

- [Top-level verification](../../dyd-415-statechart-verification.md)
- [Reviewed fix4 evidence](../fix4/verification.md)
- [Supported exact-source Coverlet evidence](../fix3/verification.md)
- [Working-tree contract](../../../../guides/working-tree-contract.md)
