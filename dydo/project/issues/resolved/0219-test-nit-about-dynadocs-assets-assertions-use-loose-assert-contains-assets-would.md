---
id: 219
area: general
type: issue
severity: low
status: resolved
found-by: review
date: 2026-07-07
resolved-date: 2026-07-12
---

# Test nit: about-dynadocs _assets assertions use loose Assert.Contains("_assets") — would pass even if placeholder asset ref dropped; tighten to 'dydo/_assets/'

(One-line summary)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-13 (landed 45e10f35). about-dynadocs _assets test assertion tightened from Assert.Contains(_assets) to Assert.Contains(dydo/_assets/) so dropping the placeholder asset reference now fails. Codex Emma (Terra).