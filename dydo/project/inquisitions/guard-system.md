# Guard System Inquisition

## 2026-04-09 — Brian

### Scope
- **Entry point:** Feature investigation — guard system per orchestrator brief
- **Files investigated:** Commands/GuardCommand.cs (~1375 lines), Commands/GuardLiftCommand.cs, Services/BashCommandAnalyzer.cs, Services/OffLimitsService.cs, Services/MustReadTracker.cs, Services/PathPermissionChecker.cs, Services/GuardLiftService.cs, Services/ConfigFactory.cs, Utils/GlobMatcher.cs, Models/NudgeConfig.cs, dydo/files-off-limits.md
- **Docs cross-checked:** dydo/understand/guard-system.md, dydo/reference/guardrails.md
- **Scouts dispatched:** 3 reviewers (Iris security-1, Jack security-2, Kate quality-1, Leo docs-1), 1 test-writer (Mia hyp-1)

### Findings

#### 1. Guard lift self-escalation: agents can bypass RBAC by writing marker file
- **Category:** security
- **Severity:** critical
- **Type:** tested
- **Evidence:** Any agent can write `dydo/agents/{self}/.guard-lift.json` to its own workspace. All roles have write access to `dydo/agents/{self}/**`. The file is NOT protected by `files-off-limits.md`. `GuardLiftService.IsLifted()` (`Services/GuardLiftService.cs:42`) reads this marker with no integrity validation — no signature, no HMAC, no `liftedBy` verification. `HandleWriteOperation` (`Commands/GuardCommand.cs:272`) checks `IsGuardLifted` before RBAC and returns success immediately if true.

  **Attack path:** (1) Agent writes `.guard-lift.json` with valid JSON, (2) all subsequent writes bypass RBAC. Off-limits files (state.md, modes/**, workflow.md, files-off-limits.md) remain protected by Layer 1, but all other files become writable: source code, project docs, other agents' non-off-limits workspace files.

  **Test code (Mia — GuardLiftSelfEscalationTests):**
  ```csharp
  // Setup: claimed agent with code-writer role, must-reads completed
  // Step 1: Write .guard-lift.json to own workspace (RBAC allows this)
  var markerPath = Path.Combine(TestDir, "dydo", "agents", "Adele", ".guard-lift.json");
  File.WriteAllText(markerPath, """{"agent":"Adele","liftedBy":"self","liftedAt":"2026-01-01T00:00:00Z"}""");
  // Step 2: Verify IsLifted returns true
  Assert.True(new GuardLiftService(TestDir).IsLifted("Adele"));
  // Step 3: Write to dydo/** (normally blocked for code-writer) — succeeds
  var result = await GuardAsync("write", "dydo/some-file.md");
  result.AssertSuccess(); // RBAC bypassed
  ```

  **Fix:** Add `dydo/agents/*/.guard-lift.json` to `files-off-limits.md`. Alternative: store lift markers outside agent-writable paths (`dydo/_system/.local/guard-lifts/`).
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/GuardLiftService.cs (lines 1-71), Commands/GuardCommand.cs (lines 271-280, 330-342, 785-793, 1330-1334), Commands/GuardLiftCommand.cs (lines 56-103, 1224-1226), dydo/files-off-limits.md (full file), DynaDocs.Tests/Integration/GuardLiftSelfEscalationTests.cs (full file)
- **Independent verification:** Confirmed `.guard-lift.json` is NOT in files-off-limits.md. Confirmed `IsLifted()` performs no integrity validation — accepts any `liftedBy` value. Confirmed `HandleWriteOperation:272` returns Success before RBAC at line 283. Confirmed `HumanOnlyDydoCommandRegex` blocks agents from `dydo guard lift` CLI but not direct file writes. Confirmed `IsPathAllowed` for code-writer role permits `dydo/agents/{self}/**` which includes the marker path. Independently verified Mia's test suite — 5 tests covering the full exploit chain including blast radius validation (off-limits still blocks).
- **Alternative explanations considered:** Could off-limits implicitly cover this? No — only `state.md`, `.session`, `.pending-session` are listed, not `.guard-lift.json`. Could RBAC block the marker write? No — all roles permit `dydo/agents/{self}/**`.
- **Issue:** #0055

#### 2. Interpreter execution bypasses bash file operation analysis entirely
- **Category:** security
- **Severity:** high
- **Type:** obvious
- **Evidence:** `BashCommandAnalyzer` only detects shell-level file operations (commands, redirections). Commands like `python3 -c "open('secrets.json').read()"`, `node -e "require('fs').writeFileSync('src/file.cs','pwned')"`, or `bash -c "cat /etc/shadow"` bypass all file operation analysis. The analyzer tokenizes the command but doesn't understand that the inline code performs file operations. The only relevant dangerous pattern is `Base64DecodePipeExecRegex` which catches `base64 -d | python` but not `python -c "..."`.

  - `Services/BashCommandAnalyzer.cs`: `python`, `node`, `ruby`, `perl`, `php` are absent from all command dictionaries (`ReadCommands`, `WriteCommands`, `DeleteCommands`, etc.)
  - `Commands/GuardCommand.cs:640-679` (`HandleNonDydoBash`): passes command to `bashAnalyzer.Analyze()` which returns no file operations for interpreter commands
  - Off-limits checking depends on extracted file operations — if none are extracted, no off-limits check occurs
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/BashCommandAnalyzer.cs (lines 12-136, 301-332, 344-360, 440-509, 512-523, 628-702), Commands/GuardCommand.cs (lines 640-679, 731-746)
- **Independent verification:** Confirmed `python`, `python3`, `node`, `ruby`, `perl`, `php` absent from all command dictionaries (ReadCommands, WriteCommands, DeleteCommands, PermissionCommands, CopyMoveCommands). Confirmed `AnalyzeSubCommand` only checks sed/awk special cases and redirections for unknown commands. Confirmed `AnalyzeAndCheckBashOperations` iterates `analysis.Operations` — if empty (no recognized file ops), no off-limits or RBAC checks occur. `Base64DecodePipeExecRegex` catches `base64 -d | python` but not standalone `python -c "..."`.
- **Alternative explanations considered:** This is an inherent limitation of static shell analysis — the guard cannot execute arbitrary interpreter code to determine its file operations. However, detecting interpreter invocation patterns (`python -c`, `node -e`, etc.) as at minimum a warning is feasible and absent.
- **Issue:** #0056

#### 3. Command substitution/variable expansion warnings don't block
- **Category:** security
- **Severity:** high
- **Type:** obvious
- **Evidence:** `BashCommandAnalyzer.CheckBypassAttempts` (`Services/BashCommandAnalyzer.cs:353-359`) adds warnings but never blocks. An agent can use `$(echo rm) -rf path`, `CMD=cat; $CMD secrets.json`, or shell functions to evade command detection. The guard emits "WARNING: Command contains command substitution/variable expansion" but allows execution. The extracted file operations are based on the literal tokens, which don't reflect the actual runtime command.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/BashCommandAnalyzer.cs (lines 344-360, 192-203, 301-332, 440-509, 733-734), Commands/GuardCommand.cs (lines 459-471, 731-746)
- **Independent verification:** Confirmed `BypassChecks` array at line 344 only adds to `result.Warnings`, never sets `HasDangerousPattern` or returns early. Confirmed `HandleBashCommand` at lines 733-734 prints warnings via `Console.Error.WriteLine` then proceeds to file operation checks. The file operations extracted from commands containing `$(...)` or `$VAR` are based on literal tokens, not runtime-resolved values.
- **Alternative explanations considered:** Blocking all command substitution would be overly restrictive — legitimate uses like `$(git rev-parse HEAD)` are common. The issue is not that warnings don't block, but that when these patterns are present, the file operation analysis is known to be unreliable yet the guard proceeds as if it's reliable. A "tainted analysis" flag could trigger stricter review.
- **Issue:** #0057

#### 4. DangerousPatterns has gaps for non-Unix destructive commands
- **Category:** security
- **Severity:** high
- **Type:** obvious
- **Evidence:**
  - `rm -rf ./` (current directory) — not caught by `RecursiveDeleteRootRegex` which requires `/`, `~`, or `/*` after flags (`Services/BashCommandAnalyzer.cs:205`)
  - PowerShell `Remove-Item -Recurse -Force C:\` — no dangerous pattern covers this
  - Disk device regex `DdDiskWriteRegex` (`Services/BashCommandAnalyzer.cs:220`) only matches `/dev/sd[a-z]`, missing NVMe (`/dev/nvme*`), virtio (`/dev/vd*`), and eMMC (`/dev/mmcblk*`) devices
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/BashCommandAnalyzer.cs (lines 139-185, 205-272), Commands/GuardCommand.cs (lines 459-471, 682-746)
- **Independent verification:** Confirmed `RecursiveDeleteRootRegex` at line 205 requires `(/|~|/\*)` after flags — `./` does not match. Confirmed `RecursiveDeleteGlobRegex` catches `rm -rf *` but not `rm -rf ./`. Confirmed `DdDiskWriteRegex` at line 220 only matches `/dev/sd[a-z]`, missing NVMe/virtio/eMMC. No PowerShell dangerous pattern for `Remove-Item -Recurse -Force`. However: `rm -rf ./` would still be caught by RBAC as a delete operation extracted by `BashCommandAnalyzer`, and PowerShell `Remove-Item` is in `DeleteCommands` dictionary — these are partially mitigated by the RBAC layer.
- **Alternative explanations considered:** The dangerous patterns are an early-exit safety net; RBAC provides a second layer for the `rm -rf ./` and PowerShell cases. The NVMe gap is narrow but real. The gaps are real even if partially mitigated.
- **Issue:** #0058

#### 5. Nudge patterns match content inside command arguments (false positives)
- **Category:** bug
- **Severity:** high
- **Type:** tested
- **Evidence:** `CheckNudges` (`Commands/GuardCommand.cs:492-549`) matches regex patterns against the **entire bash command string** including argument values. Running `dydo dispatch --brief "text about git worktree add"` triggers the worktree nudge because the nudge pattern `\bgit\b[^;|&]*\bworktree\s+(add|remove)\b` matches text inside `--brief`. Observed during this investigation — my dispatch command was blocked.

  The nudge check runs before `IsDydoCommand()` routing, so even `dydo` commands are subject to nudge patterns matching on their argument text. Any nudge pattern that happens to match text in a `--brief`, `--body`, or `--task` argument will produce false positives.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 441-549, 492-549), Services/ConfigFactory.cs (lines 9-58)
- **Independent verification:** Confirmed `CheckNudges` at line 503 calls `regex.Match(command)` against the entire bash command string including arguments. Confirmed nudges run at line 451 BEFORE `IsDydoCommand()` at line 455. Verified the git worktree nudge pattern (`\bgit\b[^;|&]*\bworktree\s+(add|remove)\b` from ConfigFactory.cs:49) would match text inside `--brief` or `--body` arguments. The inquisitor observed this firsthand during investigation, which corroborates.
- **Alternative explanations considered:** Could nudge patterns be designed to match argument text? No — the worktree nudge targets the `git worktree add` shell command, not text about it. The false positive is a bug in the matching scope.
- **Issue:** #0059

#### 6. Off-limits bypass inconsistency: direct reads vs bash reads
- **Category:** bug
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `CheckDirectFileOffLimits` (`Commands/GuardCommand.cs:308-331`) calls `ShouldBypassOffLimits` for bootstrap and mode files on direct read operations. `CheckBashFileOperation` (`Commands/GuardCommand.cs:749-812`) does NOT have this bypass — it checks `offLimitsService.IsPathOffLimits(op.Path)` directly. Mode files like `dydo/agents/*/modes/*.md` match off-limits pattern `dydo/agents/*/modes/**`. Result: `Read dydo/agents/Kate/modes/reviewer.md` succeeds (bootstrap bypass), but `cat dydo/agents/Kate/modes/reviewer.md` is blocked by off-limits.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 308-331, 333-342, 749-812), Services/OffLimitsService.cs (lines 56-64), dydo/files-off-limits.md (line 29: `dydo/agents/*/modes/**`)
- **Independent verification:** Confirmed `CheckDirectFileOffLimits` (line 314) calls `ShouldBypassOffLimits` which invokes `IsAnyModeFile` for Stage 2 agents, allowing reads of any agent's mode files. Confirmed `CheckBashFileOperation` (lines 753-766) calls `offLimitsService.IsPathOffLimits(op.Path)` directly — no `ShouldBypassOffLimits` call. Verified `dydo/agents/*/modes/**` is in files-off-limits.md. Therefore `Read dydo/agents/Kate/modes/reviewer.md` succeeds (bootstrap bypass) but `cat dydo/agents/Kate/modes/reviewer.md` is blocked by off-limits.
- **Alternative explanations considered:** Agents should use the Read tool, not cat. But the guard should be consistent regardless — inconsistent enforcement erodes trust in the system and causes confusing agent failures.
- **Issue:** #0060

#### 7. Dead code in OffLimitsService (~70 lines)
- **Category:** dead-code
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `OffLimitsService.CheckCommand` (line 91), `ExtractPathsFromCommand` (line 252), `CommandPathPatterns` (line 238), and private `LooksLikePath` (line 291) are dead code. `CheckCommand` is not on the `IOffLimitsService` interface and has zero callers in production code or tests. The guard pipeline uses `BashCommandAnalyzer` for path extraction and calls `offLimitsService.IsPathOffLimits()` on individual paths. These are remnants of an earlier implementation approach. ~70 lines of dead code.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/OffLimitsService.cs (lines 91-101, 238-266, 291-309), Services/IOffLimitsService.cs (lines 15-35)
- **Independent verification:** Confirmed `CheckCommand` (line 91) is NOT on the `IOffLimitsService` interface — interface only exposes `LoadPatterns`, `IsPathOffLimits`, `Patterns`, `WhitelistPatterns`. Grepped for `.CheckCommand(` across all .cs files — zero callers found. `ExtractPathsFromCommand` (line 252) and `CommandPathPatterns` (line 238) are only reachable through `CheckCommand`. The private `LooksLikePath` (line 291) is only called by `ExtractPathsFromCommand`. All ~70 lines are dead code from an earlier implementation approach when off-limits checking was command-based rather than path-based.
- **Alternative explanations considered:** Could these be used by the `dydo check` validation command? No — grepped for all `.CheckCommand(` calls and found none.
- **Issue:** #0061

#### 8. Nudge evaluation order shadows dangerous pattern detection
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** In `HandleBashCommand` (`Commands/GuardCommand.cs:441-490`), `CheckNudges` (line 451) runs BEFORE `CheckDangerousPatterns` (line 459). For block-severity nudges, the nudge always blocks first and the dangerous pattern is never reached. For warn-severity nudges, first invocation blocks (creates marker), second invocation passes nudge but may then block on dangerous pattern — producing two separate error messages for the same command. The nudge system is configurable (dydo.json) while dangerous patterns are hardcoded — a misconfigured nudge could shadow a security check.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 441-490, 451, 459, 492-549)
- **Independent verification:** Confirmed `CheckNudges` runs at line 451 before `CheckDangerousPatterns` at line 459. For block-severity nudges, line 544-545 returns immediately, preventing dangerous pattern check from running. For warn-severity nudges, first invocation blocks at line 530, second invocation passes at line 533-534 then proceeds to dangerous pattern check at 459. Verified current default nudges (ConfigFactory) don't overlap with dangerous patterns, but custom nudges could. A misconfigured nudge matching `rm -rf /` would shadow H17.
- **Alternative explanations considered:** Could the ordering be intentional? No — security checks (hardcoded, non-configurable) should always take priority over configurable nudges. The inversion means a project owner could inadvertently shadow security checks.
- **Issue:** #0062

#### 9. Performance: 15 filesystem reads per guard invocation
- **Category:** inefficiency
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `GuardCommand.cs` calls `registry.GetCurrentAgent(sessionId)` at 15 locations (lines 227, 249, 259, 312, 403, 512, 566, 593, 603, 645, 690, 709, 771, 789, 1318). Each call reads the agent state file from disk. A typical non-dydo bash command path hits 6-10 of these per guard invocation. Additionally, `CheckNudges` (line 500) creates `new Regex()` per nudge per invocation for patterns that don't change during process lifetime. The guard runs on every single tool call.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 227, 249, 259, 312, 403, 512, 566, 593, 603, 645, 690, 709, 771, 789, 1318), Services/ConfigFactory.cs (line 500 in CheckNudges)
- **Independent verification:** Counted 15 `registry.GetCurrentAgent(sessionId)` call sites in GuardCommand.cs — confirmed. Not all hit on every invocation (different code paths), but a typical non-dydo bash command path hits 6-10 per invocation. Confirmed `CheckNudges` at line 500 creates `new Regex(nudge.Pattern, RegexOptions.IgnoreCase)` per nudge per invocation — patterns are static but compiled fresh each time. The guard is a short-lived CLI process (no caching opportunity), so the practical impact is low on local SSDs but could matter on network filesystems.
- **Alternative explanations considered:** The guard is invoked per tool call and exits — no opportunity for cross-invocation caching. Within a single invocation, a local variable could avoid redundant reads. Low practical severity but real waste.
- **Issue:** #0063

#### 10. H19 (indirect dydo invocation) is a configurable nudge, not a hard rule
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `guardrails.md` lists H19 as a Hard Rule ("Uncrossable") and the Extensibility section states H17-H20 are "hard-coded." The actual implementation is configurable nudges with `Severity = "block"` in `ConfigFactory.cs` (lines 11-38), loaded from `dydo.json`. These can be removed or edited by the project owner. `guard-system.md` lists it as a "Special block" under Bash Command Analysis. While functionally blocking, the mechanism is fundamentally different from hardcoded dangerous patterns.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/ConfigFactory.cs (lines 9-58), Commands/GuardCommand.cs (lines 451, 455, 492-549), dydo/reference/guardrails.md (lines 100-107, 139-144)
- **Independent verification:** Confirmed H19's patterns (`npx dydo`, `dotnet dydo`, etc.) are defined as `Severity = "block"` nudges in `ConfigFactory.DefaultNudges` (lines 11-38). Confirmed `CheckNudges` at line 492-549 processes these — same configurable mechanism as all nudges. Confirmed guardrails.md line 105 lists H19 under "Hard Rules (Uncrossable)" and line 141 states "Bash safety analysis (H17–H20, H26)" is "hard-coded." Confirmed `ConfigFactory.EnsureDefaultNudges` (line 96) adds missing defaults but doesn't prevent removal. A project owner editing `dydo.json` can remove these nudges.
- **Alternative explanations considered:** Functionally, block-severity nudges always block (no marker/retry mechanism), so the behavior matches a hard rule. But the mechanism is fundamentally different — it's configurable, not hardcoded. The doc should distinguish this.
- **Issue:** #0064

#### 11. Guard lift mechanism entirely undocumented
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `Commands/GuardLiftCommand.cs` (147 lines) provides `dydo guard lift <agent> [minutes]` and `dydo guard restore <agent>`. `GuardCommand.cs:271-279` checks `IsGuardLifted()` to bypass RBAC. `CheckBashFileOperation:792` checks it for bash writes. Neither `guard-system.md` nor `guardrails.md` mention guard lift at all. This is a mechanism that bypasses the entire RBAC layer and should be documented.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardLiftCommand.cs (lines 1-147), Commands/GuardCommand.cs (lines 271-280, 792, 1330-1334), Services/GuardLiftService.cs (lines 42-68), dydo/understand/guard-system.md (full file), dydo/reference/guardrails.md (full file)
- **Independent verification:** Searched guard-system.md for "lift" — zero mentions. Searched guardrails.md for "lift" — only found in HumanOnlyDydoCommandRegex context (line 1225) and as a human-only command category, but no dedicated guardrail entry documenting the RBAC bypass mechanism. GuardLiftCommand.cs is 147 lines implementing a significant feature. GuardCommand.cs at lines 271-280 and 792 uses `IsGuardLifted()` to bypass RBAC entirely.
- **Alternative explanations considered:** Guard lift is human-only (blocked for agents by `IsHumanOnlyDydoCommand`), so one might argue it's an admin tool. But it's a mechanism that bypasses the entire RBAC layer and is relevant to understanding the guard system. Its absence from both docs is a significant gap, especially given finding #1 (the self-escalation vulnerability exists partly because this mechanism is undocumented).
- **Issue:** #0065

#### 12. git merge worktree block and human-only command restriction lack guardrail IDs
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `git merge` is hard-blocked in worktree contexts (`Commands/GuardCommand.cs:706-729`) but has no guardrail ID in `guardrails.md`. Human-only command restriction (`GuardCommand.cs:563-578`, regex at line 1225) blocks agents from running `task approve/reject`, `roles reset`, `guard lift/restore` but is not listed in guardrails.md.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 706-729, 1221-1226), dydo/reference/guardrails.md (lines 69-70, 99-107)
- **Independent verification:** Confirmed `git merge` block at lines 706-729 is a distinct guardrail with no ID in guardrails.md. Confirmed `HumanOnlyDydoCommandRegex` at line 1225 blocks `task approve/reject`, `roles reset`, `guard lift/restore` for agents — implemented at lines 563-578. Neither has a guardrail ID. Guardrails.md lists H17-H26 and H27 but has no entry for these two enforcement points.
- **Alternative explanations considered:** The git merge block could be considered part of worktree enforcement, but it's a distinct hard rule with its own error message and deserves its own ID for completeness. The human-only restriction is a fundamental access control mechanism.
- **Issue:** #0066

#### 13. S3 (unread message delivery) doesn't follow soft-block pattern
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `guardrails.md` defines S-tier as "one-time blocking message... The agent CAN override by retrying." S3's own description says "Hard-blocks every operation until messages are read and cleared — this is a delivery mechanism, not a one-time check." S3 is a persistent block (not one-time), doesn't use a purpose-built marker file, and can't be overridden by retrying. Behaves more like a hard rule than a soft-block.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 896-930), dydo/reference/guardrails.md (lines 36-49)
- **Independent verification:** Confirmed S-tier definition (guardrails.md lines 36-39): "one-time blocking message... The agent CAN override by retrying... a marker file... On second attempt, the marker is found, deleted, and the command succeeds." Confirmed `NotifyUnreadMessages` (lines 896-930) checks `agent.UnreadMessages.Count == 0` — if not zero, blocks. No marker file created. Block persists until messages are read and cleared via `MarkMessageRead` / `dydo inbox clear`. The doc's own description at line 47 acknowledges: "this is a delivery mechanism, not a one-time check." S3 is correctly categorized as a persistent block, which contradicts the S-tier definition.
- **Alternative explanations considered:** S3 could be in a tier of its own. The doc acknowledges the deviation in S3's description, but it undermines the tier system's clarity to have an entry that contradicts its tier's definition.
- **Issue:** #0067

#### 14. Duplicate LooksLikePath implementations
- **Category:** antipattern
- **Severity:** low
- **Type:** obvious
- **Evidence:** `OffLimitsService.LooksLikePath` (`Services/OffLimitsService.cs:291`) and `BashCommandAnalyzer.LooksLikePath` (`Services/BashCommandAnalyzer.cs:687`) are separate implementations with divergent logic. The OffLimitsService version is simpler and more permissive. The BashCommandAnalyzer version checks known extensions, sensitive names, and rejects bracket/digit-colon patterns. Currently moot since OffLimitsService's version is dead code (finding 7), but risks inconsistency if revived.
- **Judge ruling:** CONFIRMED
- **Files examined:** Services/OffLimitsService.cs (lines 91-101, 238-266, 291-309), Services/BashCommandAnalyzer.cs (lines 687-702)
- **Independent verification:** Confirmed `OffLimitsService.LooksLikePath` (line 291) and `BashCommandAnalyzer.LooksLikePath` (line 687) are separate implementations with divergent logic. OffLimitsService version: simpler, checks `.`, `/`, `\\`, shell builtins. BashCommandAnalyzer version: checks known extensions, sensitive names, bracket/digit-colon patterns, path indicators. Currently moot because the OffLimitsService version is dead code (finding #7). If the dead code is removed, this issue resolves itself.
- **Alternative explanations considered:** N/A — currently moot. The finding is technically correct but has zero practical impact while finding #7 exists.
- **Issue:** #0068

#### 15. Stage 2 agents can read ALL agents' mode files via off-limits bypass
- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `ShouldBypassOffLimits` (`Commands/GuardCommand.cs:339`) calls `IsAnyModeFile` for agents with a role set, allowing reads of any agent's mode files (not just own). `guard-system.md` off-limits section mentions "Bootstrap bypass" and "Mode files" but doesn't clarify this scope expansion at Stage 2. An agent could read other agents' role instructions, which may contain task-specific sensitive information.
- **Judge ruling:** CONFIRMED
- **Files examined:** Commands/GuardCommand.cs (lines 333-342, 1120-1127), dydo/understand/guard-system.md (off-limits and staged access sections)
- **Independent verification:** Confirmed `ShouldBypassOffLimits` (line 339-340) calls `IsAnyModeFile(filePath)` when `agent.Role` is set — this matches `dydo/agents/*/modes/*.md` for ANY agent, not just the current one. Confirmed guard-system.md's staged onboarding section says Stage 1 adds "own mode files" and Stage 2 says "all reads allowed" — but doesn't clarify that the off-limits bypass scope expands at Stage 2 to include all agents' mode files specifically.
- **Alternative explanations considered:** Mode files are generic role templates, not task-specific. Reading another agent's mode file reveals role instructions but no sensitive data. This may be intentional — judges reviewing inquisition reports benefit from understanding other roles. The security risk is minimal, but the documentation gap is real.
- **Issue:** #0069

### Hypotheses Not Reproduced
- Nudge marker pre-emption for warn-severity nudges: creating `.nudge-{hash}` markers would bypass the first-attempt block, but matches intended behavior — the agent would proceed to the second attempt anyway. Block-severity nudges are unaffected.
- `.merge-source` / `.worktree` marker manipulation: these markers cause more restrictive behavior (blocking git merge), not privilege escalation.
- Heredoc stripping exploit: `CatHeredocRegex` is correctly scoped to `$(cat <<'WORD'...WORD)` patterns only. No meaningful exploit path found.

### Confidence: high
The guard system's core files were read thoroughly. Five scouts covered security, quality, docs, and edge cases. The critical guard lift bypass was confirmed by independent test. Bash analyzer coverage is thorough for the file analyzed. Areas not examined: audit trail integration (AuditService), conditional must-reads in depth, worktree-specific guard paths, integration tests. The nudge false-positive was observed firsthand.
