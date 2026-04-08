---
area: general
name: fix-issues-template-bugs
status: review-pending
created: 2026-04-08T18:06:16.2726247Z
assigned: Charlie
updated: 2026-04-08T18:37:31.5807446Z
---

# Task: fix-issues-template-bugs

Fixed 5 template system bugs from inquisition: (1) StoreInitialFrameworkHashes now hashes doc and binary files alongside templates (#29), (2) Reanchor preserves insertion order for shared upper anchors by skipping past already-placed tags (#30), (3) GetOldStockContent falls back to onDisk instead of new embedded content when storedHash is null or irrecoverable (#31), (4) AccumulateResult counts warned files in summary output (#32), (5) FindLineIndex uses last occurrence for upper-only anchors to avoid frontmatter collision (#38). Also fixed CRLF double-encoding in test and added TerminalLauncher coverage tests for gap_check. All 3538 tests pass, 135/135 modules pass gap_check.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 5 template system bugs from inquisition: (1) StoreInitialFrameworkHashes now hashes doc and binary files alongside templates (#29), (2) Reanchor preserves insertion order for shared upper anchors by skipping past already-placed tags (#30), (3) GetOldStockContent falls back to onDisk instead of new embedded content when storedHash is null or irrecoverable (#31), (4) AccumulateResult counts warned files in summary output (#32), (5) FindLineIndex uses last occurrence for upper-only anchors to avoid frontmatter collision (#38). Also fixed CRLF double-encoding in test and added TerminalLauncher coverage tests for gap_check. All 3538 tests pass, 135/135 modules pass gap_check.