---
id: 72
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# CDN dependency without version pin or SRI hash in audit visualization

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Commands/AuditCommand.cs:311 pins vis-network@9.1.9 with integrity sha384-yxKDWWf0wwdUj/gPeuL11czrnKFQROnLgY8ll7En9NYoXibgg3C6NK/UDHNtUgWJ and crossorigin=anonymous. Fix commit 99a9a33. Verified by Charlie.