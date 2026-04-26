---
id: 67
area: project
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# S3 unread message delivery behaves as hard rule but categorized as soft-block

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

S3 reclassified as H30 in commit 5ffcb54 — moved from Tier 2 (Soft-Blocks) to Tier 3 under new 'Pending State' sub-section in dydo/reference/guardrails.md. No marker/override; re-fires every call. Tier 2 description and S3's hard-rule semantics no longer contradict. Verified by Charlie.