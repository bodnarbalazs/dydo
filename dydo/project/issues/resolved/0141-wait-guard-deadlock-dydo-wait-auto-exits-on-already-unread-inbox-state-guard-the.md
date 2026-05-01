---
id: 141
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-04-30
resolved-date: 2026-05-01
---

# Wait-guard deadlock: 'dydo wait' auto-exits on already-unread inbox state, guard then blocks recovery (post-v1.4.0)

Open high-severity bug: under v1.4.0, `dydo wait` auto-exits in <1s when `state.md.UnreadMessages` and the inbox-dir scanner disagree (the snapshot says zero unread but the dir has files). The guard then blocks recovery because the agent has no live wait. Plan called for snapshotting from the inbox dir and tightening idempotency on duplicate registration; landed under task `fix-wait-guard-deadlock`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed by 65705e0 (Emma): WaitGeneral snapshot moved from state.md.UnreadMessages to MessageFinder.GetInboxMessageIds(inboxPath); idempotency guard refuses duplicate registrations with nonzero exit. Note: #0147 later refined the alignment to use UnreadMessages as canonical signal across all of dydo.