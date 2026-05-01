---
id: 142
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-04-30
---

# dydo dispatch must refuse to run in background — silent no-launch when run with run_in_background true

Open medium-severity bug: when `dydo dispatch` is invoked through Claude Code's `run_in_background: true`, the dispatch silently fails to launch the new terminal — no error, no agent claim, the caller assumes success. The fix is to detect background-execution context and refuse with an actionable error message instead of failing silently.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)