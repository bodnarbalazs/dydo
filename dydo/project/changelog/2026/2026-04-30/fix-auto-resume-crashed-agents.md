---
area: general
type: changelog
date: 2026-04-30
---

# Task: fix-auto-resume-crashed-agents

Review commit c8e6d83 for fix-auto-resume-crashed-agents (Decision 022). Plan: dydo/agents/Quinn/plan-auto-resume-crashed-agents.md. Brief: dydo/agents/Brian/brief-fix-auto-resume-crashed-agents.md. Verify the per-agent lock contract is preserved (PollAndResumeForAgent uses TryAcquireLockAtPath then releases before delegating to registry.IncrementResumeAttempts which takes its own lock — no double-locking), the cap is enforced, the continuation prompt matches Decision 022 verbatim, and gap_check is green. NB: pre-existing CRAP on adjacent modules from prior commits this week is unrelated; only flag CRAP regressions caused by THIS commit.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit c8e6d83 for fix-auto-resume-crashed-agents (Decision 022). Plan: dydo/agents/Quinn/plan-auto-resume-crashed-agents.md. Brief: dydo/agents/Brian/brief-fix-auto-resume-crashed-agents.md. Verify the per-agent lock contract is preserved (PollAndResumeForAgent uses TryAcquireLockAtPath then releases before delegating to registry.IncrementResumeAttempts which takes its own lock — no double-locking), the cap is enforced, the continuation prompt matches Decision 022 verbatim, and gap_check is green. NB: pre-existing CRAP on adjacent modules from prior commits this week is unrelated; only flag CRAP regressions caused by THIS commit.

## Code Review

- Reviewed by: Adele
- Date: 2026-04-30 10:41
- Result: PASSED
- Notes: LGTM. Lock contract preserved (acquire→release in finally→delegate to IncrementResumeAttempts which takes its own lock; no double-locking). Cap=3 enforced and tested. Continuation prompt verbatim from Decision 022. gap_check 137/137. All 17 planned tests pass. 3 unrelated pre-existing flaky tests filed as #135/#136/#137 per user direction.

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
