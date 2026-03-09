---
area: platform
name: clean-command-hardening
status: human-reviewed
created: 2026-03-09T12:08:57.8806488Z
assigned: Charlie
updated: 2026-03-09T13:10:29.3272193Z
---

# Task: clean-command-hardening

Extend CleanWorkspace to remove .waiting, .reply-pending, .auto-close markers and audit stale wait PIDs

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Slice 2: CleanWorkspace now removes .waiting/, .reply-pending/, and .auto-close artifacts. Added wait marker audit that counts markers before/after clean and reports (format: 'Audit: found N stale wait marker(s), cleaned M'). Audit runs in CleanAgent and CleanAll. No plan deviations. Omitted Clean_AuditPreservesAliveListeners test since WaitMarker lacks pid/listening fields (Slice 1 not landed). Pre-existing test compilation failure (ProcessUtils.PowerShellResolverOverride internal visibility) prevents running tests — unrelated to these changes.

## Code Review (2026-03-09 13:00)

- Reviewed by: Henry
- Result: FAILED
- Issues: Two issues: (1) Audit scope bug — CountWaitMarkers counts ALL agents globally but CleanAgent only cleans one, inflating the 'found N' number. (2) Three what-comments in CleanWorkspace restate the obvious and violate coding standards.

Requires rework.

## Code Review

- Reviewed by: Henry
- Date: 2026-03-09 13:10
- Result: PASSED
- Notes: LGTM. Audit scoped correctly per agent, what-comments removed, tests valid.

Awaiting human approval.