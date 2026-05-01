---
id: 37
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# Init_StoresFrameworkHashes test too weak to catch missing hashes

Resolved low-severity test-quality finding: `Init_StoresFrameworkHashes` only checked that hashes existed without validating count or format, so it could pass even with framework files silently dropped. Fixed in commit `7756e7e` by tightening the assertions to verify hash count and format.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 7756e7e: Init_StoresFrameworkHashes test strengthened to verify hash count and format