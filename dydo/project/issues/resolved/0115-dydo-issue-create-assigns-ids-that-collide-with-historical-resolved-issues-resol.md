---
id: 115
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-04-26
resolved-date: 2026-04-26
---

# dydo issue create assigns IDs that collide with historical resolved issues; resolve subcommand then refuses with "already resolved"

## Description

`dydo issue create`'s ID-assignment logic appears to scan only `dydo/project/issues/*.md` (open issues) when picking the next free ID. It does not check `dydo/project/issues/resolved/*.md`. When the open directory has been cleared past a resolved-only ID, the next-free pointer skips back to a number already used by a resolved issue.

The result: two issues with the same numeric `id:` frontmatter exist on disk — one in `resolved/`, one in the open directory. `dydo issue resolve <id>` then matches the resolved file first, prints `Issue #<id> is already resolved`, and refuses to operate on the open issue. The open issue is uncloseable via the normal flow without manually editing the frontmatter.

Observed in this project today (2026-04-26):

- `dydo/project/issues/resolved/0003-reviewer-role-lacks-docs-review-guidance-prompt-engineering-debt.md` (`id: 3`, resolved 2026-04-07).
- `dydo/project/issues/0003-dydo-issue-create-lacks-a-body-flag-producing-empty-issue-files.md` (`id: 3`, opened 2026-04-20).

When orchestrator Brian ran `dydo issue resolve 3 --summary "..."` after the `--body`/`--body-file` work shipped (commit `7811ad4`), the command refused with `Issue #3 is already resolved`. Workaround used: edit the open issue's `id:` frontmatter to `114` (next free), `git mv` to the corresponding filename, then `dydo issue resolve 114`. The renumber metadata is captured inline in `resolved/0114-...md` for traceability.

This was not the first collision in the project's history — issue `dydo/project/issues/_index.md` shows several gaps in the open ID sequence, suggesting other resolved-vs-open collisions may already be on disk uncaught.

## Reproduction

1. Have at least one resolved issue at `dydo/project/issues/resolved/NNNN-*.md`.
2. From a state where the next-free ID computed from `dydo/project/issues/*.md` only equals `NNNN`, run `dydo issue create --title "..." --area ... --severity ... --found-by ...`. The new issue is created with `id: NNNN`.
3. Run `dydo issue resolve NNNN --summary "..."` on the new issue.
4. Observe: `Issue #NNNN is already resolved` — the resolve command found the resolved-directory file first.

## Likely root cause

`Commands/IssueCreateHandler.cs` (or the underlying ID-allocation helper it calls) likely enumerates `dydo/project/issues/*.md` and computes `max(id) + 1` without descending into `resolved/`. The fix is to enumerate both directories before computing next-free.

A complementary fix in `Commands/IssueCommand.cs` `resolve` handler: when looking up an ID, prefer the file in the open directory (which is what the user is asking to resolve); if both exist, surface a clear error rather than silently picking the resolved one. (Or refuse the duplicate at create time, which is the better fix.)

## Suggested fix

1. **In `IssueCreateHandler.cs`:** the next-free-ID scan should walk `dydo/project/issues/**/*.md` (recursive) rather than the open directory only. Trivial change.
2. **In `IssueCommand.cs` resolve handler:** when an ID matches multiple files (open + resolved), prefer the open one and surface a warning. Or refuse with an explicit "ID NNNN is ambiguous; this should not happen — file an issue" message.
3. **Optional one-time scan utility:** `dydo issue verify` (or similar) that walks both directories and reports any duplicate `id:` frontmatter, so existing collisions can be detected and renumbered. Could be folded into `dydo check` if that command exists.

Add a regression test that creates an issue, resolves it, then creates another — assert the second issue's ID is strictly greater than the first's.

## Impact

- Low frequency but non-zero: hits any time the open-directory ID sequence has gaps and a resolved issue lives in those gaps. The `_index.md` evidence suggests this has happened multiple times in this project's history; we just hadn't tried to resolve those duplicates before.
- Confusing failure mode: the error message "already resolved" reads as if the user already closed the issue, when they actually have an entirely different issue with the same numeric ID. Easy to lose minutes here.
- Workaround is mechanical but requires guard-lift to edit the frontmatter — agents without lift cannot self-recover.
- May be load-bearing for any future tool that relies on `id:` uniqueness (issue indices, audit cross-refs, dispatch task names that reference issue IDs).

## Related context

- `Commands/IssueCreateHandler.cs` — ID-assignment logic.
- `Commands/IssueCommand.cs` — resolve subcommand handler.
- `dydo/project/issues/resolved/0003-reviewer-role-lacks-docs-review-guidance-prompt-engineering-debt.md` — historical `id: 3`, resolved 2026-04-07.
- `dydo/project/issues/resolved/0114-dydo-issue-create-lacks-a-body-flag-producing-empty-issue-files.md` — the renumbered example, with `renumbered-from: 3` frontmatter capturing the cause.
- `dydo/project/issues/_index.md` — gaps in the open ID sequence indicate other potentially-affected IDs.

## Resolution

Fixed in commit 3654ec6 (Frank). dydo issue create now scans dydo/project/issues/**/*.md recursively when computing next-free ID. dydo issue resolve uses open-dir-wins logic with a warning when both open and resolved files share an ID; 'already resolved' only fires when the open file is genuinely absent. Integration test asserts exact behavior. Reviewed by Charlie.
