---
area: general
name: swarm-0190
status: pending
created: 2026-07-12T18:49:25.3908968Z
assigned: Brian
needs-human: false
---

# Task: swarm-0190

CODEX swarm fix ROUND 2 — issue 0190. Your round-1 PRODUCTION fix in `Services/AgentSessionManager.cs` (ResolveSessionFallback now filters Working agents by AssignedHuman == DYDO_HUMAN, returns null when DYDO_HUMAN unset) is CORRECT and a Claude reviewer VERIFIED it (the leak is closed; real single-human callers still resolve because they always export DYDO_HUMAN to claim; the human source is consistent). KEEP the production fix EXACTLY — do NOT change AgentSessionManager.cs. The review FAILED on TEST fallout only. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

THE PROBLEM: your filter DETERMINISTICALLY breaks a pre-existing committed test — `DynaDocs.Tests/Services/WhoamiConcurrencyTests.cs` `SessionContextRace_SingleWorkingAgent_FallbackResolves` (~line 341). That test writes a single Working agent and expects ResolveSessionFallback to return "session-adele", but it NEVER sets DYDO_HUMAN. With your fix, the empty-DYDO_HUMAN early-return makes the fallback return null → `Assert.Equal("session-adele", sessionId)` fails on EVERY clean run (this is the "expected session, got null" failure, not flakiness). Also, your new 4th test is a duplicate.

FIX (TEST-ONLY — do NOT touch production code):
1. `WhoamiConcurrencyTests.SessionContextRace_SingleWorkingAgent_FallbackResolves`: set DYDO_HUMAN=testuser (SAVE the prior value and RESTORE it in a finally/try, mirroring the save/restore pattern already used in `AgentSessionManagerTests` / `AgentRegistryTests`) so the fallback caller has a human — this reflects reality (a real fallback caller always has DYDO_HUMAN set to have claimed). Ensure the single Working agent's state has AssignedHuman=testuser so it matches, and assert it still resolves "session-adele".
2. `WhoamiConcurrencyTests.SessionContextRace_OverwrittenByOtherTerminal_FallsBackCorrectly`: apply the same DYDO_HUMAN=testuser set/restore (+ AssignedHuman on the states) so it still exercises the AMBIGUITY guard path, not the new empty-human early-return (otherwise it passes vacuously).
3. De-duplicate in `DynaDocs.Tests/Services/AgentSessionManagerTests.cs`: `FallbackWithoutCurrentHumanWorking_ReturnsNull` is byte-identical to `FallbackWithOnlyOtherHumansWorking_ReturnsNull`. Rewrite it to actually cover the UNSET-DYDO_HUMAN early-return branch (run with DYDO_HUMAN CLEARED → ResolveSessionFallback returns null) — that branch currently has no test.

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` (0 errors); `dotnet test` filtered to WhoamiConcurrency + AgentSessionManager tests — ALL green, especially `SessionContextRace_SingleWorkingAgent_FallbackResolves` (previously failing) now PASSES, and the de-duplicated test covers the unset-human branch. Do NOT run the python coverage gate.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0190-r2` with: the three test edits (DYDO_HUMAN set/restore on the two Whoami tests + the de-dup covering the unset branch), confirmation WhoamiConcurrency now passes, build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch ONLY `DynaDocs.Tests/Services/WhoamiConcurrencyTests.cs` and `DynaDocs.Tests/Services/AgentSessionManagerTests.cs`. Do NOT touch `Services/AgentSessionManager.cs` (production fix is verified correct — changing it would invalidate the review). Do NOT touch peer files.

--- STANDING INSTRUCTIONS ---
1. NO DESTRUCTIVE GIT: never run git checkout/reset/stash/clean — shared tree holds peers' uncommitted work. Use dotnet build/test directly.
2. CONCURRENCY: other agents may be editing OTHER files; build errors only outside your files = a peer mid-edit — wait + rebuild, don't touch peer files.
3. REPORT+RELEASE as above; do NOT run the python gate (reviewer does).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)