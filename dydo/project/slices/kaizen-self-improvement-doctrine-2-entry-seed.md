---
title: Plant the Kaizen Entry Seed
sprint: kaizen-self-improvement-doctrine
seq: 2
status: ready
area: general
type: context
---

# Slice 2 — Plant the Kaizen Entry Seed

Point both runtime entry surfaces to the detailed skill with only two sentences of prompt weight.

## Spec fragment

Plant exactly two runtime-neutral sentences in the canonical entry template and both of this
repository's current entry surfaces. The seed names `self-improvement`; all details remain in the
skill from slice 1.

Accepted means the exact seed occurs once in each owned entry file, a fresh all-integrations init
emits identical `CLAUDE.md` and `AGENTS.md` content containing it, current entry content outside
the append is preserved, and every gate passes.

## Implementation detail

Touch only:

- `Templates/entry-point.template.md`
- `CLAUDE.md`
- `AGENTS.md`
- `DynaDocs.Tests/Integration/InitCommandTests.cs`

Append this exact paragraph, with no heading or list, to `Templates/entry-point.template.md` after
the existing paragraph ending `invoke it and follow it.`:

> Practice kaizen: when a failure, correction, or workaround recurs, treat the pattern as evidence
> that the harness may need one small, durable improvement. Invoke the `self-improvement` skill to
> choose and route the smallest justified change without expanding the current task or silently
> changing policy.

These are exactly two sentences. Keep the backticks around `self-improvement`; do not add the
compound expression, examples, platform names, routing list, or any other prose to the entry
template.

Append the identical paragraph once to the EOF of current `CLAUDE.md` and current `AGENTS.md`.
Preserve all preceding bytes semantically: do not regenerate either file from the template, do not
remove the memory-routing paragraph present only in `CLAUDE.md`, and do not add that paragraph to
`AGENTS.md`.

Extend `InitCommandTests.Init_All_WiresBothIntegrations` after the existing file assertions:

- Read the generated `CLAUDE.md` and `AGENTS.md`.
- Assert they are exactly equal; this pins the one-template/two-runtime contract.
- Store the exact two-sentence paragraph in one local expected string and assert it occurs in the
  generated content.
- Assert `self-improvement` occurs exactly once in each generated entry file. Use a direct ordinal
  occurrence count or equivalent non-regex assertion; do not introduce a helper for this one test.

Do not change `TemplateGenerator` or `InitCommand`; the current implementation already generates
both filenames from `GenerateEntryPointMd`.

## Out of scope for this slice

- The self-improvement template, generated skill outputs, and sync/discovery tests — slice 1 owns
  them.
- Reconciling current `CLAUDE.md`/`AGENTS.md` with the rest of the canonical entry template.
- Memory policy changes, hooks, nudges, docs, records other than this slice, and release/version
  work.

## Gate

Run in order and require every command/assertion to pass:

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~InitCommandTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dotnet run --project DynaDocs.csproj -- check
$slicePaths = @('Templates/entry-point.template.md', 'CLAUDE.md', 'AGENTS.md', 'DynaDocs.Tests/Integration/InitCommandTests.cs')
git add -- $slicePaths
if ($LASTEXITCODE -ne 0) { throw "exact staging failed: $LASTEXITCODE" }
$actual = @(git diff --cached --name-only)
$difference = @(Compare-Object ($slicePaths | Sort-Object) ($actual | Sort-Object))
if ($difference.Count -ne 0) { throw "staged paths differ from slice allowlist: $($difference | Out-String)" }
git diff --cached --check -- $slicePaths
if ($LASTEXITCODE -ne 0) { throw "cached diff check failed: $LASTEXITCODE" }
```

All commands run in the dedicated lane worktree. After the lane merge, the orchestrator reruns the
full runner, forced gap check, and source-built `dydo check`, then performs the exact shared-root
16-path manifest comparison specified by slice 1 and the sprint root.
