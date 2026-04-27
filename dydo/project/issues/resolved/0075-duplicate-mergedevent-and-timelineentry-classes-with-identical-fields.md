---
id: 75
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-27
---

# Duplicate MergedEvent and TimelineEntry classes with identical fields

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

MergedEvent and TimelineEntry collapsed into a single shared type. Field-identical duplicates removed; AuditCommand.MergeTimelines maps directly to the unified type. Cherry-picked from Charlie's recovery branch onto master as c79d107 (originally ea49ead). Verified by CI run 24998191977.