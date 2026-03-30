---
area: general
name: fix-all-path-formats
status: review-pending
created: 2026-03-30T12:09:49.6203285Z
assigned: Charlie
updated: 2026-03-30T12:52:57.0774360Z
---

# Task: fix-all-path-formats

Added MSYS/Git Bash path format (/c/Users/...) on Windows and Write permission variants for all path formats. Extracted BuildPermissionEntries helper for testability. Updated existing tests to use dynamic counts via BuildPermissionEntries. Added 5 new tests: Write variants, MSYS on Windows, drive path includes MSYS, Unix path excludes MSYS, Read/Write pairing. All 3307 tests pass, coverage gap check clean.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added MSYS/Git Bash path format (/c/Users/...) on Windows and Write permission variants for all path formats. Extracted BuildPermissionEntries helper for testability. Updated existing tests to use dynamic counts via BuildPermissionEntries. Added 5 new tests: Write variants, MSYS on Windows, drive path includes MSYS, Unix path excludes MSYS, Read/Write pairing. All 3307 tests pass, coverage gap check clean.