---
area: general
type: changelog
date: 2026-03-19
---

# Task: baton-passing-implementation

Implement decision 010: baton-passing reply-pending clearance and review enforcement for dispatched code-writers.

## Progress

- [x] Design (co-thinker) — brief written
- [x] Graduated to orchestrator
- [x] Slice A: Code changes (Charlie — code-writer) — complete, 12 new tests, all 2602+ pass
- [x] Slice B: Template + docs fixes (Dexter — docs-writer) — complete (guardrails H15/H25, role docs, --no-wait)
- [ ] Review: Code changes (Emma — reviewer) — dispatched

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-docs-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-planner.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-co-thinker.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\agent-workflow.template.md — Modified


## Review Summary

# Review: Baton-Passing + Review Enforcement (Decision 010)

Review Charlie's code changes for the baton-passing and review enforcement implementation.

## Scope — IMPORTANT

Multiple agents are working concurrently in the same repo. Only review changes related to decision 010. The following files contain Charlie's changes:

### In-scope (review these):
- **Services/DispatchService.cs** — Baton-passing logic (inheritReply flag, reply-pending clearance on same-task dispatch, reply_required inheritance) AND review enforcement marker creation (code-writer dispatching reviewer creates .review-dispatched marker). NOTE: This file also has worktree-related changes (cleanupWorktreeId, mainProjectRoot, .worktree-hold) from Frank's worktree-implementation task — those are OUT OF SCOPE.
- **Services/AgentRegistry.cs** — H25 validation in ValidateReleasePreconditions(), ReviewDispatched marker methods, ClearAllReviewDispatchedMarkers in cleanup
- **Services/MarkerStore.cs** — ReviewDispatched marker methods (parallel implementation to AgentRegistry)
- **Services/WorkspaceCleaner.cs** — Added .review-dispatched to cleaned directories
- **Models/ReviewDispatchedMarker.cs** — New model (Task, DispatchedTo, Since)
- **Serialization/DydoJsonContext.cs** — JSON context for new marker model
- **Tests** — 12 new tests for baton-passing and review enforcement

### Out-of-scope (ignore these — Frank's worktree task):
- Commands/WorktreeCommand.cs
- Services/TerminalLauncher.cs
- Services/WindowsTerminalLauncher.cs
- Any .worktree-hold related changes in DispatchService.cs

## What to verify:

1. **Baton-passing correctness**: When dispatching on the same task, does the reply-pending marker get cleared? Does the new inbox item inherit reply_required: true?
2. **Review enforcement (H25)**: Does ValidateReleasePreconditions block dispatched code-writers (DispatchedBy != null) from releasing without a .review-dispatched marker? Does it correctly NOT block non-dispatched code-writers?
3. **Marker lifecycle**: Created when code-writer dispatches reviewer on same task, cleaned up on release and workspace clean
4. **Test coverage**: Do the 12 new tests adequately cover the happy paths and edge cases?
5. **No regressions**: All 2602+ existing tests should still pass

## Decision reference
Read dydo/project/decisions/010-baton-passing-and-review-enforcement.md for the full decision.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-16 16:29
- Result: PASSED
- Notes: LGTM. Baton-passing logic correctly clears reply-pending on same-task dispatch and inherits reply_required. H25 review enforcement blocks dispatched code-writers without review marker, correctly skips direct sessions and non-code-writer roles. MarkerStore follows existing patterns. 12 new tests cover happy paths and edge cases. 2 unrelated test failures from Frank's worktree task (window-id assertions) — out of scope.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:03
