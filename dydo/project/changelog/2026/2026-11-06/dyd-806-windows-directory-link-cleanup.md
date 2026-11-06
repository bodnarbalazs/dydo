---
area: project
type: changelog
date: 2026-11-06
---

# DYD-806 - Remove the directory link before recursive fixture teardown

## Summary

`GuardPrReadyBodyFileTests.LinkedDirectoryLeavingTheTree_IsNull` now removes its owned
directory link with non-recursive `Directory.Delete(link)` in `finally`. The `Assert.Null`
and unconditional `[Fact]` remain. On Windows the link is a junction; leaving it for the
fixture's recursive teardown caused `IOException: The parameter is incorrect. : 'linked'`
in `GuardPrReadyBodyFileTests.Dispose`.

## Changed

- `guard-windows` selects this exact method alongside its existing six classes. Its
  siblings with intentional Windows skips are not selected.
- The TRX check requires a nonzero result count for the exact class and method from the
  test definitions. Every selected result must still pass, and all existing class and
  counter checks remain.
- Focused workflow tests execute the actual PowerShell selection and TRX-check scripts,
  including missing, skipped, failed, and similarly named results. Product behavior,
  coverage targets, and production mutation scope are unchanged.

## Proof

The captain supplied the existing Windows red from
[run 42168408198, job 127730931333](https://github.com/BalazsOrg/dydo/actions/runs/42168408198/job/127730931333)
at `49cd8dc7`: the regression failed in recursive `Dispose` after 188 ms. This existing
test is the reproduction. Redundant minimization, hypothesis instrumentation, and a local
revert-red are omitted because the failure is already localized to Windows junction
cleanup and this writer ran on Linux. Hosted Windows green remains CI and captain-owned.

Before the workflow change, the focused workflow tests failed four cases: the method
was absent from the filter, and missing, same-class sibling, and wrong-class results were
accepted by the TRX check. The eight existing-pass/failure checks passed.

Local Linux proof after the change, with SDK 10.0.303, Node 26.13.0 and PowerShell 7.6.9:

- Locked restore and `dotnet build DynaDocs.sln --no-restore --warnaserror
  --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false`: exit 0,
  zero warnings and errors.
- `dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-build --no-restore
  --disable-build-servers --list-tests --filter
  'FullyQualifiedName~DynaDocs.Tests.Commands.GuardPrReadyBodyFileTests.'`:
  exit 0, 30 tests discovered, including the exact regression.
- The same selection without `--list-tests`, with a TRX logger: exit 0,
  30 passed, zero failed or skipped.
- `node --test DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs`:
  exit 0, 12 passed, zero failed or skipped, against the actual workflow scripts.

All runs used the shared gate lock, `app-t3code-agents.slice`, memory caps (4 GiB
for probes, 8 GiB for the gate), no swap, `OOMPolicy=continue`, and bounded timeouts.
.NET node and compiler server reuse was disabled. Raw logs, TRX, tested source hashes,
and the runnable gate script are retained under `/tmp/dyd806-writer` for the captain.

## Related

- [DYD-806](https://linear.app/bodnar-balazs/issue/DYD-806)
- [Hosted Windows guard CI](../2026-11-05/dyd-681-windows-guard-ci.md)
- [Changelog](../../_changelog.md)
