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

Resolved medium-severity correctness bug: `ListSessionFiles` returned `_baseline-` files alongside session files, inflating the session count and confusing audit consumers. Fixed in commit `99a9a33` by filtering out the `_baseline-` prefix in `AuditService.ListSessionFiles` and mirroring the filter in `SnapshotCompactionService`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

ListSessionFiles filters _baseline- prefix at Services/AuditService.cs:160 (.Where(f => !Path.GetFileName(f).StartsWith('_baseline-'))). SnapshotCompactionService.cs:192 mirrors the filter. Fix commit 99a9a33. Verified by Charlie in triage-audit.