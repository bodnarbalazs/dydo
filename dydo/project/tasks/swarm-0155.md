---
title: Swarm 0155
area: general
name: swarm-0155
status: stale
created: 2026-07-12T15:41:25.7541708Z
assigned: Brian
needs-human: false
---

# Task: swarm-0155

CODEX swarm fix ROUND 2 — issue 0155 (security). Your round-1 CODE is SECURE and VERIFIED: a Claude reviewer adversarially traced HandleDydoBashCommand and confirmed it now runs the full security analysis on the dydo-routed chain — `CheckDangerousPatterns(command)` on the full string (load-bearing: Analyze() early-returns with an EMPTY ops list when HasDangerousPattern, so the explicit dangerous check is REQUIRED and you got it right) then `AnalyzeAndCheckBashOperations` (Analyze + off-limits + cross-agent RBAC). No surviving bypass, order-independent, the wait/dispatch pending-state exception is preserved WITHOUT a new hole (the chain tail is still fully analyzed), no existing check weakened. KEEP the code in `Commands/GuardCommand.cs` EXACTLY as-is — do NOT change it. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

THE PROBLEM (test coverage only): the 5 tests assert real exit codes but OMIT the two most task-central vectors — the issue #0155 explicitly names "RBAC ... on the surrounding chain," and that RBAC code path (`BlockIfCrossAgentWorkspace` — writing into ANOTHER agent's workspace) has NO test; and the issue's own examples include a dydo-NOT-first ordering (`rm -rf / && dydo whoami`) which is also untested. Both are already correctly BLOCKED by your code (the reviewer confirmed) — they just need to be PINNED so a future regression can't silently reopen them.

FIX (build ON your round-1 diff — add ONLY tests, do NOT touch GuardCommand.cs):
Add two tests to `DynaDocs.Tests/Integration/GuardSecurityTests.cs`, matching the existing test style/harness there (look at how the existing 5 tests set up the agent, session, and workspaces — reuse that scaffold; the guard tests init multiple agents, so use a SECOND existing agent name from that setup as the "other agent"):
1. **Cross-agent-workspace WRITE via a dydo chain** (the RBAC-on-chain vector the issue names): a chain like `dydo whoami && tee dydo/agents/<OtherAgent>/notes.md` (or the write idiom the other tests use — `>`/`Set-Content`/`tee` into another agent's workspace path) → assert it is BLOCKED (exit code 2) via `BlockIfCrossAgentWorkspace` ("another agent's workspace"). Pick `<OtherAgent>` as an agent that exists in the test's init but is NOT the acting agent.
2. **dydo-not-first ordering**: `rm -rf / && dydo whoami` (dydo is LAST, dangerous op first) → assert BLOCKED (exit code 2, "Dangerous"). This proves the routing + full-string dangerous scan is order-independent — the exact shape in the issue's examples.
Keep your existing 5 tests. These two close the named gaps.

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` passes; run `dotnet test` filtered to GuardSecurityTests and confirm the two new tests PASS (proving the code already blocks these vectors) plus the existing ones still pass. Do NOT run the python coverage gate (0282) — the reviewer re-runs it.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0155-r2` with: the two tests added (the exact chains + the other-agent name you used + the asserted block reasons), confirmation both PASS against your unchanged code, build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch ONLY `DynaDocs.Tests/Integration/GuardSecurityTests.cs`. Do NOT modify `Commands/GuardCommand.cs` (verified correct — changing it would invalidate the review). Do NOT touch other swarm agents' files (WorktreeCommand.cs, AgentRegistry.cs, Sync/, Rules/, gap_check.py).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)

> Mass-closed 2026-07-16 (DR-041 campaign wrap-up): pre-campaign roster-era task; the work either landed before the pivot or was abandoned with the roster. See git history.
