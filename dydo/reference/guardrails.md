---
area: reference
type: reference
---

# Guardrails

The three-tier system that shapes agent behavior. Every guardrail falls into one of three categories based on how strictly it constrains the agent.

> **2.1 note ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)).** dydo ceded agent identity to the platforms, so everything gated on a claimed agent is gone from the guard: staged read access (**H3**), the other-agent-workflow read block (**H4**), must-read write enforcement (**H5**), the search-tool identity lockout (**H6**), the no-identity/no-role write blocks (**H7/H8**), and the human-only-command block (**H29**). Per-role write RBAC (**H1**) and the **N7/N8** denial hints were already removed in 2.0 ([Decision 024](../project/decisions/024-dydo-2-native-pivot.md)). The guard is now **universal off-limits (H2) + nudges + dangerous-bash + git-safety** only: reads, searches, and writes by anyone are allowed unless the path is off-limits or the command is dangerous. **H12** (judge panel) was retired with the judge role.

---

## Tier 1: Nudges (Contextual Injection)

Passive guidance injected into command output when relevant. The agent is not blocked — it receives additional context to make a better decision. If the situation doesn't apply, the nudge is not shown.

**Mechanism:** Extra text appended to stdout/stderr of a command that succeeds (exit code 0). The operation proceeds regardless.

### Instances

| # | Name | Trigger | Message |
|---|------|---------|---------|
| N2 | Bash command warnings | Bash command contains suspicious-but-not-dangerous patterns: command substitution, base64/hex decode, variable expansion, embedded newlines. | `WARNING: Command contains base64 decode - potential obfuscation` (etc.) |
| N3 | Daily validation | First guard call per 24-hour period runs a background validation of config/roles. Issues are reported but never block. | `Daily validation found issues: [...] Run 'dydo validate' for full report.` |

---

## Tier 2: Soft-Blocks (One-Time Stop)

A one-time blocking message that forces the agent to acknowledge before proceeding. The agent CAN override by retrying — the block catches mistakes, not deliberate choices.

**Mechanism:** Command returns exit code 2 on first attempt and writes a marker file. On second attempt, the marker is found, deleted, and the command succeeds. Think of it as a "are you sure?" checkpoint.

### Instances

