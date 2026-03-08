---
area: backend
type: changelog
date: 2026-03-08
---

# Task: inbox-clear-sync

Fix inbox clear to sync UnreadMessages in agent state

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added ClearAllUnreadMessages to AgentRegistry and IAgentRegistry. InboxCommand.ExecuteClear now calls ClearAllUnreadMessages on --all, and MarkMessageRead on --id clear. Added 4 tests: --all clears unread messages, --id clears specific message, empty inbox no error, guard sees no unread after clear. All 10 Inbox_Clear tests pass.

## Approval

- Approved: 2026-03-08 20:25
