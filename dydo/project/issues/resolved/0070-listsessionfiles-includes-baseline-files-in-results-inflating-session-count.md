---
id: 70
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# ListSessionFiles includes baseline files in results, inflating session count

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

ListSessionFiles filters _baseline- prefix at Services/AuditService.cs:160 (.Where(f => !Path.GetFileName(f).StartsWith('_baseline-'))). SnapshotCompactionService.cs:192 mirrors the filter. Fix commit 99a9a33. Verified by Charlie in triage-audit.