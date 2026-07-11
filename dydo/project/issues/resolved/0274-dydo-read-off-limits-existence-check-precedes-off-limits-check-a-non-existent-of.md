---
title: dydo read/off-limits: existence check precedes off-limits check - a non-existent off-limits path reports 'not found' instead of 'off-limits' (existence oracle)
id: 274
area: backend
type: issue
severity: low
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
resolved-date: 2026-07-11
---

# dydo read/off-limits: existence check precedes off-limits check - a non-existent off-limits path reports 'not found' instead of 'off-limits' (existence oracle)

c1-8 smoke (2026-07-11): 'dydo read .env' returned 'neither an inbox id nor an existing file path' (exit 1) because no .env exists in the repo - a missing-file error, NOT an off-limits block. 'dydo read dydo.json' (exists + off-limits) correctly BLOCKED. So off-limits ENFORCEMENT works, but the check ORDER leaks existence: for an off-limits pattern, a non-existent matching path should still report 'off-limits' rather than 'not found', so an unprivileged caller cannot use the error to probe whether an off-limits file exists (e.g. presence of a secrets file). Low severity (no content leak; the enforcement itself is correct), but the check order should be off-limits-before-existence for defense in depth. Route to the dydo read follow-up / H1.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-11: fix landed 7d21f85c. Off-limits check now runs BEFORE existence in dydo read, so a non-existent off-limits-pattern path returns BLOCKED (no existence oracle). Implemented by CODEX (Henry, ~7min auto mode), cross-vendor Claude-reviewed PASS (traced control flow, re-ran 30 tests, confirmed no enforcement weakening, no new leak). First codex->Claude-review->land loop, clean. Follow-up 0281 (low): empty-target now throws instead of friendly not-found (fails closed, no security impact).