---
area: services
type: inquisition
---

# Agent Lifecycle Inquisition

## 2026-04-03 — Charlie

### Scope
- **Entry point:** Feature investigation — agent lifecycle system (dispatched by Adele)
- **Focus areas:** AgentRegistry, agent claiming/release, workspace archival, dispatch markers, reply-pending markers
- **Files investigated:** Services/AgentRegistry.cs, Services/IAgentRegistry.cs, Services/AgentClaimValidator.cs, Services/AgentCrudOperations.cs, Services/AgentSelector.cs, Services/AgentSessionManager.cs, Services/AgentStateStore.cs, Services/WorkspaceArchiver.cs, Services/WorkspaceCleaner.cs, Services/MarkerStore.cs, Services/DispatchService.cs, Services/InboxService.cs, Services/InboxMetadataReader.cs, Services/RoleConstraintEvaluator.cs, Services/FileCoverageService.cs, Models/AgentState.cs, Models/AgentSession.cs, Models/AgentStatus.cs, Commands/AgentLifecycleHandlers.cs
- **Docs cross-checked:** dydo/understand/architecture.md, dydo/understand/dispatch-and-messaging.md
- **Scouts dispatched:** 5 reviewers (Emma, Frank, Grace, Henry, Iris)

### Findings

#### 1. InboxMetadataReader only reads active inbox, not archive
- **Category:** bug
- **Severity:** high
- **Type:** obvious
- **Evidence:** `Services/InboxMetadataReader.cs:25` — `ReadFrontmatterField` searches only `inbox/`, never `archive/inbox/`. Compare with `DispatchService.GetOriginForTask` (`DispatchService.cs:721-722`) which correctly searches both directories. If an agent clears their inbox before calling `dydo agent role`, the metadata lookup at `AgentRegistry.cs:567-568` returns null. This permanently loses `DispatchedBy` and `DispatchedByRole` in agent state, breaking dispatch-restriction constraint evaluation and conditional must-read logic. No fallback mechanism exists. Confirmed by Grace.
- **Judge ruling:** [pending]

#### 2. Massive code duplication — AgentRegistry reimplements 40 methods from helper classes
- **Category:** antipattern
- **Severity:** high
- **Type:** obvious
- **Evidence:** `Services/AgentRegistry.cs` (2176 lines) duplicates all methods from three helper classes instead of delegating:
  - **AgentSessionManager** (10 methods): `GetPendingSessionPath` (ASM:29/AR:814), `GetSession` (ASM:38/AR:1189), `GetCurrentAgent` (ASM:60/AR:757), `GetPendingSessionId` (ASM:108/AR:856), `StorePendingSessionId` (ASM:129/AR:877), `GetSessionContext` (ASM:151/AR:902), `StoreSessionContext` (ASM:169/AR:928), `FileReadWithRetry` (ASM:192/AR:827), plus path helpers
  - **AgentStateStore** (13 methods): `GetAgentState`, `UpdateAgentState`, `WriteStateFile`, `ParseStateFile`, `ParseStatus`, `ParseTaskRoleHistory`, `ParsePathList`, `FormatTaskRoleHistory`, field parser dicts, and null-string helpers
  - **MarkerStore** (17 methods): All wait marker, reply-pending, and dispatch marker methods (MarkerStore:19-253, AgentRegistry:952-1185)
  
  AgentRegistry has minor enhancements not in the helpers (`GetCurrentAgent` adds DYDO_AGENT env var fast path at AR:762; `GetAgentState` adds `.queued` status overlay at AR:710-714). Suggests extraction happened first, then AgentRegistry diverged rather than delegating. Confirmed by Emma (40 duplicated methods enumerated).
- **Judge ruling:** [pending]

#### 3. Three dead production classes — AgentSessionManager, AgentStateStore, MarkerStore
- **Category:** dead-code
- **Severity:** medium
- **Type:** obvious
- **Evidence:** None instantiated in production code:
  - `new AgentSessionManager` — only in `DynaDocs.Tests/Services/AgentSessionManagerTests.cs:20`
  - `new AgentStateStore` — only in `DynaDocs.Tests/Services/AgentStateStoreTests.cs:15`
  - `new MarkerStore` — only in `DynaDocs.Tests/Services/MarkerStoreTests.cs:14`
  
  Only cross-reference: `AgentStateStore.cs:121` calls `AgentSessionManager.FileReadWithRetry` (static). All production code goes through AgentRegistry. Confirmed independently by both Emma and Frank.
- **Judge ruling:** [pending]

#### 4. IsAgentFree in AgentCrudOperations lacks stale dispatch timeout
- **Category:** bug
- **Severity:** high
- **Type:** obvious
- **Evidence:** `AgentCrudOperations.cs:358-363` — `IsAgentFree` treats Dispatched/Queued as unconditionally busy. `AgentRegistry.cs:142-144` — `IsEffectivelyFree` correctly adds a 2-minute stale timeout via `IsStaleDispatch` (line 146-149). CRUD operations (rename, remove, reassign) are blocked indefinitely on stale-dispatched agents. Grace also found that `AgentRegistry.cs:1634-1639` `IsAgentActive` has the identical bug — same missing timeout, same three CRUD callers. If a dispatch terminal fails to launch or an agent crashes before claiming, the agent becomes permanently unmodifiable without manual state file editing.
- **Judge ruling:** [pending]

