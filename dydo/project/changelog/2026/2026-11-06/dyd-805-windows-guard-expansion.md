---
area: project
type: changelog
date: 2026-11-06
---

# DYD-805: give the Bash expansion fixture its real host

## Change

The original three body-file expansion commands now enter the real hook with `--host claude`.
Each asserts the exact review-block warning and an allowed identical retry; the literal reviewed
body remains allowed. Separate full-pipeline cases retain PowerShell hook and Unknown CLI hard
denial on both attempts, including their different exit/JSON contracts and allowed literal controls.
Unmarked Bash and PowerShell retain warning/retry coverage for the other two expansion forms.
Production code, the lone unprefixed body-file rule (DYD-731 Option A), and DYD-736 prefix nudges
are unchanged. Guards prevent accidents; this adds no attempt to police deliberate bypasses.

`guard-windows` now selects the entire `GuardPrReadyTests` class. Its nonzero per-class discovery,
all-results-Passed requirement, and TRX counter checks are unchanged. The job enables Windows
Developer Mode so the file-symlink fixture can run. Link creation must succeed and the linked file
must contain the reviewed body before the guard is exercised; no exception becomes a skip.

All five skip sites have active replacements:

- Both absolute-path fixtures use native paths and explicit PowerShell on Windows, Claude Bash on
  Unix. A lone reviewed file passes; each original preceding-command form still warns.
- Both outside-link forms run: a file symlink and a directory junction/symlink.
- Placeholder fixtures use actual brace-named files on both platforms, with reviewed and unreviewed
  contents, warning/retry, and a plain reviewed-file control. Unix keeps its colon-named reviewed
  decoy. Braces are outside the body-file literal allowlist even inside quotes; Windows cannot
  represent the Unix colon filename.
- The case fixture measures the actual filesystem: two distinct case-sensitive worktrees must
  both be checked and named; case-insensitive aliases must expose both writes in one checked tree.
  Neither branch skips or returns before asserting the guard decision.

## Diagnosis

