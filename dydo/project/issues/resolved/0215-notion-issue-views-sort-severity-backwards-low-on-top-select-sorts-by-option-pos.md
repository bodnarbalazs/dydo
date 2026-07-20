---
id: 215
area: general
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-06
resolved-date: 2026-07-07
---

# Notion Issue views sort severity backwards (low on top): select sorts by option position, options are critical-first so descending shows low first

Notion Issue board views sorted severity with Low on top: descending sort on a select property orders by option position, and the options are defined critical-first.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed + shipped in 2.0.3: Issue views 'Open' and 'Needs Attention' severity sort flipped descending->ascending so critical/high land on top (Notion sorts selects by option position; options are critical-first). Landed 525d96e; regression test 098a521 (both view sorts asserted). Model/template fix; live boards reflect it on next fresh provision (create-only reconcile, tracked separately).