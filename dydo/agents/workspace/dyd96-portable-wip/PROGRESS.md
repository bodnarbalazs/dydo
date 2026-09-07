# DYD-96 portable UNREVIEWED WIP handoff

This packet preserves the halted implementation state on `codex/DYD-96-assurance-adoption`.
It is **UNREVIEWED WIP**: it is not `IMPLEMENTED`, SPEC reapproval, independent review, G-final, PASS, or Done.

## Pinned contract

- Starting HEAD: `de702c5b1e29dd6b9ac38ce9caf64df4ed02c9a7`; direct base parent: `37acc1f3706bf925e5230fb58c4a790dba769d62`.
- Accepted specification SHA256: `64B3EDCEA70805E8E115E005AEA7DB191F185B357323F52546AE336A91C1D4B2`.
- SPEC PASS SHA256: `797FC52B1D26152C6599E266F9D0D641CCDE7695DB2AA14B7C39E102D8B1716A`.
- Producer brief SHA256: `58941B62CE99CA2C623D99B43423F8B6AD80BB2713A55C7A9218822245CEDB8C`.
- 101-path ownership manifest SHA256: `4FD8495BAD4DEC0C82895A96006A6EEDE7589E6E8BDEA45D2D097AB18EC6CC1F`.
- Retained disposition SHA256: `EB3E051A86EE2A7EA76887703E999C5E4CE444B5B5C3D5CD84F0B71F2A7A4631`.
- All 82 disposition-named retained working-byte inputs were read-only checked against their recorded sizes and SHA256 values before adoption: 82 match, 0 mismatch. The old `.worktrees/dyd96` was never edited, built, tested, or cleaned.
- All 61 disposition rows that name an adoption destination are represented in this checkpoint. The other 21 are explicitly `retain-not-adopted` retired broker/session/receipt/shadow/mutation sources and tests; they are deliberately absent and are not resume inputs. No disposition-sealed source needed to resume remains only in the old local worktree.
- Model identity used for this producer: `gpt-5.6-sol`.

The exact accepted inputs are copied beside this file. Their bytes retain the hashes above; `specifier-return.md` has SHA256 `D07A392E2A7D89D3E0BB054B7FA1C4CBBE9ABC3AE883D64BB2AD89FD7BAE2DBB`.

## Preserved source state

The WIP commit containing this packet is the exact dirty source checkpoint. Before the packet was added, Git reported 85 dirty production paths and no path outside the accepted production manifest. Five accepted production files had not yet been created:

- `DynaDocs.Tests/Steps/AssuranceAdoptionSteps.cs`
- `DynaDocs.Tests/coverage/sync_testing_example.py`
- `DynaDocs.Tests/coverage/test-associations.json`
- `DynaDocs.Tests/coverage/tests/test_report.py`
- `DynaDocs.Tests/coverage/tests/test_sync_testing_example.py`

`DynaDocs.Tests/coverage/tier_registry.json` also remains present and must be deleted under its accepted ownership action. Testing docs, coverage reference, glossary, `gap_check.json`, and `report.py` remain unfinished. The immutable feature file was not edited.

## Completed mechanism proof

- Public facade reserved-exit amendment, inventory and associations primitives have focused RED/GREEN proof. DR048 policy fixtures passed 25/25.
- Effective analyzer configuration has native failing/corrected fixtures for all five required diagnostics and evaluated product/test/metrics settings; focused analyzer tests passed 2/2.
- C# Roslyn/Sonar/PDB metric fixtures passed 12 selected non-shadow tests. The later NuGet-source slice has an explicit RED then GREEN: locked `Microsoft.NET.Test.Sdk.Program.cs` is classified as `nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs`, while an arbitrary out-of-root `Compile` item still exits 2.
- Python runtime fixtures passed 13/13; Python metrics passed 9/9; Python native coverage/join fixtures passed 4/4 combined.
- JavaScript native CJS, ESM, child-only, never-imported, and same-line callback coverage fixtures passed 2/2. Node suite discovery passed 2/2.
- C# campaign raw collection produced collector and console JSON/OpenCover/Cobertura plus exact DLL/PDB/source hashes in an earlier probe. The final WIP probe additionally proved the Windows owner cleanup contract: complete true, cleanup confirmed true, job empty, all 43 held identities signaled.
- Common private adapter publication/normalization fixture passed 2/2.

## Actual static G-measure checkpoint

These are measured policy outcomes, not mechanism errors and not G-final:

- Python: `fail`, 48 findings, 0 errors.
- C#: `fail`, 67 findings, 0 errors.
- Node: 19 findings plus 1 known mechanism gap for extensionless `npm/bin/dydo`, assigned to DYD-105.

The local ignored raw reports and their hashes are recorded in `proof-summary.json`. They are intentionally not in this portable packet because they are large machine-local journals.

## Interrupted/incomplete coverage measurement

No command remained active when preservation began. The last C# coverage probe completed its owned process campaign and cleanup, then exited 1 during normalization. Its exact mechanism error was:

`Missing physical method coverage: System.Int32 Program/<>c::<<Main>$>b__0_0(System.CommandLine.ParseResult)`

The portable PDB/assembly producer reports maintained `Program.cs` points for that compiler-generated lambda, while Coverlet JSON reports only the enclosing async state-machine `MoveNext`. The producer Coverlet console reports in that filtered probe were empty. This is missing evidence/mechanism error, not a policy finding. The accepted specification requires a bounded route correction or specification correction; it forbids shadow compilation and false completion. Do not redesign around it without the captain/specifier decision.

The probe mistakenly used `--filter Category=Unit`, for which the repository has no matching tests. Therefore it is not full-suite or functional proof. The owner result itself is still valid cleanup evidence.

## Remaining work

1. Resolve or respecify the exact Coverlet compiler-generated-method limitation above, then prove the C# native full-suite join.
2. Run full repository Python and Node coverage and classify policy findings separately from evidence gaps. Fix the known nested outer Python coverage configuration inheritance in `test_python_coverage.py` before that full run.
3. Finish stable adapter publication with exclusive locks and the exact summary schema/path; wire all six static/coverage rows and required artifacts in `gap_check.json` while leaving mutation unavailable pending DYD-103.
4. Create and validate the exact source-to-test association manifest; finish facade feature bindings, derived-copy sync/check, normalized report renderer, docs, and obsolete tier-registry deletion.
5. Adapt and run newly copied `test_orchestration.py` and `test_windows_job.py`; rerun edited source-token and JavaScript suite tests. The Windows Job retained receipt/session tests were removed, but the remaining file has not been run after adoption.
6. Run both exact unittest interpreters, the full Node suite, isolated .NET tests, facade gates, copy parity, manifest-boundary checks, and a complete G-measure. No final implementation commit, independent review, G=0, or mutation M proof exists.

## Cleanup and concurrency state

- The producer owns no running test, coverage, `dotnet`, or VBCS process at handoff.
- Its last temporary worktree `C:/Users/User/AppData/Local/Temp/dydo-test-4559d50a` was removed by the runner after cleanup confirmation.
- Two other temporary worktrees at heads unrelated to DYD-96 remained registered and were deliberately preserved.
- Active DYD-111 and every other worktree/source owner were left untouched. The retained `.worktrees/dyd96` remains intact.
- Installed Python, Node, and .NET tool caches and ignored raw journals remain local-only by design. Resume restores them from the tracked exact lock/manifests; no cache or dependency directory is required from this laptop. The ignored raw report hashes and decisive observations are preserved in `proof-summary.json`.
