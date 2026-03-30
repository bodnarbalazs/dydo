---
area: general
name: fix-guard-inquisition-batch1
status: human-reviewed
created: 2026-03-30T17:45:15.8457091Z
assigned: Frank
updated: 2026-03-30T18:04:19.7820361Z
---

# Task: fix-guard-inquisition-batch1

Fix guard-system.md line 84: it claims bash operations are checked just like direct tool calls. After issue #0003 fix, bash read operations now do go through staged access control (IsReadAllowed) via CheckBashFileOperation. Update the doc to accurately describe that bash commands are split and each file operation is checked for off-limits, staged access (reads), and RBAC (writes). The phrasing 'just like direct tool calls' is now accurate for staged access, but the mechanism is different (per-operation checking after command analysis vs direct path checking).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review 3 guard enforcement fixes. #0003: CheckBashFileOperation now calls IsReadAllowed for Read ops — bash read commands enforce the same staged access as direct Read tool calls. #0005: CheckCommandSeparator now splits on single pipe — piped commands analyzed independently, eliminating false Read ops from downstream segments. #0009: Removed sc alias from WriteCommands (conflicts with Windows sc.exe). 9 new tests (3 in WorktreeCompatTests for staged access, 6 in BashCommandAnalyzerTests for pipes and sc). All 3328 tests pass, coverage gate green.

## Code Review

- Reviewed by: Jack
- Date: 2026-03-30 19:08
- Result: PASSED
- Notes: LGTM. All 3 fixes correct: staged read access in CheckBashFileOperation, pipe splitting in CheckCommandSeparator, sc alias removal. Regex refactoring to GeneratedRegex is clean. 10 new tests, coverage gate green.

Awaiting human approval.