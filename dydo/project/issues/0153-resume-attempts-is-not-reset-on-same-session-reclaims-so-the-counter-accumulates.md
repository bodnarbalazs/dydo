---
id: 153
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-01
---

# resume-attempts is not reset on same-session reclaims, so the counter accumulates across crash episodes (decision 022 mismatch)

`ResumeAttempts` is reset only on fresh-claim and release paths, never on the same-session reclaim that every successful auto-resume actually flows through, so the counter accumulates across crash episodes. This contradicts Decision 022's "resets to 0 on `dydo agent claim`" wording and silences future auto-resumes once the cap is reached.

## Description

Finding 3 from auto-resume inquisition. Decision 022 §Retry cap (dydo/project/decisions/022-auto-resume-crashed-agents.md:55-57): The field resets to 0 on dydo agent claim (i.e. when the human or the workflow reclaims fresh) and on dydo agent release. In code, the fresh-claim path in SetupAgentWorkspace (Services/AgentRegistry.cs:381) writes s.ResumeAttempts = 0, but that branch only runs when there is no existing session OR the incoming sessionId differs (decision-018 stale-working reclaim). The same-session branch in HandleExistingSession (Services/AgentRegistry.cs:321-329) only calls RefreshClaimedPid and returns — no state mutation, no counter reset. Every successful auto-resume goes through the same-session path, so the counter set by IncrementResumeAttempts during the resume launch is never wiped. An agent that lived through two prior crashes (counter=2) now has only one resume budget left for the rest of its life — until dydo agent release (which does reset at :526) or a different sessionId arrives (extremely rare in practice). Compounded by Finding 2 (#0152): a single noisy crash can saturate the counter to 3 before the agent ever recovers, silencing every future crash. No test in WatchdogServiceTests or AgentRegistryTests asserts a reset on same-session reclaim — ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid only checks the PID refresh. Suggested fix: write state.ResumeAttempts = 0 alongside the RefreshClaimedPid call in the same-session branch (or extend RefreshClaimedPid itself to touch state.md). Add a regression test paralleling ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid. Decide explicitly whether decision 022's wording means ``any claim`` or ``fresh claim only`` and reflect in code + spec.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)