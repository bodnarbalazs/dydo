---
area: general
type: changelog
date: 2026-05-08
---

# Task: implement-pr3-instrumentation

Review PR3 of agent-crash-fixes (commit 036b88c). PURE INSTRUMENTATION — no behaviour change beyond schema additions.

What landed
- 3 nullable fields on AuditEvent for Claim events: recovery_kind ("fresh"|"auto"|"manual"), resume_predecessor_session, resume_attempts_at_claim. All JsonIgnoreCondition.WhenWritingNull.
- New watchdog log event resume_outcome ("succeeded"|"failed"|"gave_up") at three sites — see report-pr3.md and reviewer-brief.md in dydo/agents/Brian/.
- Same-session reclaim (HandleExistingSession) now emits a Claim audit event when LastResumeLaunchedAt was non-null pre-reset. Pre-PR3 the path was silent.
- New Services/RecoveryClassifier.cs holds the classification rule + auto-recovery emission to keep Services/AgentRegistry.cs under the T1 CRAP threshold.
- SaturateResumeAttempts also clears LastResumeLaunchedAt now (semantically: episode terminated). MarkResumeEpisodeTerminated was rolled into it.

Authoritative plan: dydo/agents/Brian/archive/20260507-110855/plan-agent-crash-fixes.md, §"PR3 - Instrumentation for the follow-up inquisition" (line 216).

Gates I ran
- dotnet build: clean
- DynaDocs.Tests/coverage/run_tests.py: 4183/4183 (4165 baseline + 18 new tests including balazs's BC pin)
- DynaDocs.Tests/coverage/gap_check.py --force-run: 141/141 modules at tier (RecoveryClassifier added as 141st, passes T1)

NOT in scope (out-of-role for code-writer)
- dydo/understand/architecture.md §Audit Trail and §Watchdog — needs a docs-writer follow-up. Same pattern PR1/PR2 had.

Linux CI is the remaining gate. Please push and watch master CI green before approving.

Read dydo/agents/Brian/reviewer-brief.md for the full key-decisions list and PR-boundary discipline notes.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review PR3 of agent-crash-fixes (commit 036b88c). PURE INSTRUMENTATION — no behaviour change beyond schema additions.

What landed
- 3 nullable fields on AuditEvent for Claim events: recovery_kind ("fresh"|"auto"|"manual"), resume_predecessor_session, resume_attempts_at_claim. All JsonIgnoreCondition.WhenWritingNull.
- New watchdog log event resume_outcome ("succeeded"|"failed"|"gave_up") at three sites — see report-pr3.md and reviewer-brief.md in dydo/agents/Brian/.
- Same-session reclaim (HandleExistingSession) now emits a Claim audit event when LastResumeLaunchedAt was non-null pre-reset. Pre-PR3 the path was silent.
- New Services/RecoveryClassifier.cs holds the classification rule + auto-recovery emission to keep Services/AgentRegistry.cs under the T1 CRAP threshold.
- SaturateResumeAttempts also clears LastResumeLaunchedAt now (semantically: episode terminated). MarkResumeEpisodeTerminated was rolled into it.

Authoritative plan: dydo/agents/Brian/archive/20260507-110855/plan-agent-crash-fixes.md, §"PR3 - Instrumentation for the follow-up inquisition" (line 216).

Gates I ran
- dotnet build: clean
- DynaDocs.Tests/coverage/run_tests.py: 4183/4183 (4165 baseline + 18 new tests including balazs's BC pin)
- DynaDocs.Tests/coverage/gap_check.py --force-run: 141/141 modules at tier (RecoveryClassifier added as 141st, passes T1)

NOT in scope (out-of-role for code-writer)
- dydo/understand/architecture.md §Audit Trail and §Watchdog — needs a docs-writer follow-up. Same pattern PR1/PR2 had.

Linux CI is the remaining gate. Please push and watch master CI green before approving.

Read dydo/agents/Brian/reviewer-brief.md for the full key-decisions list and PR-boundary discipline notes.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-07 22:38
- Result: PASSED
- Notes: PASS. Plan-faithful instrumentation. All five gates green: dotnet build clean, dydo check clean (4 pre-existing warnings), 4183/4183 tests, 141/141 modules at tier, Linux CI (run 25525664436) success on 036b88c. BC pin tests match balazs ask. Two cosmetic notes (C1 narrow TOCTOU, C2 stale reviewer-brief) — not blockers, see review notes.

Awaiting human approval.

## Approval

- Approved: 2026-05-08 12:36
