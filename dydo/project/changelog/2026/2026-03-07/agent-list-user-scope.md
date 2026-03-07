---
area: general
type: changelog
date: 2026-03-07
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

C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Modified


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

## Approval

- Approved: 2026-03-07 22:42
