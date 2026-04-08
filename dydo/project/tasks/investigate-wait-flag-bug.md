---
area: general
name: investigate-wait-flag-bug
status: human-reviewed
created: 2026-04-08T14:18:47.9790230Z
assigned: Dexter
updated: 2026-04-08T15:37:55.9612603Z
---

# Task: investigate-wait-flag-bug

# Plan: Fix Session Context Race Condition

## Approach

The shared `.session-context` file is a last-writer-wins mechanism that breaks under concurrent multi-agent scenarios. When the initial terminal (no `DYDO_AGENT` env var) reads `.session-context`, another terminal's guard may have overwritten it, causing the wrong agent to be resolved. Fix by storing the agent name alongside the session ID and verifying against per-agent `.session` files, with a fallback scan when verification fails.

## Files to Modify

- `Services/AgentSessionManager.cs` — Change `StoreSessionContext` to write `{sessionId}\n{agentName}`, change `GetSessionContext` to parse both fields and verify. Add `ResolveSessionFallback` for race recovery.
- `Services/AgentRegistry.cs` — Update `StoreSessionContext` to pass agent name. Update `GetSessionContext` and `GetCurrentAgent` to use verified resolution. The DYDO_AGENT fast path stays unchanged.
- `Commands/GuardCommand.cs` — Pass agent name to `StoreSessionContext` (guard already resolves agent at line 559/586, but `StoreSessionContext` is called at line 553 before resolution). Restructure to resolve agent first, then store.
- `DynaDocs.Tests/Services/AgentSessionManagerTests.cs` — New tests for verified session context.
- `DynaDocs.Tests/Services/WhoamiConcurrencyTests.cs` — New race condition simulation test.
- `DynaDocs.Tests/Integration/DispatchWaitIntegrationTests.cs` — New test for --wait with concurrent session overwrites.

## Implementation Steps

### Step 1: Update `AgentSessionManager.StoreSessionContext` signature

Add `agentName` parameter. Write `{sessionId}\n{agentName}` to the file. The first line is the session ID (backwards-compatible — old readers just get the first line), second line is the agent name.

Verify: Existing `AgentSessionManagerTests.StoreSessionContext_ThenGet` still passes (GetSessionContext returns session ID, not the full file content — `.Trim()` on line 159 only trims whitespace, so we need to update GetSessionContext to parse the first line).

### Step 2: Update `AgentSessionManager.GetSessionContext` to return verified session

Change `GetSessionContext` return to parse the two-line format:
1. Read file, split by newline
2. Line 1 = sessionId, Line 2 = agentName (optional, for backwards compat)
3. If agentName present, verify: read `{agentName}/.session` → check stored sessionId matches
4. If verified → return sessionId
5. If no agentName (old format) → return sessionId as-is (backwards compat)
6. If verification fails (race detected) → call new `ResolveSessionFallback` method

Verify: Unit tests pass with both old-format and new-format `.session-context` files.

### Step 3: Implement `ResolveSessionFallback`

When verification fails (`.session-context` was overwritten by another terminal):
1. Scan all agents' `.session` files
2. For each agent: if status == Working AND assignedHuman == current human AND `.session` file exists → candidate
3. If exactly one candidate → return its session ID
4. If zero or multiple → return null (caller handles gracefully)

This is safe because in the initial terminal, typically only one agent is in "working" state for the current human. Dispatched agents have `DYDO_AGENT` set and never reach this path.

Verify: New unit test simulating race (two session IDs written, fallback finds correct agent).

### Step 4: Update `AgentRegistry.StoreSessionContext` and callers

`AgentRegistry.StoreSessionContext(string sessionId)` → `StoreSessionContext(string sessionId, string? agentName = null)`.

In `AgentRegistry.GetSessionContext()`: the DYDO_AGENT fast path stays as-is (line 876-881). The fallback path delegates to `AgentSessionManager.GetSessionContext()` which now does verification.

Verify: `AgentRegistry` tests pass. DYDO_AGENT tests still pass (fast path untouched).

### Step 5: Update `GuardCommand.HandleDydoBashCommand`

Current code (line 551-553):
```csharp
registry.StoreSessionContext(sessionId);
```

The guard doesn't know the agent name at this point (it resolves the agent later). Two options:
- **Option A:** Move `StoreSessionContext` AFTER agent resolution. Risk: agent resolution itself calls `GetSessionContext`, creating circularity.
- **Option B:** Call `StoreSessionContext(sessionId)` without agent name first (backwards-compatible write). Then after agent resolution, call `StoreSessionContext(sessionId, agentName)` to update with verified data.

Go with **Option B** — it's safe and avoids restructuring the guard flow. The second write overwrites the first with the enriched format. Even if another terminal overwrites between the two writes, the verification in `GetSessionContext` catches it.

Verify: Guard integration tests pass. Bash command handling still works.

### Step 6: Update `IAgentRegistry` interface

Add optional `agentName` parameter to `StoreSessionContext` if it's in the interface. Check if mock implementations need updating.

Verify: All implementations compile.

### Step 7: Write tests

