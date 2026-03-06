---
area: general
name: filename-sanitization-brief-file
status: review-pending
created: 2026-03-06T20:39:40.1700288Z
assigned: Emma
updated: 2026-03-06T21:06:25.0979476Z
---

# Task: filename-sanitization-brief-file

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented filename sanitization and --brief-file option per plan. Added PathUtils.SanitizeForFilename() that replaces illegal Windows filename chars with dashes. Applied at filesystem boundary in DispatchCommand (inbox file creation + origin lookup) and TaskCommand (create, approve, reject, review-transition). Added --brief-file option to dispatch that reads brief from a file. Original task names preserved in metadata/frontmatter. Warning printed when sanitization changes the name. 13 new tests (6 unit + 7 integration), all 1278 tests pass. No plan deviations.