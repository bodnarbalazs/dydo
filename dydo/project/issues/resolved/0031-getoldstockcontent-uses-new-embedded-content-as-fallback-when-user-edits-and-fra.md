---
id: 31
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-10
---

# GetOldStockContent uses new embedded content as fallback when user edits and framework update collide

Resolved low-severity correctness bug: when `storedHash` was null, `GetOldStockContent` used the new embedded content as the fallback, defeating user-edit detection during framework updates. Fixed by falling back to the on-disk content instead.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed: GetOldStockContent now falls back to onDisk instead of new embedded content when storedHash is null