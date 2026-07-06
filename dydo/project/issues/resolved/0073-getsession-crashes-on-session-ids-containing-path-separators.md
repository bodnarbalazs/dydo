---
title: GetSession crashes on session IDs containing path separators
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 73
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# GetSession crashes on session IDs containing path separators
Resolved low-severity correctness/security finding: `GetSession` crashed on session IDs containing path separators or traversal patterns rather than rejecting them. Fixed by validating session IDs in `AuditService` and throwing a clear exception on malformed input; cherry-picked as `e1c2886` with test alignment in `d012105`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
AuditService now validates session IDs against path separators and traversal patterns; rejects malformed input with a clear exception. Cherry-picked from Charlie's recovery branch onto master as e1c2886 (originally 7a50852). Test alignment shipped in d012105 (Henry). Verified by CI run 24998191977.