---
area: general
name: agent-messaging
status: review-failed
created: 2026-03-07T23:54:38.2483623Z
assigned: Olivia
updated: 2026-03-08T00:24:29.3494260Z
---

# Task: agent-messaging

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented agent messaging: dydo message/msg command for sending messages between agents, dydo wait command for blocking until a message arrives, guard notification that blocks tool calls when unread messages exist (with automatic clearing on read), inbox show differentiation between dispatch and message items. Added 32 new tests (all pass), updated 4 templates. One plan deviation: added unread-message blocking to the glob/grep and bash guard paths (plan only mentioned the non-bash path). Edge case noted by user handled: dydo wait checks for existing messages before entering poll loop.

## Code Review (2026-03-08 01:05)

- Reviewed by: Adele
- Result: FAILED
- Issues: Implementation is excellent: MessageCommand, WaitCommand, guard integration, inbox differentiation, 32 tests all passing. One issue that must be fixed: CommandDocConsistencyTests.BuildRootCommand() is missing MessageCommand and WaitCommand. This means the meta-tests (Tests 1-7) have a blind spot for the new messaging commands - docs, help text, templates, and examples for message/wait are not verified for consistency. Add both commands to BuildRootCommand() in CommandDocConsistencyTests.cs (lines 19-38).

Requires rework.