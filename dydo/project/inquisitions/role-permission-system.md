---
area: services
type: inquisition
---

# Inquisition: Role and Permission System

## 2026-04-08 — Brian

### Scope
- **Entry point:** Feature investigation — role and permission system (dispatched by Adele)
- **Files investigated:**
  - Services/RoleDefinitionService.cs (354 lines)
  - Services/IRoleDefinitionService.cs (17 lines)
  - Services/RoleConstraintEvaluator.cs (192 lines)
  - Services/PathPermissionChecker.cs (105 lines)
  - Services/AgentRegistry.cs (IsPathAllowed: 1156-1207, SetRole: 540-604, CanTakeRole: 689-693, CanRelease: 475-487, GetRelativePath: 1245-1258, MatchesGlob: 1260)
  - Services/DispatchService.cs (CanDispatch integration: ~497-506)
  - Services/MustReadTracker.cs (AddConditionalMustReads, InterpolatePath)
  - Services/ValidationService.cs (ValidateRoleFile, ValidateSingleRoleFile)
  - Commands/GuardCommand.cs (permission check at 278-290, 788)
  - Commands/RolesCommand.cs (create/reset/list)
  - Models/RoleDefinition.cs, RoleConstraint.cs, AgentState.cs, ConditionalMustRead.cs, ConditionalMustReadCondition.cs, PathsConfig.cs
  - Utils/GlobMatcher.cs (31 lines)
  - dydo/_system/roles/*.role.json (9 files)
- **Docs cross-checked:** dydo/understand/roles-and-permissions.md vs all implementation files
- **Scouts dispatched:** 3 reviewers (Grace, Henry, Iris), 1 test-writer (Jack)

### Findings

#### 1. PathPermissionChecker is dead production code
- **Category:** dead-code / antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `Services/PathPermissionChecker.cs` is never instantiated in production code. `new PathPermissionChecker` appears only in test files: `PathPermissionCheckerTests.cs:17,150` and `WorktreeCompatTests.cs:1009,1030,1052`. The guard calls `AgentRegistry.IsPathAllowed` (line 1156) directly — see `GuardCommand.cs:280,788`. This is the same dead-extraction pattern as MarkerStore, AgentSessionManager, and AgentStateStore (agent-lifecycle inquisition Finding #3). PathPermissionChecker is a 4th dead helper class. Confirmed independently by Grace.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/PathPermissionChecker.cs (all 105 lines), Services/AgentRegistry.cs (lines 1156-1261), Commands/GuardCommand.cs (lines 278-290, 788)
- **Independent verification:** Grepped for `PathPermissionChecker` across all .cs files in the project — zero production references. Only found in PathPermissionCheckerTests.cs and WorktreeCompatTests.cs. GuardCommand.cs lines 280 and 788 both call `registry.IsPathAllowed()` (AgentRegistry), never PathPermissionChecker.
- **Alternative explanations considered:** Could be staged for a future extraction/refactor — but no TODO, no issue, and the class has been stable since Mar 30. Dead code.
- **Issue:** #39

#### 2. IsPathAllowed logic duplicated between PathPermissionChecker and AgentRegistry
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `AgentRegistry.IsPathAllowed` (lines 1156-1207) and `PathPermissionChecker.IsPathAllowed` (lines 20-59) implement identical permission-checking logic. Additionally duplicated: `GetRelativePath` (AR:1245 vs PPC:86), `MatchesGlob` (AR:1260 vs PPC:101), `GetRoleRestrictionMessage` (AR:1214 vs PPC:61), `GetPathSpecificNudge` (AR:1226 vs PPC:73). ~80 lines of duplicated logic. No current drift between the two, but `PathPermissionCheckerTests` exercise the dead copy while `RoleBehaviorTests` exercise the production code — any future change to one without the other creates silent divergence. Confirmed by Grace.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/PathPermissionChecker.cs (lines 20-59, 61-68, 73-84, 86-99, 101-104), Services/AgentRegistry.cs (lines 1156-1207, 1214-1237, 1245-1260)
- **Independent verification:** Compared both implementations line-by-line. Logic is structurally identical: no-role check → ReadOnlyPaths loop with writable override → empty-writable check → writable match. Helper methods (GetRelativePath, MatchesGlob, GetRoleRestrictionMessage, GetPathSpecificNudge) are functionally identical. PathPermissionChecker.MatchesGlob delegates to GlobMatcher.IsMatch, and AgentRegistry.MatchesGlob does the same. No drift between the two — yet. Tests exercise the dead copy (PathPermissionCheckerTests) while production uses AgentRegistry.
- **Alternative explanations considered:** Intentional extraction in progress — but no TODO, no issue, and both copies are being tested independently, suggesting the duplication was accidental.
- **Issue:** #40

#### 3. Roles-and-permissions.md missing two constraint types
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `dydo/understand/roles-and-permissions.md` "Constraint Types" table (line 116) lists 3 types: `role-transition`, `requires-prior`, `panel-limit`. Code implements 5 types — also `requires-dispatch` (used by code-writer at `RoleDefinitionService.cs:24` and inquisitor at line 160) and `dispatch-restriction` (used by reviewer at line 60). Both are fully functional in `RoleConstraintEvaluator.cs:91-100` (requires-dispatch evaluated in CanRelease:106-147) and `RoleConstraintEvaluator.cs:152-181` (dispatch-restriction evaluated in CanDispatch). The "Role Transitions and Restrictions" section mentions H10/H11/H12 but has no entries for these two constraint types. Confirmed independently by Iris.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/understand/roles-and-permissions.md (lines 114-121), Services/RoleConstraintEvaluator.cs (lines 91-100, 106-147, 152-181), dydo/_system/roles/code-writer.role.json, dydo/_system/roles/inquisitor.role.json, dydo/_system/roles/reviewer.role.json
- **Independent verification:** Grepped all .role.json files for constraint types. Found `requires-dispatch` in code-writer.role.json (line 19) and inquisitor.role.json (line 18), and `dispatch-restriction` in reviewer.role.json (line 26). Both are fully implemented in RoleConstraintEvaluator: `requires-dispatch` evaluated in CanRelease (lines 106-147), `dispatch-restriction` evaluated in CanDispatch (lines 152-181). The constraint types table at doc line 116-121 only lists role-transition, requires-prior, and panel-limit.
- **Alternative explanations considered:** Could these be intentionally undocumented internal constraints? No — they're user-facing: requires-dispatch blocks agent release with a user-visible error message, and dispatch-restriction blocks dispatch commands. They should be documented.
- **Issue:** #41

#### 4. Roles-and-permissions.md IsPathAllowed flow description is wrong
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Doc section "How Permissions Map to File Paths" (line 84-90) describes a 3-step flow: off-limits check → WritablePaths match → denial. Actual `AgentRegistry.IsPathAllowed` (line 1156-1207) has a 4-step flow: no-role check → ReadOnlyPaths check (entirely omitted from docs) → empty-writable check → writable-match check. The off-limits check is NOT in IsPathAllowed at all — it's a separate stage in the guard pipeline (`GuardCommand.cs`). The doc conflates the guard pipeline with the permission checker. Confirmed by Iris.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/understand/roles-and-permissions.md (lines 84-90), Services/AgentRegistry.cs (lines 1156-1207), Commands/GuardCommand.cs (lines 277-290, 128-137 of the doc's own "Guard Resolves Permissions" section)
- **Independent verification:** Read AgentRegistry.IsPathAllowed (1156-1207) step-by-step. Actual flow: (1) no-agent check, (2) no-role check, (3) ReadOnlyPaths loop — if path matches any read-only pattern AND is not in WritablePaths, block, (4) empty-writable check, (5) writable-match check. The doc (line 84-88) describes: (1) off-limits check, (2) WritablePaths match, (3) denial. The off-limits check is NOT in IsPathAllowed — it's a separate guard pipeline stage (GuardCommand). ReadOnlyPaths is entirely omitted. The doc's own "How the Guard Resolves Permissions at Runtime" section (lines 128-137) is more accurate but the earlier section specifically misrepresents IsPathAllowed.
- **Alternative explanations considered:** The doc section could be describing the full guard pipeline abstractly rather than IsPathAllowed specifically — but the heading says "How Permissions Map to File Paths" and step 2 says "role's WritablePaths," implying it's describing the permission checker. The ReadOnlyPaths omission is the critical error; read-only overrides are a core feature of how roles like reviewer work.
- **Issue:** #42

#### 5. Roles-and-permissions.md incomplete glob pattern documentation
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** Doc line 90 says matching "converts glob patterns to regex (`**` → `.*`, `*` → `[^/]*`)". Actual `GlobMatcher.CompileGlob` (`Utils/GlobMatcher.cs:22-28`) has 4 conversions: `**/` → `(.*/)?`, `**` → `.*`, `*` → `[^/]*`, `?` → `.`. Two conversions missing from docs. Order matters: `**/` must be replaced before `**`. Confirmed by Iris.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/understand/roles-and-permissions.md (line 90), Utils/GlobMatcher.cs (all 31 lines)
- **Independent verification:** Read GlobMatcher.CompileGlob (lines 18-29) directly. The replacement chain is: (1) `\*\*/` → `(.*/)?`, (2) `\*\*` → `.*`, (3) `\*` → `[^/]*`, (4) `\?` → `.`. Doc line 90 only mentions `**` → `.*` and `*` → `[^/]*`. Missing: `**/` → `(.*/)?` (critical — this is the directory-prefix wildcard) and `?` → `.` (single-char wildcard). The order matters: `**/` must be replaced before `**` to avoid partial match corruption, which the code handles correctly.
- **Alternative explanations considered:** The doc could be intentionally simplified for readability — but the `**/` pattern is functionally important (it's optional-prefix matching, not just recursive descent), and omitting it gives a wrong mental model.
- **Issue:** #43

#### 6. Roles-and-permissions.md schema sample missing two fields
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** Doc section "Role Definition Schema" (line 57-69) shows a sample JSON with 8 fields. `Models/RoleDefinition.cs` has 10 fields — missing from doc sample: `CanOrchestrate` (bool, used by orchestrator/inquisitor/judge roles) and `ConditionalMustReads` (List, used by code-writer/reviewer roles). Both are serialized to JSON and round-trip correctly (tested in `RoleDefinitionServiceTests.cs:617-661`). Confirmed by Iris.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/understand/roles-and-permissions.md (lines 57-69), Models/RoleDefinition.cs (all 15 lines), dydo/_system/roles/orchestrator.role.json, dydo/_system/roles/code-writer.role.json, dydo/_system/roles/reviewer.role.json
- **Independent verification:** RoleDefinition.cs has 10 properties: Name, Description, Base, WritablePaths, ReadOnlyPaths, TemplateFile, DenialHint, CanOrchestrate, Constraints, ConditionalMustReads. The doc sample shows 8 fields. Verified both missing fields are actively used: `canOrchestrate: true` in orchestrator.role.json (line 15), `conditionalMustReads` with entries in code-writer.role.json (lines 31-40) and reviewer.role.json (lines 39-59).
- **Alternative explanations considered:** Fields could be optional/internal and intentionally omitted from docs — but both are configurable by custom role authors and affect guard behavior (CanOrchestrate gates --wait dispatch; ConditionalMustReads injects required reading). Custom role authors need to know these exist.
- **Issue:** #44

#### 7. Inconsistent case sensitivity in RoleConstraintEvaluator
- **Category:** coding-standards
- **Severity:** low
- **Type:** obvious
- **Evidence:** `RoleConstraintEvaluator.cs` uses case-sensitive comparison for `role-transition` (line 57: `previousRoles.Contains(constraint.FromRole!)`) and `requires-prior` (line 66: `taskRoles.Contains(r)`), but case-insensitive comparison for `requires-dispatch` (line 122: `StringComparer.OrdinalIgnoreCase`) and `dispatch-restriction` (lines 167, 173: `StringComparison.OrdinalIgnoreCase`). Mitigated by `AgentRegistry.SetRole` (line 550) validating against case-sensitive `_rolePermissions` dictionary, ensuring stored role names are always the canonical lowercase form. Risk is theoretical but the inconsistency violates uniform coding practices. Confirmed independently by Grace and Jack.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/RoleConstraintEvaluator.cs (lines 48-101, 106-147, 152-181), Services/AgentRegistry.cs (lines 540-604, specifically 550-553)
- **Independent verification:** Traced case sensitivity through each constraint type: `role-transition` (line 57) uses `List<string>.Contains` which is ordinal/case-sensitive. `requires-prior` (line 66) same. `requires-dispatch` (line 122) uses `StringComparer.OrdinalIgnoreCase`. `dispatch-restriction` (lines 167, 173) uses `StringComparison.OrdinalIgnoreCase`. Verified the mitigation: AgentRegistry.SetRole (line 550) checks `_rolePermissions.ContainsKey(role)` — Dictionary uses default ordinal comparer, so only exact-case role names pass. Role definitions use lowercase names ("code-writer", "reviewer", etc.). Risk is theoretical but the inconsistency is a coding standards violation — all comparisons should use the same convention.
- **Alternative explanations considered:** The case-insensitive comparisons in requires-dispatch/dispatch-restriction might be intentional because they compare against `dispatchedByRole` which could come from external input — but that value also goes through SetRole validation, so it's always canonical lowercase too.
- **Issue:** #45

#### 8. GlobMatcher recompiles regex on every call without caching
- **Category:** inefficiency
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Utils/GlobMatcher.cs:18-29` — `CompileGlob` creates `new Regex(pattern, RegexOptions.Compiled)` on every call. The `RegexOptions.Compiled` flag causes JIT compilation to native code, which is expensive (~10x slower than interpretation for first use). The guard pipeline calls `MatchesGlob` via `AgentRegistry.IsPathAllowed` on every write tool call, checking each glob pattern against the target path. Patterns are deterministic per session — they don't change after role assignment. A `ConcurrentDictionary<string, Regex>` cache would eliminate redundant compilation. Confirmed by Grace. Henry (security reviewer) also noted this as a defense-in-depth opportunity (add `MatchTimeout` to prevent ReDoS from crafted custom role patterns).
- **Judge ruling:** CONFIRMED
- **Files examined:** Utils/GlobMatcher.cs (all 31 lines), Services/AgentRegistry.cs (line 1260, IsPathAllowed 1177-1189)
- **Independent verification:** Read GlobMatcher.CompileGlob (lines 18-29). Every call to `IsMatch` calls `CompileGlob` which creates `new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)`. `RegexOptions.Compiled` triggers JIT compilation to IL — expensive on first use per pattern. The guard pipeline calls IsPathAllowed on every write tool call, iterating over all writable and read-only patterns. Patterns are fixed after role assignment and never change mid-session. A static `ConcurrentDictionary<string, Regex>` cache keyed by normalized pattern would eliminate redundant compilation. Henry's MatchTimeout suggestion for ReDoS defense is also sound (patterns come from .role.json which is trusted, but custom roles broaden the trust surface).
- **Alternative explanations considered:** Could the JIT/runtime cache compiled Regex internally? .NET does NOT cache Regex instances created with `new Regex()` — only `Regex.IsMatch()` static methods use the internal cache (capped at 15 by default). This is a genuine miss.
- **Issue:** #46

