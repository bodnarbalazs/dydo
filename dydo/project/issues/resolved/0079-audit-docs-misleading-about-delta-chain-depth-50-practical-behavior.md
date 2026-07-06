---
id: 79
area: reference
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-07-04
---

# Audit docs misleading about delta chain depth 50 practical behavior

Open low-severity inquisition finding: `audit-system.md`'s description of the delta-chain depth-50 behavior misrepresents the practical effect on session reconstruction cost and snapshot resolution. Awaiting follow-up to align the doc with how the chain actually behaves end-to-end.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Outdated by the 2.0 pivot: the audit subsystem (snapshots/delta-chain) was torn down in dcf42c7; aligning audit-system.md's delta-chain-depth prose is moot. Doc removal itself is covered by the legacy-sweep inquisition (hardening backlog). Triage sweep 2026-07-04 (Brian, CoS).