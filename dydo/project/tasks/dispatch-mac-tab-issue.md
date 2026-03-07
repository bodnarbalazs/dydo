---
area: general
name: dispatch-mac-tab-issue
status: review-pending
created: 2026-03-07T21:12:05.9774563Z
assigned: Leo
---

# Task: dispatch-mac-tab-issue

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed Mac tab dispatch: Terminal.app's 'do script in front window' runs in the existing tab instead of creating a new one. Added iTerm2 detection with native AppleScript tab support (create tab with default profile). Terminal.app tab mode now falls back to new window with an info message. Added ITerminalDetector interface for testability. All 1409 tests pass.