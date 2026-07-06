---
title: Audit docs missing GuardLift/GuardRestore events and lifted field
area: reference
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 78
type: issue
found-by: inquisition
date: 2026-04-09
---

# Audit docs missing GuardLift/GuardRestore events and lifted field
`audit-system.md` did not list `GuardLift` and `GuardRestore` as audited event types, and the Event Fields table omitted the `lifted` boolean that marks file operations succeeding under a lifted guard. Resolved by adding both event types to the "What Gets Audited" table, expanding the `agent` field's "Present" column, and documenting the `lifted` field.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Added `GuardLift` and `GuardRestore` event types to the "What Gets Audited" table in audit-system.md. Updated the Event Fields table: expanded the `agent` field's "Present" column to include GuardLift and GuardRestore events, and added the `lifted` field documenting the boolean flag set on file operations that succeed because the guard was lifted.