---
area: general
name: implement-clean-rename
status: human-reviewed
created: 2026-03-30T20:06:55.6052744Z
assigned: Charlie
updated: 2026-03-31T14:22:19.6046469Z
---

# Task: implement-clean-rename

Implemented decision 014: renamed dydo clean to dydo agent clean. Moved command registration from root to AgentCommand in Program.cs. Updated CompletionProvider (refactored to data-driven tables, reducing CC from 38 to ~19). Updated help text, templates, tests, and generated reference docs. All 3364 tests pass (1 flaky watchdog test unrelated). Coverage gate passes (132/132 modules). No alias or deprecation per decision.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented decision 014: renamed dydo clean to dydo agent clean. Moved command registration from root to AgentCommand in Program.cs. Updated CompletionProvider (refactored to data-driven tables, reducing CC from 38 to ~19). Updated help text, templates, tests, and generated reference docs. All 3364 tests pass (1 flaky watchdog test unrelated). Coverage gate passes (132/132 modules). No alias or deprecation per decision.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-31 14:31
- Result: PASSED
- Notes: LGTM. Command correctly moved from root to agent subcommand group. CompletionProvider refactor to data-driven tables is clean and reduces CC from ~38 to ~19. Tests comprehensive — 8+ new edge-case tests added. No dead code, no backwards-compat hacks, exactly per decision 014. All 3364 tests pass, coverage gate 132/132.

Awaiting human approval.