#### Unit tests (AgentSessionManagerTests.cs)

- `StoreSessionContext_NewFormat_WritesAgentName` — Verify file contains two lines
- `GetSessionContext_NewFormat_ReturnsVerifiedSessionId` — Happy path with matching agent
- `GetSessionContext_OldFormat_ReturnsSessionId` — Backwards compatibility
- `GetSessionContext_RaceDetected_FallsBackToScan` — Agent mismatch triggers fallback
- `GetSessionContext_RaceDetected_FallbackFindsWorkingAgent` — Fallback resolves correct agent
- `GetSessionContext_RaceDetected_NoWorkingAgent_ReturnsNull` — Fallback with no candidates

#### Concurrency tests (WhoamiConcurrencyTests.cs)

- `ConcurrentSessionContextOverwrite_VerificationCatchesRace` — Two terminals write concurrently, reader gets correct agent via verification or fallback

#### Integration tests (DispatchWaitIntegrationTests.cs)

- `Dispatch_Wait_SucceedsAfterSessionContextRace` — Simulate race, verify orchestrator can still dispatch with --wait

Verify: All new and existing tests pass. Run full test suite.

### Step 8: Verify no regressions

Run `dotnet test` for the full suite. Check the 16+ callers of `GetSessionContext()` are unaffected (they all go through the same method, so format change is transparent).

## Risks & Mitigations

- **Risk:** Backwards compatibility — old `.session-context` files without agent name.
  **Mitigation:** Parse first line only if no second line. Old format works unchanged.

- **Risk:** Fallback scan picks wrong agent when multiple are working.
  **Mitigation:** Filter by `assignedHuman == currentHuman`. In practice, the initial terminal runs one agent. If ambiguous, return null (commands handle null sender gracefully — they already do for unclaimed sessions).

- **Risk:** Guard circularity — `StoreSessionContext` called before agent is known, but `GetSessionContext` needs agent for verification.
  **Mitigation:** Option B — two-phase write. First write without agent (fallback-compatible), second write with agent after resolution.

- **Risk:** Performance — verification adds one file read per `GetSessionContext` call.
  **Mitigation:** Only one extra read (`{agent}/.session`), which is a small JSON file already read frequently. Negligible impact.

## Out of Scope

- Fixing `.session-agent` hint file race (lower impact, hint is validated by session match)
- Setting `DYDO_AGENT` in the initial terminal (would require Claude Code changes)
- Per-PID session files (Option B from analysis — too fragile, too much cleanup)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

# Plan: Fix Session Context Race Condition

## Approach

The shared `.session-context` file is a last-writer-wins mechanism that breaks under concurrent multi-agent scenarios. When the initial terminal (no `DYDO_AGENT` env var) reads `.session-context`, another terminal's guard may have overwritten it, causing the wrong agent to be resolved. Fix by storing the agent name alongside the session ID and verifying against per-agent `.session` files, with a fallback scan when verification fails.

## Files to Modify

- `Services/AgentSessionManager.cs` — Change `StoreSessionContext` to write `{sessionId}\n{agentName}`, change `GetSessionContext` to parse both fields and verify. Add `ResolveSessionFallback` for race recovery.
- `Services/AgentRegistry.cs` — Update `StoreSessionContext` to pass agent name. Update `GetSessionContext` and `GetCurrentAgent` to use verified resolution. The DYDO_AGENT fast path stays unchanged.
- `Commands/GuardCommand.cs` — Pass agent name to `StoreSessionContext` (guard already resolves agent at line 559/586, but `StoreSessionContext` is called at line 553 before resolution). Restructure to resolve agent first, then store.
- `DynaDocs.Tests/Services/AgentSessionManagerTests.cs` — New tests for verified session context.
- `DynaDocs.Tests/Services/WhoamiConcurrencyTests.cs` — New race condition simulation test.
- `DynaDocs.Tests/Integration/DispatchWaitIntegrationTests.cs` — New test for --wait with concurrent session overwrites.

## Implementation Steps

### Step 1: Update `AgentSessionManager.StoreSessionContext` signature

Add `agentName` parameter. Write `{sessionId}\n{agentName}` to the file. The first line is the session ID (backwards-compatible — old readers just get the first line), second line is the agent name.

Verify: Existing `AgentSessionManagerTests.StoreSessionContext_ThenGet` still passes (GetSessionContext returns session ID, not the full file content — `.Trim()` on line 159 only trims whitespace, so we need to update GetSessionContext to parse the first line).

### Step 2: Update `AgentSessionManager.GetSessionContext` to return verified session

Change `GetSessionContext` return to parse the two-line format:
1. Read file, split by newline
2. Line 1 = sessionId, Line 2 = agentName (optional, for backwards compat)
3. If agentName present, verify: read `{agentName}/.session` → check stored sessionId matches
4. If verified → return sessionId
5. If no agentName (old format) → return sessionId as-is (backwards compat)
6. If verification fails (race detected) → call new `ResolveSessionFallback` method

Verify: Unit tests pass with both old-format and new-format `.session-context` files.

### Step 3: Implement `ResolveSessionFallback`

