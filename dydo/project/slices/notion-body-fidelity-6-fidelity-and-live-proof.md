---
title: Fidelity Corpus and Live Proof
sprint: notion-body-fidelity
seq: 6
status: ready
area: backend
type: context
---

# Slice 6 — Fidelity Corpus and Live Proof

Prove formatting preservation, semantic edit sensitivity, and the exact watchdog sequence through
deterministic fixtures, full gates, and isolated real-Notion execution.

## Spec fragment

Prove the composed implementation offline and against isolated real Notion, then reconcile the public
reference and issue record to observed behavior. This Slice cannot pass on skipped live tests.

## Implementation detail

Add exact test class `NotionBodyFidelityMutationTests`, consuming Slice 5's named fixture. Generate bounded combinations of
headings/H1 omission, blank gaps, escapes, emphasis/links, nested lists, tables, quotes, code, repeated
sections, insert/delete/modify/reorder, and disjoint/overlap edits. Assert exact untouched bytes and that
word, punctuation, structure, link target, checkbox, and code mutations always register. Retain the
sanitized slice-11 fixture as a named regression, not just generated input.

Add exact class `NotionSpineBodyFidelityLiveTests` with three `notion-live` facts using
`NotionLiveTestBase.ChildPageId` as the unique scratch parent. Provision a
scratch spine database/page, materialize an existing tracked file and v2 base, push a local slice-11 edit,
read the real echo, run the actual delta/watchdog tick, and assert `None` plus complete file-byte identity.
Then edit the scratch page through `UpdatePageMarkdown`, run the delta tick, assert a single surgical
import with exact untouched body/frontmatter bytes, and assert the following tick is `None`. Add the
Notion-originated create control. Archive the scratch child in existing teardown; never reset or address
the configured production board.

Before running live, assert both `DYDO_NOTION_TEST_TOKEN` and `DYDO_NOTION_TEST_PARENT` exist without
printing values. Capture the executed/non-skipped test count. Update `dydo/reference/notion-sync.md` with
the dual-projection contract, migration, diagnostics, and dated live evidence. Resolve issue 0309 only
after every acceptance assertion and gate passes; otherwise leave it open with the exact blocker.

## Out of scope for this slice

Production-board mutation, release publication, and unrelated docs-mirror activation.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~NotionBodyFidelityMutationTests"
if (($listed | Select-String 'NotionBodyFidelityMutationTests').Count -lt 20) { throw 'Fidelity gate matched fewer than 20 offline tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~NotionBodyFidelity"
if (-not (Test-Path Env:DYDO_NOTION_TEST_TOKEN) -or -not (Test-Path Env:DYDO_NOTION_TEST_PARENT)) { throw 'Live Notion test credentials are required; a skipped test cannot pass this Slice.' }
$liveResults = Join-Path ([IO.Path]::GetTempPath()) ("dydo-notion-fidelity-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $liveResults | Out-Null
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "Category=notion-live&FullyQualifiedName~NotionSpineBodyFidelityLiveTests" --results-directory $liveResults --logger "trx;LogFileName=live.trx"
if ($LASTEXITCODE -ne 0) { throw 'Live Notion fidelity tests failed.' }
[xml]$trx = Get-Content (Join-Path $liveResults 'live.trx')
$counters = $trx.TestRun.ResultSummary.Counters
if ([int]$counters.total -ne 3 -or [int]$counters.executed -ne 3 -or [int]$counters.passed -ne 3 -or [int]$counters.notExecuted -ne 0) { throw "Live gate did not execute exactly 3 passing tests: $($counters.OuterXml)" }
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dydo check
```
