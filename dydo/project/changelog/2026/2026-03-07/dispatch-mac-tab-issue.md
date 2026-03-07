---
area: general
type: changelog
date: 2026-03-07
---

# Task: dispatch-mac-tab-issue

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed Mac tab dispatch: Terminal.app's 'do script in front window' runs in the existing tab instead of creating a new one. Added iTerm2 detection with native AppleScript tab support (create tab with default profile). Terminal.app tab mode now falls back to new window with an info message. Added ITerminalDetector interface for testability. All 1409 tests pass.

## Code Review

- Reviewed by: Paul
- Date: 2026-03-07 22:32
- Result: PASSED
- Notes: LGTM. Interface extraction (IProcessStarter, ITerminalDetector) correctly fixes One Type Per File violation. LaunchMac refactor with iTerm2 detection is clean. Guard tests for dotnet run patterns are thorough. Rules template-additions skip is consistent. All 1423 tests pass.

Awaiting human approval.

## Approval

- Approved: 2026-03-07 22:42
