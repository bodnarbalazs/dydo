---
id: 160
area: project
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-04
---

# SummaryRule lacks the _system/template-additions/ skip block its three sibling rules already have

## Description

Rules/SummaryRule.cs (full body, 23 lines) has no path filter. If doc.Title is empty it errors 'Missing title (# heading)'. Sibling rules FrontmatterRule:22-26, BrokenLinksRule:24-26, NamingRule:17-19 all carry an identical 4-line skip block:\n\n    if (normalized.StartsWith("_system/templates/", StringComparison.OrdinalIgnoreCase) ||\n        normalized.StartsWith("_system/template-additions/", StringComparison.OrdinalIgnoreCase))\n        yield break;\n\nSummaryRule is the outlier. Result: 4 spurious errors on dydo project (extra-complete-gate.md, extra-review-checklist.md, extra-review-steps.md, extra-verify.md) and 5 on LC (one extra: extra-test-guidance.md). Template-additions are content fragments spliced into other docs via {{include:name}} - per Templates/template-additions-readme.md they must NOT have H1s (would corrupt host doc heading hierarchy).\n\nFix: add the same 4-line skip block at the top of SummaryRule.Validate. Direct port of an existing pattern. See finding #6 for the deeper refactor that would prevent this class of bug.\n\nConfirmed by inquisition dydo-check-drift.md finding #2 (judge: Dexter).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)