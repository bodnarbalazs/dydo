---
area: general
name: agent-list-user-scope
status: human-reviewed
created: 2026-03-07T19:41:13.5514529Z
assigned: Iris
updated: 2026-03-07T21:29:03.4502253Z
---

# Task: agent-list-user-scope

(No description)

## Progress

- [x] Code changes (implemented by previous agents, code-reviewed and human-approved)
- [x] Program.cs help text (already updated — verified correct)
- [x] Templates/dydo-commands.template.md (already updated — verified correct)
- [x] dydo/reference/dydo-commands.md — updated `agent list` section
- [x] dydo/reference/about-dynadocs.md — updated command reference table

## Files Changed

- `dydo/reference/dydo-commands.md` — Updated `agent list` docs to reflect user-scoped default, `--all` flag
- `dydo/reference/about-dynadocs.md` — Updated agent list entry in command reference table

## Review Summary

Implemented --all flag for dydo agent list. Default now shows only current human's agents with Task column; --all restores old behavior showing all agents with Human column. Error if no human set without --all. Added 4 new integration tests. Could not edit Program.cs and Templates/ (outside code-writer writable paths) — help text there still needs updating.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-07 20:48
- Result: PASSED
- Notes: LGTM. Feature logic correct, tests comprehensive (4 list + 3 auto-close), no security issues. freeCount2 naming is cosmetic. Program.cs/Templates help text update remains outstanding (was outside code-writer writable paths).

Awaiting human approval.

## Code Review

- Reviewed by: Leo
- Date: 2026-03-07 21:31
- Result: PASSED
- Notes: LGTM. Single-line fix in HelpCommandTests.cs:55 correctly syncs test help text with Program.cs:56. All 9 tests pass. Clean, minimal change.

Awaiting human approval.