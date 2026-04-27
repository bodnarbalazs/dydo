---
id: 73
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# GetSession crashes on session IDs containing path separators

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

AuditService now validates session IDs against path separators and traversal patterns; rejects malformed input with a clear exception. Cherry-picked from Charlie's recovery branch onto master as e1c2886 (originally 7a50852). Test alignment shipped in d012105 (Henry). Verified by CI run 24998191977.