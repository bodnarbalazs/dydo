---
area: general
type: changelog
date: 2026-05-21
---

# Task: identity-hijack-fix-plan

Plan the implementation slice that closes the identity-hijack bug class (F1–F13 from Brian's inquisition) and the nine sub-issues filed by the prior judge: [#0183](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0183-identity-hijack-round-2-dydo-agent-role-mutates-other-agent-s-record-phantom-inb.md), [#0189](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0189-tests-agentregistrytests-getsessioncontext-prefersdydoagentenvvar-overfile-and-g.md), [#0190](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/0190-resolvesessionfallback-does-not-filter-by-assignedhuman-currenthuman-despite-the.md), [#0191](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0191-dydo-wait-stderr-suppressed-by-resume-bodies-in-all-three-terminal-launchers.md), [#0192](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0192-notice-handler-has-no-operator-escape-hatch-when-the-cited-inbox-file-is-unreach.md), [#0193](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0193-dydo-agent-claim-does-not-refuse-or-warn-when-dydo-agent-is-set-to-a-different-a.md), [#0194](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0194-lifecycle-audit-events-record-the-hijacked-agent-because-agentregistry-setrole-r.md), [#0195](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0195-duplicate-wait-dos-an-attacker-with-dydo-agent-x-can-hold-x-s-general-wait-slot.md), [#0196](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0196-two-phase-storesessioncontext-in-handledydobashcommand-publishes-an-unverifiable.md), [#0197](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0197-watchdog-and-terminal-launchers-do-not-scrub-or-pin-dydo-agent-on-child-processs.md).

Deliverable: a concrete implementation plan a code-writer can execute. Includes slice decomposition (bundling decisions with justifications), files-per-slice, test list, F1 fix-shape recommendation with code-read justification, verification recipe, worktree decision, coordination notes, and open questions for the user.

Plan (archived): `dydo/agents/Dexter/archive/20260519-175829/plan-identity-hijack-fix.md`

## Progress

- [x] Read Brian's inquisition report end-to-end (F1–F13, S0–S13, severity matrix, test-coverage gaps, lower-confidence areas)
- [x] Read all 10 sub-issues ([#0183](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0183-identity-hijack-round-2-dydo-agent-role-mutates-other-agent-s-record-phantom-inb.md), [#0189](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0189-tests-agentregistrytests-getsessioncontext-prefersdydoagentenvvar-overfile-and-g.md)–[#0197](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0197-watchdog-and-terminal-launchers-do-not-scrub-or-pin-dydo-agent-on-child-processs.md))
- [x] Read F1 pivot code: `GetSessionContext`, `GetCurrentAgent`, `SetRole`, `ExecuteRole`
- [x] Read F11 surface: `WaitCommand` end-to-end
- [x] Read F12 surface: `HandleDydoBashCommand` phase-1/phase-2, `AgentSessionManager.GetSessionContext`
- [x] Read F13 surface: `WatchdogService` end-to-end + launcher `ProcessStartInfo` paths
- [x] Read F4 encoded-bug tests + existing F1 reproducer (from inquisitor worktree)
- [x] Verify lower-confidence items (F13 watchdog, R2 wait exit-2) — no scout needed
- [x] Decide F1 fix shape (PID/ancestry verification on env paths, both primitives)
- [x] Decide slice decomposition (Slice A = identity core + adjacent defenses; Slice B = NOTICE escape + wait observability; [#0190](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/0190-resolvesessionfallback-does-not-filter-by-assignedhuman-currenthuman-despite-the.md) deferred)
- [x] Write `dydo/agents/Dexter/plan-identity-hijack-fix.md`
- [x] Message Adele that the plan is ready
- [x] User sign-off received via Adele (all five open questions resolved; plan locked)

## Files Changed

- `dydo/agents/Dexter/plan-identity-hijack-fix.md` (created)
- `dydo/project/tasks/identity-hijack-fix-plan.md` (this file)

## Review Summary

Plan signed off by user via Adele (2026-05-19). Slice A + Slice B + defer [#0190](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/0190-resolvesessionfallback-does-not-filter-by-assignedhuman-currenthuman-despite-the.md) confirmed; F1 option (a) with companion check in `GetCurrentAgent` confirmed; F14–F19 out of scope (docs-writer dispatch handles); LC audit-replay skipped; ownership gate stops at env paths only.

Ready for code-writer dispatch on Slice A.

## Approval

- Approved: 2026-05-21 19:06
