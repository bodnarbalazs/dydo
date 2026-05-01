---
id: 140
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-04-30
resolved-date: 2026-04-30
---

# ReserveAgent refuses stale-dispatch reclaim with no live launcher

Resolved high-severity finding: an observation that `ReserveAgent` refused stale-dispatch reclaim when no launcher process was alive. Closed as no-repro on master HEAD — five consecutive runs of the targeted test passed (10/10), CI green at run 25179154263, and the `IsReservable` gating is in place via commit `215e8d6`. Same likely-environmental story as #0139.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

No-repro on master HEAD (473af47). 5 consecutive runs of the targeted test via run_tests.py passed (10/10 test executions). CI green at run 25179154263. The IsReservable gating for stale-dispatched + dead-launcher reclaim is in place via 215e8d6 and continues to function. Same likely-environmental story as #0139.