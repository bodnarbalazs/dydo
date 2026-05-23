---
area: general
name: f11-guard-side-replan
status: pending
created: 2026-05-22T19:52:28.7792753Z
assigned: Charlie
---

# Task: f11-guard-side-replan

Re-plan of #0207 part 2: replace the rejected prompt-driven re-claim with a
guard-side `ClaimedPid` auto-refresh. The guard, on a resumed session's first
guarded tool call, refreshes `.session.ClaimedPid` to the live claude ancestor,
resets resume bookkeeping (#0153), and emits the `recovery_kind=auto` audit +
`resume_outcome=succeeded` log — all atomic under the agent's `.claim.lock`.

## Progress

- [x] Plan written — `dydo/agents/Charlie/plan-f11-guard-side.md`
- [x] Plan v2 — inquisitor-grade pass (31 edge cases, 3 proofs, 24 tests, status gate, TOCTOU fix, deadness-keyed trigger)
- [x] User sign-off on the plan (companion change accepted)
- [x] Charlie transitioned planner → orchestrator (briefly reverted, then re-graduated)
- [x] Code-writer dispatched: Dexter on `f11-guard-side-impl`
- [x] Plan-audit by Brian (inquisitor): 8 findings, all plan-text precision
- [x] Judge ruling by Emma: 8/8 CONFIRMED, design sound
- [x] **Adele REVERSED course-correction** — user confirmed Charlie's graduation; Charlie owns slice end-to-end
- [x] Plan revised in place per all 8 audit findings (Findings 1-6,8 + 3 new tests for Finding 7)
- [x] Brief + Dexter messaged with un-HALT + full diff of plan revisions
- [ ] Dexter completes implementation per revised plan (target: 27 unit tests + 3 live spikes + companion change)
- [ ] Reviewer dispatched on `f11-guard-side-review` after Dexter's reply
- [ ] Merge to master once review passes
- [ ] #0207 resolution proposed to user
- [ ] Docs-writer dispatched for Decision 022 + architecture.md amendments
- [ ] All dispatched agents released as work completes

## Files Changed

(Dexter implementing — see Dexter's report when in)

## Review Summary

(Pending)