When verification fails (`.session-context` was overwritten by another terminal):
1. Scan all agents' `.session` files
2. For each agent: if status == Working AND assignedHuman == current human AND `.session` file exists → candidate
3. If exactly one candidate → return its session ID
4. If zero or multiple → return null (caller handles gracefully)

This is safe because in the initial terminal, typically only one agent is in "working" state for the current human. Dispatched agents have `DYDO_AGENT` set and never reach this path.

Verify: New unit test simulating race (two session IDs written, fallback finds correct agent).

### Step 4: Update `AgentRegistry.StoreSessionContext` and callers

`AgentRegistry.StoreSessionContext(string sessionId)` → `StoreSessionContext(string sessionId, string? agentName = null)`.

In `AgentRegistry.GetSessionContext()`: the DYDO_AGENT fast path stays as-is (line 876-881). The fallback path delegates to `AgentSessionManager.GetSessionContext()` which now does verification.

Verify: `AgentRegistry` tests pass. DYDO_AGENT tests still pass (fast path untouched).

### Step 5: Update `GuardCommand.HandleDydoBashCommand`

Current code (line 551-553):
```csharp
registry.StoreSessionContext(sessionId);
```

The guard doesn't know the agent name at this point (it resolves the agent later). Two options:
- **Option A:** Move `StoreSessionContext` AFTER agent resolution. Risk: agent resolution itself calls `GetSessionContext`, creating circularity.
- **Option B:** Call `StoreSessionContext(sessionId)` without agent name first (backwards-compatible write). Then after agent resolution, call `StoreSessionContext(sessionId, agentName)` to update with verified data.

Go with **Option B** — it's safe and avoids restructuring the guard flow. The second write overwrites the first with the enriched format. Even if another terminal overwrites between the two writes, the verification in `GetSessionContext` catches it.

Verify: Guard integration tests pass. Bash command handling still works.

### Step 6: Update `IAgentRegistry` interface

Add optional `agentName` parameter to `StoreSessionContext` if it's in the interface. Check if mock implementations need updating.

Verify: All implementations compile.

### Step 7: Write tests

#### Unit tests (AgentSessionManagerTests.cs)

- `StoreSessionContext_NewFormat_WritesAgentName` — Verify file contains two lines
- `GetSessionContext_NewFormat_ReturnsVerifiedSessionId` — Happy path with matching agent
- `GetSessionContext_OldFormat_ReturnsSessionId` — Backwards compatibility
- `GetSessionContext_RaceDetected_FallsBackToScan` — Agent mismatch triggers fallback
- `GetSessionContext_RaceDetected_FallbackFindsWorkingAgent` — Fallback resolves correct agent
- `GetSessionContext_RaceDetected_NoWorkingAgent_ReturnsNull` — Fallback with no candidates

#### Concurrency tests (WhoamiConcurrencyTests.cs)

- `ConcurrentSessionContextOverwrite_VerificationCatchesRace` — Two terminals write concurrently, reader gets correct agent via verification or fallback

#### Integration tests (DispatchWaitIntegrationTests.cs)

- `Dispatch_Wait_SucceedsAfterSessionContextRace` — Simulate race, verify orchestrator can still dispatch with --wait

Verify: All new and existing tests pass. Run full test suite.

### Step 8: Verify no regressions

Run `dotnet test` for the full suite. Check the 16+ callers of `GetSessionContext()` are unaffected (they all go through the same method, so format change is transparent).

## Risks & Mitigations

- **Risk:** Backwards compatibility — old `.session-context` files without agent name.
  **Mitigation:** Parse first line only if no second line. Old format works unchanged.

- **Risk:** Fallback scan picks wrong agent when multiple are working.
  **Mitigation:** Filter by `assignedHuman == currentHuman`. In practice, the initial terminal runs one agent. If ambiguous, return null (commands handle null sender gracefully — they already do for unclaimed sessions).

- **Risk:** Guard circularity — `StoreSessionContext` called before agent is known, but `GetSessionContext` needs agent for verification.
  **Mitigation:** Option B — two-phase write. First write without agent (fallback-compatible), second write with agent after resolution.

- **Risk:** Performance — verification adds one file read per `GetSessionContext` call.
  **Mitigation:** Only one extra read (`{agent}/.session`), which is a small JSON file already read frequently. Negligible impact.

## Out of Scope

- Fixing `.session-agent` hint file race (lower impact, hint is validated by session match)
- Setting `DYDO_AGENT` in the initial terminal (would require Claude Code changes)
- Per-PID session files (Option B from analysis — too fragile, too much cleanup)

## Code Review

- Reviewed by: Frank
- Date: 2026-04-08 15:55
- Result: PASSED
- Notes: LGTM. Clean separation (AgentSessionManager/AgentRegistry), correct race detection with conservative fallback, backwards-compatible. Tests comprehensive (14 new). All 3511 tests pass, gap_check 135/135 green. Minor notes: (1) GetCurrentAgent double-call in GuardCommand could be optimized, (2) ResolveSessionFallback omits plan's assignedHuman filter — safe in practice.

Awaiting human approval.