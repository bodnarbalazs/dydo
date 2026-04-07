---
area: general
name: fix-commands-doc-consistency
status: human-reviewed
created: 2026-04-07T20:27:02.0213535Z
assigned: Henry
updated: 2026-04-07T21:15:15.8802991Z
---

# Task: fix-commands-doc-consistency

Fixed all 3 issues from the brief plus pre-existing template sync failures. (a) Updated Templates/dydo-commands.template.md with all 5 inquisition coverage flags and synced about-dynadocs template with reference doc. (b) BuildRootCommand now derives commands from Program.cs via regex+reflection — parses XxxCommand.Create() calls, so it can never drift. Added infrastructure commands (worktree, watchdog, queue) to ExcludedPaths since they lack docs. (c) Fixed ExtractFlags regex to handle backtick-enclosed --flag <value> patterns. Added ExtractFlags_HandlesFlagValuePatterns test. Also fixed AboutQuickReference test to check command aliases (MessageCommand 'msg' alias). All 3483 tests pass, gap_check 135/135 green. Note: worktree/queue/watchdog commands remain undocumented in the reference doc — out of scope for this task.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed all 3 issues from the brief plus pre-existing template sync failures. (a) Updated Templates/dydo-commands.template.md with all 5 inquisition coverage flags and synced about-dynadocs template with reference doc. (b) BuildRootCommand now derives commands from Program.cs via regex+reflection — parses XxxCommand.Create() calls, so it can never drift. Added infrastructure commands (worktree, watchdog, queue) to ExcludedPaths since they lack docs. (c) Fixed ExtractFlags regex to handle backtick-enclosed --flag <value> patterns. Added ExtractFlags_HandlesFlagValuePatterns test. Also fixed AboutQuickReference test to check command aliases (MessageCommand 'msg' alias). All 3483 tests pass, gap_check 135/135 green. Note: worktree/queue/watchdog commands remain undocumented in the reference doc — out of scope for this task.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-07 21:25
- Result: PASSED
- Notes: LGTM. All 3 issues resolved cleanly: (a) template flags complete and in sync, (b) BuildRootCommand reflection approach is sound with proper null/type guards, (c) ExtractFlags regex correctly handles flag-value patterns. Alias check fix is correct. 3483 tests pass, gap_check 135/135 green.

Awaiting human approval.