---
id: 78
area: reference
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-09
---

# Audit docs missing GuardLift/GuardRestore events and lifted field

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Added `GuardLift` and `GuardRestore` event types to the "What Gets Audited" table in audit-system.md. Updated the Event Fields table: expanded the `agent` field's "Present" column to include GuardLift and GuardRestore events, and added the `lifted` field documenting the boolean flag set on file operations that succeed because the guard was lifted.