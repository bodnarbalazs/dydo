---
id: 148
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-05-01
---

# Test suite runtime ballooned from 3min to 10min — investigate parallelism + other speedups

Open medium-severity perf finding: the test suite's wall-clock time grew from ~3 minutes to ~10 minutes. Investigation should look at parallelism settings (xunit collection / `dotnet test` `--maxcpucount`), per-test setup overhead, and whether recently-added integration tests are dominating the budget.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)