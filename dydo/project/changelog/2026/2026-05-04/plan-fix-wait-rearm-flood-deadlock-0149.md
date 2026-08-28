---
area: general
type: changelog
date: 2026-05-04
---

# Task: plan-fix-wait-rearm-flood-deadlock-0149

Plan the implementation for fixing [#0149](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0149-wait-rearm-gap-deadlocks-agent-when-3-plus-messages-stack-faster-than-agent-can-process.md) (wait re-arm flood deadlock). Synthesizes Adele co-thinker findings + Charlie inquisition into an executable plan for a code-writer.

## Progress

- [x] Read Adele co-thinker findings (archive/20260501-194057/notes-0149-findings.md)
- [x] Read Charlie inquisition report (dydo/project/inquisitions/wait-rearm-flood-deadlock.md)
- [x] Read primary code paths: WaitCommand.WaitGeneral, GuardCommand.HandleDydoBashCommand, MessageFinder.FindMessage, AgentRegistry.CreateListeningWaitMarker, WaitMarker
- [x] Decided fix shape: registration-time snapshot of UnreadMessages passed as excludeIds (Adele's approach, implementation-cleaner than Charlie's timestamp filter)
- [x] Decided scope: bypass fix ([#0155](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0155-rbac-off-limits-and-dangerous-pattern-checks-bypassed-for-any-bash-chain-contain.md)) is a SEPARATE slice
- [x] Wrote plan: dydo/agents/Adele/plan-fix-wait-rearm-flood-deadlock-0149.md
- [ ] Plan path delivered to Noah; release.

## Files Changed

- dydo/agents/Adele/plan-fix-wait-rearm-flood-deadlock-0149.md (new)

## Review Summary

(Pending — code-writer slice, then reviewer)

## Approval

- Approved: 2026-05-04 21:52