| # | Name | Trigger | Marker File | Message |
|---|------|---------|-------------|---------|
| S6 | Agent tool notice | Agent uses Claude Code's built-in `Agent` tool. The call always succeeds — a stderr NOTICE reminds the agent that sub-agent tool calls run in the Tier-2 worker lane: anonymous, governed by the universal guard layers (off-limits, dangerous-bash, nudges). | *(none — warn-and-allow, every call passes)* | `NOTICE: You invoked Claude Code's built-in Agent tool. Sub-agent tool calls run in the Tier-2 worker lane: anonymous, ... governed by the universal guard layers (off-limits, dangerous-bash, nudges).` |

---

## Tier 3: Hard Rules (Uncrossable)

Absolute constraints the agent cannot bypass. No marker files, no retries, no flags — the operation is blocked and cannot proceed.

**Mechanism:** Guard or command returns exit code 2. The AI tool prevents the operation entirely.

### Access Control

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H2 | Off-limits files | Any operation (read, write, search, bash) on paths matching patterns in `files-off-limits.md`. Applies to ALL callers — there is no identity or onboarding bypass (native auto-memory outside the repo is the only exemption). | `BLOCKED: Path is off-limits to all agents. Path: ... Pattern: ...` |
| H27 | Plan mode blocked | Agent uses `EnterPlanMode` or `ExitPlanMode` tool. Dydo agents must not use Claude Code's built-in plan mode — use planner role or workspace notes instead. | `BLOCKED: Dydo agents don't use Claude Code's built-in plan mode.` |

### Role Constraints (from `.role.json`)

These are defined in the `constraints` array of each role definition file, making them extensible for custom roles.

> **H10–H12 are doc-shorthand only.** The labels do not appear in source — `grep H10|H11|H12` across `*.cs` returns zero hits. They map to the constraint-type strings (`role-transition`, `requires-prior`, `panel-limit`) evaluated by `Services/RoleConstraintEvaluator.cs` against the rows currently shipped in the base `.role.json` files. Adding a new constraint of the same type to a custom role is **not** assigned a new H## — the ID names the row, not the mechanism.

| # | Name | Constraint Type | Used By | Trigger | Message |
|---|------|----------------|---------|---------|---------|
| H10 | No self-review | `role-transition` | reviewer | Agent that was `code-writer` on a task tries to become `reviewer` on the same task. Checked via `TaskRoleHistory`. | `Agent {name} was code-writer on task '{task}' and cannot be reviewer on the same task. Dispatch to a different agent for review.` |
| H11 | Orchestrator graduation | `requires-prior` | orchestrator | Agent tries to become orchestrator without having been `co-thinker` or `planner` on the same task first. | `You are a {role}. Orchestrator requires prior co-thinker or planner experience on this task.` |
| H12 | Judge panel limit | `panel-limit` | judge | More than 3 judges already active on the same task. | `Maximum 3 judges already active on task '{task}'. Escalate to the human.` |

### Release Blocking

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H13 | Unprocessed inbox | `dydo agent release` with unread inbox items. | `Cannot release: {n} unprocessed inbox item(s). Process all inbox items, then run 'dydo inbox clear'...` |
| H14 | Active wait markers | `dydo agent release` while waiting for a response. | `Cannot release: waiting for response on: {tasks}. Cancel with: dydo wait --task <name> --cancel` |
| H16 | Pending worktree merge | `dydo agent release` when a `.needs-merge` marker is present (a review passed in a worktree but the merge hasn't been dispatched yet). | `Cannot release: review passed in worktree but merge not dispatched.` |

### Bash Command Safety

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H17 | Dangerous commands | Bash command matches destructive patterns: recursive delete of root/home, fork bombs, `dd` to disk, download-and-execute (`curl\|sh`), eval of variables, history clearing, SELinux disable, firewall flush, shadow/password file access. | `BLOCKED: Dangerous command pattern detected. Reason: {reason}` |
| H18 | Chained `cd` commands | `cd /path && command` or `cd /path; command`. Breaks the guard's ability to analyze the actual command. | `BLOCKED: Don't chain cd with other commands — it breaks auto-approval for whitelisted commands.` |
| H19 | Indirect dydo invocation | `npx dydo`, `dotnet dydo`, `bash dydo`, `python dydo`, etc. Dydo should be called directly. **Severity-pinned default nudge** — implemented in `Services/ConfigFactory.cs DefaultNudges`; pattern/message editable in `dydo.json`, severity force-restored to `block` by `MergeSystemNudges` (see Extensibility section below). | `BLOCKED: Don't use '{invoker}' to run dydo — it's already on your PATH. Just use: {command}` |
| H26 | Conditional git stash block | Bash command matches `git stash` (any variant) and the agent is NOT running in a worktree. Allowed inside worktrees where the stash stack is isolated. | `BLOCKED: git stash is unsafe in multi-agent environments. Stashes are a global stack -- other agents' stash operations will interfere. Commit your changes instead.` |
| H28 | Direct `git merge` in worktree | `git merge` issued by an agent whose workspace is in a worktree or contains a `.merge-source` marker. Forces the merge through `dydo worktree merge` (which runs the safety pre-check). Regex `GitMergeRegex` in `Commands/GuardCommand.cs`. | `BLOCKED: Use dydo worktree merge to merge worktree branches. Do not use git merge directly.` |

---

## Extensibility

The guardrail system is designed for extension through role definition files (`.role.json` in `dydo/_system/roles/`).

**What's soft-coded:**
- Role constraints (`constraints` array) — H10/H11 are data-driven
  - `role-transition`: prevents role A → role B on same task
  - `requires-prior`: requires prior role experience on same task

**What's hard-coded:**
- Tool blocking (H27)
- Off-limits enforcement (H2)
- Bash safety analysis (H17, H18, H26, H28) — direct pattern checks in `Commands/GuardCommand.cs`
- Release blocking checks (H13, H14, H16)

**What's a severity-pinned default nudge** (pattern and message editable in `dydo.json`, severity force-restored to `block`):
- Indirect dydo invocation (H19) — implemented as multiple `block`-severity entries in `Services/ConfigFactory.cs:9-22 DefaultNudges` (npx, dotnet, dotnet run, bash/sh/zsh/cmd/powershell/pwsh, python). `Commands/GuardCommand.cs:587-609 MergeSystemNudges` re-merges these on every guard call: a missing pattern is re-added; a downgraded severity is force-restored to `block`. The pattern text and the message body remain user-editable.

Custom roles inherit the hard-coded enforcement automatically. Adding a new `.role.json` with custom `constraints` entries extends the system without code changes.

---

## Related

- [Guard System](../understand/guard-system.md) — How the guard hook works end-to-end
- [Roles and Permissions](../understand/roles-and-permissions.md) — The role system conceptually
- [Agent Lifecycle](../understand/agent-lifecycle.md) — Claim → role → work → release
- [Configuration Reference](./configuration.md) — Role definition file schema
- [Troubleshooting](../guides/troubleshooting.md) — What to do when you hit a guardrail
- [Decision 010](../project/decisions/010-baton-passing-and-review-enforcement.md) — Baton-passing and review enforcement rationale
