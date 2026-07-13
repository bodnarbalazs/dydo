---
title: Swarm 0178
area: general
name: swarm-0178
status: pending
created: 2026-07-12T13:13:10.2665056Z
assigned: Mia
needs-human: true
---

# Task: swarm-0178

CODEX swarm fix — issue 0178 (MEDIUM). Self-contained; report then RELEASE YOURSELF (codex msg delivery isn't wired; uncommitted work persists for the chief-of-staff to sequence). Do NOT run the python test gates (0282 - the Claude reviewer runs them); make the code compile + reason correctness.

READ: dydo/project/issues/0178-*.md for full detail.

ISSUE: the self-review guard (an agent must not review its own work) is keyed on the EXACT task name, so a sub-task name with a suffix (e.g. 'foo' vs 'foo-slice1') defeats it - the same agent can review a variant of its own task. Fix: match on task FAMILY / agent identity rather than exact string equality, so a suffix/variant of the agent's own task is still caught as a self-review.

FILES (stay within these): Services/AgentClaimValidator.cs and/or Services/AgentRegistry.cs (the self-review check). Add/extend tests in the matching DynaDocs.Tests file proving a suffixed/variant task name is still blocked as self-review, while a genuinely different agent/task still passes.

REQUIRED: implement the family/identity match; test the suffix-defeat case + the legitimate-cross-review case; don't over-broaden (a different agent reviewing a same-named task from another must still work). Message Adele (dydo msg --to Adele --subject swarm-0178) with files, the match logic, the test, ~time, any prompt. THEN release yourself.

CONSTRAINTS: do NOT touch Commands/WorktreeCommand.cs or Services/WatchdogService.cs (parallel swarm agents own those). If your change needs AgentRegistry AND another agent also needs it, STOP and flag Adele rather than collide. Under the dydo guard + auto mode.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)