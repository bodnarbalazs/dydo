---
area: general
name: orchestrator-worktree-guidance
status: human-reviewed
created: 2026-03-16T17:25:08.4035706Z
assigned: Emma
updated: 2026-03-16T17:33:12.5131214Z
---

# Task: orchestrator-worktree-guidance

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Updated Templates/mode-orchestrator.template.md with worktree guidance per decision 011. Four changes: (1) Worktree note in Slice section, (2) --worktree in Dispatch example, (3) When to Use --worktree subsection after Dispatch, (4) Merge Coordination subsection after Monitor. Note: could not edit dydo/_system/templates/ copy due to code-writer role permissions — that needs separate handling.

## Code Review (2026-03-16 17:29)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Default dispatch example (line 82) includes --worktree, contradicting the When to Use subsection below which says not to use it for sequential/single/non-code dispatches. Fix dispatched to code-writer.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-16 17:36
- Result: PASSED
- Notes: LGTM. Previous issue fixed — default dispatch example no longer includes --worktree. Worktree guidance properly separated into dedicated subsection with correct do/don't criteria per decision 011. Merge coordination section accurate. All 2621 tests pass. Note: dydo/_system/templates/ override copy is stale and needs separate sync.

Awaiting human approval.