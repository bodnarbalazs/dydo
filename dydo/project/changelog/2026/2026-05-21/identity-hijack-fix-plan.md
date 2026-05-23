---
area: general
type: changelog
date: 2026-05-21
---

# Task: identity-hijack-fix-plan

Plan the implementation slice that closes the identity-hijack bug class (F1–F13 from Brian's inquisition) and the nine sub-issues filed by the prior judge: #0183, #0189, #0190, #0191, #0192, #0193, #0194, #0195, #0196, #0197.

Deliverable: a concrete implementation plan a code-writer can execute. Includes slice decomposition (bundling decisions with justifications), files-per-slice, test list, F1 fix-shape recommendation with code-read justification, verification recipe, worktree decision, coordination notes, and open questions for the user.

Plan: [dydo/agents/Dexter/plan-identity-hijack-fix.md](../../agents/Dexter/plan-identity-hijack-fix.md)

## Progress

- [x] Read Brian's inquisition report end-to-end (F1–F13, S0–S13, severity matrix, test-coverage gaps, lower-confidence areas)
- [x] Read all 10 sub-issues (#0183, #0189–#0197)
- [x] Read F1 pivot code: `GetSessionContext`, `GetCurrentAgent`, `SetRole`, `ExecuteRole`
- [x] Read F11 surface: `WaitCommand` end-to-end
- [x] Read F12 surface: `HandleDydoBashCommand` phase-1/phase-2, `AgentSessionManager.GetSessionContext`
- [x] Read F13 surface: `WatchdogService` end-to-end + launcher `ProcessStartInfo` paths
- [x] Read F4 encoded-bug tests + existing F1 reproducer (from inquisitor worktree)
- [x] Verify lower-confidence items (F13 watchdog, R2 wait exit-2) — no scout needed
- [x] Decide F1 fix shape (PID/ancestry verification on env paths, both primitives)
- [x] Decide slice decomposition (Slice A = identity core + adjacent defenses; Slice B = NOTICE escape + wait observability; #0190 deferred)
- [x] Write `dydo/agents/Dexter/plan-identity-hijack-fix.md`
- [x] Message Adele that the plan is ready
- [x] User sign-off received via Adele (all five open questions resolved; plan locked)

## Files Changed

- `dydo/agents/Dexter/plan-identity-hijack-fix.md` (created)
- `dydo/project/tasks/identity-hijack-fix-plan.md` (this file)

## Review Summary

Plan signed off by user via Adele (2026-05-19). Slice A + Slice B + defer #0190 confirmed; F1 option (a) with companion check in `GetCurrentAgent` confirmed; F14–F19 out of scope (docs-writer dispatch handles); LC audit-replay skipped; ownership gate stops at env paths only.

Ready for code-writer dispatch on Slice A.

## Approval

- Approved: 2026-05-21 19:06