Base: `80b97b4dd6dcaa31bade1911e57cd3644d8d7a16`; branch:
`DYD-805-windows-guard-expansion`. The supplied native red was release run
[42168408198, job 127730931333](https://github.com/balazsorg/dydo/actions/runs/42168408198/job/127730931333)
on `49cd8dc7`, failing the original expansion test with a hard denial instead of the review warning.

An unmarked Bash hook selects Unknown on Windows. PowerShell treats the final backtick in
``gh pr create -F "dydo/agents/reviewed.md`printf .plain`"`` as escaping the closing quote.
`ShellReader` therefore cannot finish that reading; `PrCallScanner` returns the unreadable-shell
wrapper, and the PR-ready hard refusal precedes warning nudges. Marked Claude Bash selects POSIX
and warns/retries as intended. This is the documented unreadable-PR rule, not a product defect.

The captain's diagnosis also minimized the difference to ``gh pr create "`x`"``, independent of body
files and `-F`; adding a space before the closing quote made both readers finish. Inline review
text and a pre-armed warning marker did not clear the hard denial. Those earlier causal probes
informed the contract; this writer independently replayed the full-pipeline red and retained its
raw results below. Linux replay is parser-path evidence, not native Windows proof.

## Proof and replay

The implementation source is identified by SHA-256, so evidence from the dirty pre-commit tree is
bound to bytes, not merely its base HEAD:

| Path | SHA-256 |
| --- | --- |
| `DynaDocs.Tests/Integration/GuardPrReadyTests.cs` | `81250feeb23c7cedc20556b7fa54c2c0dba8fe737ebc2a9a3c5c981e4cf26cb9` |
| `.github/workflows/ci.yml` | `946fac3966317f95fea703a8ed9ec9a8fa39ad19da1024ed3d7a047d9af3928b` |

Local Linux toolchain: SDK 10.0.303, test runtime 10.0.14, Python 3.14.4; locked Python assurance
packages provisioned into `.static-gates/python`. Every test, probe, and gate entered
`/var/tmp/dydo-gate-lock.sh` before its systemd scope. Swap was disabled, OOMPolicy was continue,
and .NET reuse was disabled. The shared heavy hold was honored. No process environments were read.

Run these from the repository root, sequentially; the lock owns admission. All output paths are
owned DYD-805 scratch. The red recipe temporarily restores the pinned base test, inserts the
retained diagnostic, and restores the candidate through an EXIT trap. It deliberately exits 1;
rebuild afterward, since its assembly contains the diagnostic.

```bash
gate4() {
  /var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout "$@"
}
gate8() {
  /var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout "$@"
}
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
gate4 240 bash dydo/project/changelog/2026/2026-11-06/dyd-805-evidence/replay-red.sh
focus='FullyQualifiedName~GuardPrReadyTests.PrCreate_BodyFileOperandWithAnExpansion_Warns|FullyQualifiedName~GuardPrReadyTests.PrCreate_ValidExpansionsInDefaultAndPowerShellHooks|FullyQualifiedName~GuardPrReadyTests.PrCreate_BacktickBeforeClosingQuote'
light='FullyQualifiedName~Guard|FullyQualifiedName~PrReady|FullyQualifiedName~PrCallScanner'
gate4 240 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "$focus" \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=focused.trx' --results-directory /tmp/dyd805-writer-final-focused
gate4 120 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-build --list-tests --filter "$light"
gate4 600 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~DynaDocs.Tests.Integration.GuardPrReadyTests.' \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=class.trx' --results-directory /tmp/dyd805-writer-class-r2
gate8 1200 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --filter "$light" \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=light.trx' --results-directory /tmp/dyd805-writer-light
gate8 1800 .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static
gate4 240 env PATH=/tmp/dyd622-pwsh:/home/balazs/.dotnet:/usr/local/bin:/usr/bin:/bin \
  dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~NodeTestGuardTests|FullyQualifiedName~ReleaseWorkflowTests' \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=workflow.trx' --results-directory /tmp/dyd805-writer-workflow
```

The extra workflow command uses the already-provisioned PowerShell 7.6.9 at `/tmp/dyd622-pwsh`.
On another machine, put its supported `pwsh` on PATH. No tool was installed for this step.

| Observation | Exit | Result | Raw evidence |
| --- | --- | --- | --- |
| Original and reverted diagnostic | 1 | One failed test; PowerShell and Unknown hard denials disagree with the original warning expectation | [Red log](dyd-805-evidence/red.log.gz), [full responses](dyd-805-evidence/red-results.json.gz), `dyd-805-evidence/replay-red.sh` |
| Final discovery | 0 | 2,515 tests, including 257 PR-ready tests | [Discovery](dyd-805-evidence/discovery.log.gz) |
| Focused regression after restoring the candidate | 0 | 5 passed, zero skipped | [Focused log](dyd-805-evidence/focused.log.gz), [TRX](dyd-805-evidence/focused.trx.gz) |
| Entire PR-ready class | 0 | 257 passed, zero skipped | [Class log](dyd-805-evidence/class.log.gz), [TRX](dyd-805-evidence/class.trx.gz) |
| Exact Light gate | 0 | 2,505 passed; ten Node guard tests skipped because pwsh was absent from PATH | [Light log](dyd-805-evidence/light.log.gz), [TRX](dyd-805-evidence/light.trx.gz) |
| Node guard and workflow follow-up with existing pwsh | 0 | 63 passed, zero skipped; includes all ten missing Light cases | [Workflow log](dyd-805-evidence/workflow.log.gz), [TRX](dyd-805-evidence/workflow.trx.gz) |
| First static attempt | 2 | INVALID: the writer edited this changelog during C# collection; all other stacks passed | [Log](dyd-805-evidence/static-invalid.log.gz), [aggregate](dyd-805-evidence/static-invalid-result.json.gz), `dyd-805-evidence/static-input-change.json` |

`dyd-805-evidence/test-receipt.json` records counters and implementation hashes.
The ten skips were confined to unchanged `NodeTestGuardTests`; the follow-up closes their local
proof gap without changing or weakening their assertions. Two discarded draft fixtures also
failed while developing the correction: an unknown tool name bypassed hook command handling,
and a reviewed brace-named file was incorrectly expected to pass. The final fixture uses the
actual Unknown CLI and preserves the literal allowlist; the final class and Light runs cover both.

The first static attempt is not counted as a pass. The fresh static run exited 0 with all five
stacks passing and repository inputs frozen throughout collection. Retained
[static log](dyd-805-evidence/static.log.gz) and
[aggregate receipt](dyd-805-evidence/static-result.json.gz) are from that completed run.
The final documentation check is retained as `dyd-805-evidence/docs.log.gz`; rerun it with
`gate4 180 dotnet bin/Debug/net10.0/dydo.dll check`.

Native Windows on the reviewed candidate remains required through `guard-windows`; this writer
neither launched nor claimed it. Local product coverage is not required for this test/CI-only
scope; hosted whole-stack coverage remains required. Mutation is unavailable under DYD-103 and
is not claimed passing. The captain owns reviews, hosted proof, and merge validation.

## Pre-review feature integration

This local, unsquashed synchronization joins implementation
`7175d7128d62ed4a16fec9b2cd81115c34a3ee5f` with fetched feature tip
`4dfa8bb79a832d776e91539aae785fca3ed30c93`, already on `origin/feature/dydo-3-6-0`.
The sole textual conflict was `.github/workflows/ci.yml`: the resolution keeps the whole
`GuardPrReadyTests` class and file-symlink setup alongside DYD-806's exact
`GuardPrReadyBodyFileTests.LinkedDirectoryLeavingTheTree_IsNull` selection. The feature's
class/method discovery and all-results-Passed enforcement remain intact. All other feature paths
merge unchanged. This is prerequisite integration before the first code review, without a source
PASS or a remote merge.

The feature's `guard-windows-selection.test.cjs` hard-codes six classes in its filter assertion
and synthetic passing TRX. The required seventh class therefore conflicts with that fixture.
The Node test is outside this hop's authorized edit scope and is preserved unchanged. Its two
failures are an integration blocker for the captain: add `GuardPrReadyTests` to that fixture's
expected class list and passing TRX rows in a separately authorized follow-up. The original
diagnosis red remains applicable because neither side changes production guard/parser semantics.

The combined gates ran on clean merge commit `6ac09458abd0b56290237b2648469f294a18a7ad`.
The final merge hop amends only this record and retained evidence after those gates. Key test,
CI and facade SHA-256 hashes are in `dyd-805-evidence/merge-receipt.json`; the complete tested
Git tree in `dyd-805-evidence/merge-tree.txt.gz` pins all source inputs. The implementation test file retains
the SHA-256 above; merged CI SHA-256 is
`014997903f358b5031dafa913bfcec2fa4bf7aa56dfd190466744b31f7519aeb`.

| Combined observation | Exit | Result | Raw evidence |
| --- | --- | --- | --- |
| Prerequisites | 0 | SDK 10.0.303, runtime 10.0.14, pwsh 7.6.9, Python 3.14.4, all three installed pnpm locks match; skill links current | `dyd-805-evidence/merge-preflight-r2.log.gz` |
| Selection discovery | 0 | Nonzero selection, including 257 PR-ready and 30 body-file cases | `dyd-805-evidence/merge-discovery.log.gz` |
| Both full classes | 0 | 287 passed, zero skipped | `dyd-805-evidence/merge-classes.log.gz`, `merge-classes.trx.gz` |
| Exact affected Light | 0 | 2,515 passed, zero skipped; pwsh present throughout | `dyd-805-evidence/merge-light.log.gz`, `merge-light.trx.gz` |
| Feature workflow selection test | 1 | 10 passed, 2 failed, zero skipped; expected class list and synthetic passing TRX omit the seventh class | `dyd-805-evidence/merge-node.log.gz` |
| All-stack static | 0 | dotnet, python, node, viewer and statechart passed; inputs frozen throughout | `dyd-805-evidence/merge-static.log.gz`, `merge-static-result.json.gz`, per-stack static receipts |

The first prerequisite attempt exited 127 because this hop's explicit PATH omitted the provisioned
Node installation; `merge-preflight.log.gz` retains it. The corrected check and every subsequent
gate include `/home/balazs/.local/share/fnm/aliases/default/bin` and `/tmp/dyd622-pwsh` on PATH.
No dependencies were installed. Every run used the required host lock, bounded systemd scope in
`app-t3code-agents.slice`, swap off, OOMPolicy continue and disabled .NET reuse. Admission waited
for the shared slots. No process environments were read. No input changed during static collection.

Replay from this worktree's root with
`bash dydo/project/changelog/2026/2026-11-06/dyd-805-evidence/merge-replay.sh`; the retained script
contains the exact bounded commands, class and Light filters, output paths and per-command exits.
The Node failure is intentionally retained, not a passing claim. The prerequisite script is
`dyd-805-evidence/merge-preflight.sh`; run it through the same wrapper with a 4G cap and 120-second
timeout. Final documentation validation uses that wrapper with a 4G cap and 180-second timeout
around `dotnet bin/Debug/net10.0/dydo.dll check`, retained as `merge-docs.log.gz`.

Native hosted Windows remains captain-owned and unproved by these Linux results. This hop made
no production edits, push, PR or remote merge. Local product coverage remains inapplicable and
mutation remains unavailable as recorded above.

## Authorized Node fixture correction

The follow-up on `530088c4007543ce545299a8005e3508a6072aaf` closes the integration
blocker above. The independent expected class list now includes `GuardPrReadyTests`, matching
the intentionally expanded CI selection; the title names seven guard classes. All original
fault checks remain, with two additional cases rejecting missing or skipped PR-ready class results.
The workflow, C# tests and production code retain their exact bytes.

Fresh Node red/green, applicable Node static and documentation receipts are recorded in
`dyd-805-evidence/node-fixture-receipt.json`, with compressed raw logs beside it. Historical
failures above retain their original verdicts. The correction reuses the existing 2,515-passing
Light and 287-passing class evidence from `merge-receipt.json` under the captain's explicit
Node-only authorization. A diff against its tested commit `6ac09458abd0b56290237b2648469f294a18a7ad`
must contain only this Node fixture and ticket evidence; the retained preflight records that check.
The previous whole-stack static result remains historical; this hop runs the applicable Node gate.
Native hosted Windows proof remains captain-owned. No C# tests are rerun for this fixture correction.

| Fixture observation | Exit | Result | Raw evidence |
| --- | --- | --- | --- |
| Initial red and post-green revert | 1 each | 10 passed, 2 failed, zero skipped in each run | `node-fixture-red.log.gz`, `node-fixture-reverted.log.gz` |
| Corrected fixture | 0 | 14 passed, zero failed or skipped | `node-fixture-green.log.gz` |
| Node static | 0 | Node passed; child exit 0, aggregate 0 | `node-fixture-static.log.gz`, `node-fixture-static-result.json.gz` |

These artifact names are relative to `dyd-805-evidence/`. Corrected fixture SHA-256:
`7cf5e132a17a0bf5947f1cc355549c0cc826c2728a0692b2652c2ca2d60cc27b`.

From the repository root, use this wrapper for the fresh checks (logs use the
`/tmp/dyd805-node-fixture-` prefix):

```bash
node_fixture_gate() {
  /var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout "$@"
}
export PATH=/tmp/dyd622-pwsh:/home/balazs/.local/share/fnm/aliases/default/bin:/home/balazs/.dotnet:/usr/local/bin:/usr/bin:/bin
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
node_fixture_gate 180 node --test DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs
node_fixture_gate 600 .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static --stack node
node_fixture_gate 180 dotnet bin/Debug/net10.0/dydo.dll check
```

The red runs use the fixture bytes at `530088c4007543ce545299a8005e3508a6072aaf` with the
same Node command. The second red temporarily restores those bytes after green, then restores
the corrected file through an EXIT trap in `dyd-805-evidence/node-fixture-revert.sh`.
Run that script with the same wrapper and a 180-second timeout; it deliberately exits 1.
The preflight compares the restored file to its saved
green copy. No dependencies are installed or process environments inspected.

## Related

- [Guard system](../../../../understand/guard-system.md) - unreadable PR calls and body-file rules
- [Testing strategy](../../../../guides/testing-strategy.md) - gates and evidence
- [DYD-681 Windows CI](../2026-11-05/dyd-681-windows-guard-ci.md) - all-results-Passed enforcement
- [DYD-805](https://linear.app/bodnar-balazs/issue/DYD-805) - owning ticket
