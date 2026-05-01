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

Resolved medium-severity supply-chain finding: the audit visualization loaded `vis-network` from a CDN with no version pin and no SRI hash, leaving it vulnerable to CDN compromise. Fixed in commit `99a9a33` by pinning `vis-network@9.1.9` with an integrity hash and `crossorigin=anonymous`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Commands/AuditCommand.cs:311 pins vis-network@9.1.9 with integrity sha384-yxKDWWf0wwdUj/gPeuL11czrnKFQROnLgY8ll7En9NYoXibgg3C6NK/UDHNtUgWJ and crossorigin=anonymous. Fix commit 99a9a33. Verified by Charlie.