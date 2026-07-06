---
title: Guard daily-validation uses raw cwd + hardcoded "dydo", creating a stray nested dydo tree
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 213
type: issue
found-by: review
date: 2026-07-06
---

# Guard daily-validation uses raw cwd + hardcoded "dydo", creating a stray nested dydo tree

Surfaced while reviewing the Docs → Notion mirror sprint ([[033-docs-notion-nested-page-mirror]]):
the working tree accumulated a stray `dydo/project/dydo/_system/.local/last-validation` directory.
It is **not** caused by the docs-mirror code or its tests (those use isolated temp roots) — it traces
to the guard's daily-validation path construction.

## Description

**Mechanism.** `Commands/GuardCommand.cs` `RunDailyValidationIfDue` (~lines 1619-1648) builds its
`last-validation` timestamp path from raw `Environment.CurrentDirectory` joined with a hardcoded
`"dydo"` segment, rather than resolving the actual project root (the way the rest of the CLI locates
the dydo root). When `dydo guard` fires via the `PreToolUse` hook while the shell's cwd happens to be
`<repo>/dydo/project` (e.g. a tool `cd`'d there to inspect decision docs), it writes to
`<repo>/dydo/project/dydo/_system/.local/last-validation` — a doubly-nested phantom `dydo/` tree.

**Impact.** Cosmetic tree pollution (an untracked stray dir that reads like test/sprint contamination
and wastes reviewer time attributing it), plus the daily validation is keyed off the wrong root, so
its "due" bookkeeping can be computed against a path that isn't the real project state. Low severity;
no data loss.

**Suggested fix.** Resolve the real dydo root (same resolver the other commands use) instead of
`Environment.CurrentDirectory + "dydo"`; guard against writing the marker when the resolved root
doesn't match a real project. Add a regression test that runs the guard with cwd set to a subdir and
asserts no nested `dydo/` tree is created.

**Cleanup.** Delete the existing stray `dydo/project/dydo/` directory (untracked, gitignored content).

## Reproduction
1. From the repo root, `cd dydo/project`.
2. Trigger any tool call that invokes `dydo guard` via the PreToolUse hook (or run `dydo guard` there).
3. Observe `dydo/project/dydo/_system/.local/last-validation` created.

## Resolution
(Filled when resolved)
