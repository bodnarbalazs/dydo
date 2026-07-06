---
title: Tool-scoped file nudges never fire for Tier-2 workers (worker lane skips CheckFileNudges)
id: 212
area: general
type: issue
severity: medium
status: open
found-by: manual
date: 2026-07-04
---

# Tool-scoped file nudges never fire for Tier-2 workers (worker lane skips CheckFileNudges)

Tier-2 workers are governed by the guard (off-limits, dangerous-bash, bash nudges, dydo-command block) but the worker lane never evaluates tool-scoped file nudges, so path-keyed guidance like 'do not hand-edit migrations' is invisible to exactly the code-writers it targets.

## Description

HandleWorkerCall (Commands/GuardCommand.cs, Tier-2 lane entered when hook input carries agent_id) checks only BlockIfPathOffLimits for direct file/search paths and returns. CheckFileNudges is only called from HandleWriteOperation, and there it is additionally gated on string.IsNullOrEmpty(agentType) citing Decision 026 §4. The 026 §4 intent was narrower: the managers-doctrine source-write nudge must not fire for workers (they are supposed to edit source). The implementation made ALL tool-scoped file nudges Tier-1-only instead of just that one. Bash-command nudges are unaffected (CheckNudges runs before the isWorker branch in HandleBashCommand). Ruling from balazs (2026-07-04): workers correctly have no claimed identity, but nudges should still fire for them. Fix shape: add a tier/audience field to NudgeConfig (default: both tiers), evaluate file nudges in HandleWorkerCall for nudges scoped to workers, and mark the 026 §4 managers-doctrine nudge tier-1-only. Note: live guard behavior is currently the installed pre-2.0 binary's regardless — verify after the 2.0 install lands.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)