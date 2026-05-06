---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-human-only-clean-force

Review commit 3ad12ba: gate `dydo agent clean --force` as human-only via HumanOnlyDydoCommandRegex.

Files (2): Commands/GuardCommand.cs (regex + 2 comments), DynaDocs.Tests/Integration/GuardIntegrationTests.cs (4 InlineData rows).

Plan: dydo/agents/Charlie/archive/20260505-173935/plan-human-only-clean-force.md.

Key behaviors verified by the new test rows AND by 5 manual guard invocations:
- claimed + `dydo agent clean Charlie --force` -> BLOCKED exit 2
- claimed + `dydo agent clean Charlie` (bare) -> exit 0 (negative invariant pinned)
- claimed + `dydo agent clean --all --force` -> BLOCKED exit 2
- claimed + `dydo agent clean Charlie ; rm --force foo` chain test -> exit 0 (chain-segment isolation via `[^;&|]*`; verified manually with `&&` separator)
- non-claimed session + `dydo agent clean Charlie --force` -> exit 0

Verification gate: dotnet build clean; gap_check.py exit 0 (Failed: 0, Passed: 4086, 139/139 modules pass tier).

DEVIATION FROM PLAN (flagged to Adele in msg): plan included a 2-line doc edit at dydo/reference/dydo-commands.md:182-186, but code-writer role has dydo/** read-only. Doc piece needs a docs-writer dispatch on the same task — separate commit, same branch — Adele was messaged for the call between (A) defer doc to a follow-up commit by docs-writer, (B) Adele dispatches docs-writer to land doc before this PR opens. I went with (A) and committed code+tests now to unblock review.

Single PR. No bundled unrelated changes — only the two files above are staged in 3ad12ba; other 'M' entries in git status are pre-existing in-progress work from a different effort.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 3ad12ba: gate `dydo agent clean --force` as human-only via HumanOnlyDydoCommandRegex.

Files (2): Commands/GuardCommand.cs (regex + 2 comments), DynaDocs.Tests/Integration/GuardIntegrationTests.cs (4 InlineData rows).

Plan: dydo/agents/Charlie/archive/20260505-173935/plan-human-only-clean-force.md.

Key behaviors verified by the new test rows AND by 5 manual guard invocations:
- claimed + `dydo agent clean Charlie --force` -> BLOCKED exit 2
- claimed + `dydo agent clean Charlie` (bare) -> exit 0 (negative invariant pinned)
- claimed + `dydo agent clean --all --force` -> BLOCKED exit 2
- claimed + `dydo agent clean Charlie ; rm --force foo` chain test -> exit 0 (chain-segment isolation via `[^;&|]*`; verified manually with `&&` separator)
- non-claimed session + `dydo agent clean Charlie --force` -> exit 0

Verification gate: dotnet build clean; gap_check.py exit 0 (Failed: 0, Passed: 4086, 139/139 modules pass tier).

DEVIATION FROM PLAN (flagged to Adele in msg): plan included a 2-line doc edit at dydo/reference/dydo-commands.md:182-186, but code-writer role has dydo/** read-only. Doc piece needs a docs-writer dispatch on the same task — separate commit, same branch — Adele was messaged for the call between (A) defer doc to a follow-up commit by docs-writer, (B) Adele dispatches docs-writer to land doc before this PR opens. I went with (A) and committed code+tests now to unblock review.

Single PR. No bundled unrelated changes — only the two files above are staged in 3ad12ba; other 'M' entries in git status are pre-existing in-progress work from a different effort.

## Approval

- Approved: 2026-05-06 17:47
