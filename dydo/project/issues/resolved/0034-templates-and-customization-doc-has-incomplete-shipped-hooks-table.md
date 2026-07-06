---
title: Templates-and-customization doc has incomplete shipped hooks table
area: understand
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 34
type: issue
found-by: inquisition
date: 2026-04-08
---

# Templates-and-customization doc has incomplete shipped hooks table
Audit cross-checked `templates-and-customization.md`'s shipped-hooks table against the actual template files. The table was complete and accurate; the real gap was in decision doc 002's hooks section, fixed under #0033. Closed as a no-op for this doc.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Verified the shipped hooks table in templates-and-customization.md already lists all 6 hooks matching the actual template files. The decision doc 002 was updated to also include the two hooks that were missing from its shipped hooks section (extra-complete-gate, extra-test-guidance) — see issue #33.