---
title: S3 unread message delivery behaves as hard rule but categorized as soft-block
area: project
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 67
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-26
---

# S3 unread message delivery behaves as hard rule but categorized as soft-block
Resolved low-severity docs finding: S3 (unread-message delivery) was categorized as a Tier 2 soft-block but operationally re-fires every call with no marker/override, which is hard-rule behavior. Fixed in commit `5ffcb54` by reclassifying S3 as H30 and moving it to a new "Pending State" sub-section under Tier 3 so the description matches the semantics.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
S3 reclassified as H30 in commit 5ffcb54 — moved from Tier 2 (Soft-Blocks) to Tier 3 under new 'Pending State' sub-section in dydo/reference/guardrails.md. No marker/override; re-fires every call. Tier 2 description and S3's hard-rule semantics no longer contradict. Verified by Charlie.