#### 5. .session-context shared file race condition
- **Category:** bug
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `AgentRegistry.cs:817-818` — `.session-context` is a single shared file in the agents directory. `AgentRegistry.cs:938` — unprotected `File.WriteAllText` on every bash command via guard hook. `DYDO_AGENT` env var (AR:902-908) mitigates READS by bypassing the file, but all agents still WRITE to the shared file without locking. `.session-agent` hint file (AR:820-821) has the same race. File locking exists for claims (`.claim.lock`) but not for session context. Practical impact in multi-agent dispatch: session ID lookups could return wrong agent's session. Partially mitigated because dispatched terminals have `DYDO_AGENT` set. Confirmed by Grace.
- **Judge ruling:** [pending]

#### 6. WorkspaceArchiver SystemManagedEntries incomplete
- **Category:** bug
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `WorkspaceArchiver.cs:5-12` missing entries:
  - **Fixed-name gaps:** `.dispatch-markers` (dir, created at AR:1153), `.waiting` (dir, created at AR:952), `.review-dispatched` (legacy dir), `.auto-close` (legacy file)
  - **Structural gap:** `.role-nudge-*`, `.no-launch-nudge-*`, `.nudge-*`, `.claim-nudge-*` are dynamically suffixed. The HashSet uses exact `Path.GetFileName` matching (line 24), so dynamic names cannot be matched — would require prefix-based filtering. Surviving nudge markers get incorrectly archived during SetupAgentWorkspace. Confirmed by Iris.
- **Judge ruling:** [pending]

#### 7. Architecture.md has 6 doc/code discrepancies
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Confirmed by Henry:
  1. **WRONG** (Worktree Dispatch): Claims `git worktree prune` handles orphans. Code uses reference counting; tests assert prune is NOT called (`WorktreeCommandTests.cs:1173-1197`).
  2. **WRONG** (Watchdog): Says orphan detection checks "Working state + parent PID." Code checks FREE agents + wait process PID (`WatchdogService.cs:265-306`).
  3. **MISLEADING** (Dispatch): `--wait` described as "blocking until a response." Actually async — creates marker, returns immediately, requires explicit `dydo wait` (`DispatchService.cs:111-122`).
  4. **OMISSION**: `.worktree-root` marker created (`DispatchService.cs:310`) but not documented.
  5. **OMISSION**: Junctions for roles, issues, inquisitions created (`TerminalLauncher.cs:81-90`) but only `agents/` junction documented.
  6. **IMPRECISE** (Role System): `{self}` placeholder described as resolved from "dydo.json and agent identity." Actually resolved from agent identity only (`AgentRegistry.cs:563-564`); `{source}` and `{tests}` come from dydo.json.
- **Judge ruling:** [pending]

#### 8. CleanupAfterRelease vs WorkspaceCleaner artifact gaps
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Confirmed by Iris. Two cleanup mechanisms with different coverage:
  - **Gap A:** `WorkspaceCleaner.CleanWorkspace` (WC:189-211) does NOT clean nudge markers (`.role-nudge-*`, `.no-launch-nudge-*`, `.nudge-*`, `.claim-nudge-*`). A manual `dydo clean` leaves stale nudge markers behind.
  - **Gap B:** `CleanupAfterRelease` (AR:499-524) does NOT clean `.review-dispatched` directory. Legacy dir left behind on release.
  - **Gap C:** Single-agent `CleanAgent` (WC:31-71) does NOT clean worktree markers, but `CleanAll` (WC:74-125) does in a second pass (lines 112-121). Inconsistency between single and all modes.
- **Judge ruling:** [pending]

#### 9. AgentStatus.Reviewing never set in production
- **Category:** dead-code
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Models/AgentStatus.cs:9` defined. Deserialized: AR:1421, ASS:184. Checked: `DispatchService.cs:528`, `WatchdogService.cs:237`. Assigned ONLY in test: `AgentStateStoreTests.cs:107`. No production code transitions an agent to Reviewing. Either incomplete feature or leftover from removed transition. Confirmed by Frank.
- **Judge ruling:** [pending]

#### 10. Dead cleanup targets in WorkspaceCleaner
- **Category:** dead-code
- **Severity:** low
- **Type:** obvious
- **Evidence:** Confirmed by Frank:
  - `.auto-close` in `FilesToClean` (WC:9) — pre-v1.3 artifact. Never created in production. Tests annotate as "pre-v1.3 backward compat" (`WorkspaceAndCleanTests.cs:333,367`). Current mechanism uses `auto-close` field in state.md YAML.
  - `.review-dispatched` in `DirsToRemove` (WC:12) — renamed to `.dispatch-markers` per decision doc 012. Zero production code creates `.review-dispatched`.
- **Judge ruling:** [pending]

#### 11. Archive atomicity — partial failure risk in SetupAgentWorkspace
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `AgentRegistry.cs:261-262` — `ArchiveWorkspace` (`WorkspaceArchiver.cs:33-43`) moves files one-by-one in a loop. If a move fails mid-loop (locked file), some files are in the snapshot while others remain in workspace. The `catch` at AR:262 swallows silently. Workspace left in split state with no indication of failure. Practical risk moderate (same-filesystem moves usually atomic at OS level), but silent swallow means any failure is invisible. Confirmed by Iris.
- **Judge ruling:** [pending]

### Hypotheses Not Reproduced
- (No hypotheses were tested — all findings are obvious with code evidence)

### Confidence: high
All core agent lifecycle files were read thoroughly. Five parallel scouts independently audited code quality, dead code, bugs, docs consistency, and archiver/cleaner completeness. All scouts ran tests (3425 passing, 1 pre-existing failure — `ReadmeClones_ContentInSync` doc template sync). All scouts ran gap_check (all tiers passing). Not examined in depth: WatchdogService internals (only surface), QueueService internals, TerminalLauncher platform-specific code, audit replay system.
