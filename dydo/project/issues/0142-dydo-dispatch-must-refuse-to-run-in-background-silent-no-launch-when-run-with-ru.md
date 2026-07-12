---
title: dydo dispatch must refuse to run in background — silent no-launch when run with run_in_background true
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: open
work-type: 
id: 142
type: issue
found-by: manual
date: 2026-04-30
---

# dydo dispatch must refuse to run in background — silent no-launch when run with run_in_background true
Open medium-severity bug: when `dydo dispatch` is invoked through Claude Code's `run_in_background: true`, the dispatch silently fails to launch the new terminal — no error, no agent claim, the caller assumes success. The fix is to detect background-execution context and refuse with an actionable error message instead of failing silently.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Attempted + deferred — needs a different approach (2026-07-13)

Batch 3 swarm attempt: refuse the dispatch when `CLAUDECODE` is set AND stdin is redirected. Adversarial review (with EMPIRICAL probing of this exact harness) FAILED it: foreground Claude Bash children have the SAME signal as background ones — `CLAUDECODE=1` + stdin redirected to `/dev/null` (fd 0) in BOTH contexts; no `*_BACKGROUND_*` env discriminator exists. So the guard would refuse EVERY legitimate agent-run launched dispatch (the primary dispatch path) — turning "background silently fails" into "foreground loudly fails for everyone." The override-based tests masked it (they inject the predicate, never exercise the real one). REVERTED.
KEEP (were correct): the refuse-BEFORE-mutation ordering (no half-dispatch) and the `--no-launch` bypass.
REDESIGN NEEDED: a pre-flight env/stdin guess CANNOT distinguish fg from bg from inside the process. The sound fix is POST-LAUNCH verification — launch, then confirm the terminal/claim materialized within a timeout, and roll back or error loudly if not — or an explicit caller contract. Also handle the non-Claude (codex) background case (CLAUDECODE unset → currently still silently fails).

## Resolution
(Filled when resolved)