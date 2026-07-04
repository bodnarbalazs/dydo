---
id: 33
area: project
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-08
---

# Decision doc 002 has stale role names and incomplete file list

Audit of decision doc 002 (template update system) flagged stale metadata and an incomplete shipped-hooks list. Resolved by flipping the status from `proposed` to `accepted`, adding the two missing hook points (`extra-complete-gate`, `extra-test-guidance`) to both the Shipped Hook Points and Changes Required sections, and confirming no stale role names remained.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Updated decision doc 002 status from `proposed` to `accepted`. Added two missing shipped hook points (`extra-complete-gate` for code-writer/reviewer, `extra-test-guidance` for test-writer) to both the Shipped Hook Points section and the Changes Required section. No stale role names were found — all role names in the document match current names.