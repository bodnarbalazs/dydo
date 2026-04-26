---
id: 114
area: platform
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-26
renumbered-from: 3
renumbered-reason: ID collision with resolved issue dydo/project/issues/resolved/0003-reviewer-role-lacks-docs-review-guidance-prompt-engineering-debt.md (resolved 2026-04-07). The new --body-flag issue was created with id: 3 by dydo issue create on 2026-04-20 — the issuer did not detect the collision, and dydo issue resolve later refused with "already resolved" because it found the resolved/0003-* file first. Renumbered to 114 (next free) on 2026-04-26 by orchestrator Brian to unblock the resolve flow. The collision-detection bug itself is filed separately.
---

# dydo issue create lacks a --body flag, producing empty issue files

## Description

`dydo issue create` accepted only `--title`, `--area`, `--severity`, `--found-by` and wrote a hardcoded placeholder body (`(Describe the issue)` / `(Steps to reproduce, if applicable)` / `(Filled when resolved)`). Every issue Brian filed during the 2026-04-26 housekeeping session (#0108 through #0113) had to be created with placeholders and then patched up via guard-lift edits to the issue file. The lack of a body flag also forced dispatched agents (Emma on #0113) to leave their issues stub-only because the orchestrator's role couldn't write to `dydo/project/issues/` without a lift.

## Reproduction

1. Run `dydo issue create --title "..." --area backend --severity medium --found-by manual`.
2. Inspect the resulting `dydo/project/issues/NNNN-...md`.
3. Observe: Description, Reproduction, and Resolution sections all contain only the literal placeholder strings — no way to populate from the command line, even though the body is the most important part of an issue.

## Resolution

Resolved in commit `7811ad4` (Dexter, code-writer): added `--body <text>` and `--body-file <path>` options to `dydo issue create`. Mutual exclusion (both flags supplied) and missing-file are guarded with clear errors. Whitespace-only body content is trimmed to null (falls back to placeholder). `BuildBodySection` uses regex `^## (Reproduction|Resolution)\b` with Multiline to drop only line-anchored placeholders, so user-provided structure (a body that itself contains `## Reproduction` or `## Resolution`) is preserved cleanly. 6 integration tests in `DynaDocs.Tests/Integration/IssueTests.cs`. Reference docs mirrored in commit `70e242f` (Charlie, docs-writer). Reviewed and PASSED by Frank in commit-chain context (Frank's note flagged a minor non-blocking gap: no explicit test for whitespace-only body trimming — behavior is guarded but uncovered).