#### 9. Panel-limit constraint counts the requesting agent against itself
- **Category:** bug
- **Severity:** low
- **Type:** tested
- **Evidence:** `RoleConstraintEvaluator.cs:73-89` — the panel-limit check counts ALL agents with `role == targetRole && task == targetTask && status != Free`. If an agent already holds the limited role on the task (e.g., recovering after a crash and re-setting the same role), it counts against its own limit. With `MaxCount=1`, the agent blocks itself from re-setting. Test by Jack confirms: `CanTakeRole_PanelLimit_AgentBlockedByOwnCount` — agent with role=judge, task=task1, MaxCount=1 fails CanTakeRole for the same role/task combination. Arguably correct (idempotent re-set shouldn't be needed), but could cause issues in crash recovery scenarios.
  ```csharp
  // Test that demonstrates the behavior (Jack):
  // Alice already holds judge on task1 with MaxCount=1
  // CanTakeRole("Alice", "judge", "task1") returns false
  // because activeCount includes Alice herself
  ```
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/RoleConstraintEvaluator.cs (lines 73-89), Services/AgentRegistry.cs (lines 540-604, specifically 556-559)
- **Independent verification:** Traced the flow: CanTakeRole (called at AgentRegistry.SetRole:556) calls EvaluateConstraint for each constraint. For panel-limit (lines 73-89), the loop iterates ALL `_agentNames`, counting agents where `s.Role == role && s.Task == task && s.Status != AgentStatus.Free`. The requesting agent (`agentName`) is not excluded from the count. If Alice already holds judge on task1 (Status=Working), and CanTakeRole is called for Alice/judge/task1, Alice's own state contributes to activeCount. With MaxCount=1, activeCount=1 >= 1, blocking the re-set. This matters in crash recovery: if an agent's session dies and a new session re-claims the identity, the state file still shows the role, but the agent may need to re-run SetRole as part of re-onboarding.
- **Alternative explanations considered:** Could be intentional — "if you already have the role, don't re-set it." But SetRole doesn't short-circuit when the agent already holds the target role; it always goes through CanTakeRole. A simple fix would be to skip the requesting agent in the count, or short-circuit SetRole when role+task are unchanged.
- **Issue:** #47

#### 10. H10/H11/H12 labels are doc-only conventions with zero code traceability
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `dydo/understand/roles-and-permissions.md` uses labels H10, H11, H12 for constraints. No matching comments, constants, or references exist anywhere in the codebase. These labels exist only in the documentation. Noted by Iris.
- **Judge ruling:** CONFIRMED
- **Files examined:** dydo/understand/roles-and-permissions.md (lines 98-108), all .cs files (grep for H10/H11/H12), all .role.json files (grep for H10/H11/H12)
- **Independent verification:** Grepped for "H10", "H11", "H12" across all .cs and .role.json files — zero matches. These labels exist only in roles-and-permissions.md (lines 98, 102, 107). The constraint implementations in RoleConstraintEvaluator.cs have no comments, constants, or string references linking back to these labels. If the constraint logic changes, there's no traceable link from code to doc section.
- **Alternative explanations considered:** Doc-only labels can serve a purpose as reader-facing cross-references — but the "H" prefix suggests hypothesis identifiers, implying traceability that doesn't exist. Either add code comments referencing these labels, or drop the labels from the doc in favor of descriptive headings.
- **Issue:** #48

### Hypotheses Not Reproduced
- **Role-transition case sensitivity bypass:** Jack confirmed that `List.Contains` uses ordinal (case-sensitive) comparison, so a hypothetical case mismatch in TaskRoleHistory would bypass the constraint. However, all role names in the codebase are validated as lowercase at the `SetRole` boundary, so this is not exploitable in practice. Test passes (correct behavior observed).
- **Security vulnerabilities in permission system:** Henry's security audit found no exploitable issues. Path traversal handled by .NET path APIs. SubstituteConstraintVars output goes to stderr only. GlobMatcher ReDoS is theoretical (patterns from trusted source). Source-generated JSON prevents deserialization gadgets. Consistent slash normalization + IgnoreCase regex prevents guard bypass.

### Confidence: high
All core role/permission files were read thoroughly. Four parallel scouts independently audited code quality (Grace), security (Henry), doc consistency (Iris), and edge cases with new tests (Jack). All scouts ran the full test suite (3511-3520 tests passing) and gap_check (135/135 modules pass). Not examined in depth: ConditionalMustRead evaluation logic in MustReadTracker (surface-level only), RolesCommand create/reset (human-only commands, lower risk), AgentRegistry constructor role loading pipeline.
