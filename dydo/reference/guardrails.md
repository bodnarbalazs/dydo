---
area: reference
type: reference
---

# Guardrails

The three-tier system that shapes agent behavior. Every guardrail falls into one of three categories based on how strictly it constrains the agent.

---

## Tier 1: Nudges (Contextual Injection)

Passive guidance injected into command output when relevant. The agent is not blocked — it receives additional context to make a better decision. If the situation doesn't apply, the nudge is not shown.

**Mechanism:** Extra text appended to stdout/stderr of a command that succeeds (exit code 0). The operation proceeds regardless.

### Instances

| # | Name | Trigger | Message |
|---|------|---------|---------|
| N1 | Release hint | Agent dispatches work and has no remaining inbox items or wait markers. Skipped for co-thinkers (they typically stay active). | `Nothing left? Don't forget: dydo agent release` |
| N2 | Bash command warnings | Bash command contains suspicious-but-not-dangerous patterns: command substitution, base64/hex decode, variable expansion, embedded newlines. | `WARNING: Command contains base64 decode - potential obfuscation` (etc.) |
| N3 | Daily validation | First guard call per 24-hour period runs a background validation of config/roles. Issues are reported but never block. | `Daily validation found issues: [...] Run 'dydo validate' for full report.` |
| N4 | Task name sanitization | Task name contains characters unsafe for filenames. Dispatch proceeds with sanitized name. | `Warning: Task name sanitized for filesystem safety. Original: "..." Filename: "..."` |
| N5 | Worktree inheritance | `--worktree` flag specified but parent already runs in a worktree. Flag is silently ignored, warning emitted. | `Warning: --worktree ignored — inheriting parent's worktree instead.` |
| N6 | Dispatch summary | After every successful dispatch. Informational — confirms what was dispatched and to whom. | `Work dispatched to agent {name}. Role: ... Task: ... Inbox: ...` |
| N7 | Path-specific denial hint | When a write is denied AND the target path is a known "wrong destination" (currently: `.claude/plans/`). Appended to the denial message to redirect the agent. | `Dydo agents don't use Claude Code's built-in plans. Switch to planner mode...` |
| N8 | Role denial hint | When a write is denied, the role's `denialHint` from `.role.json` is appended. Generic guidance about what the role can edit. | `Code-writer role can only edit configured source/test paths and own workspace.` |

**Note:** N7 and N8 fire as part of a hard rule (the write is denied), but the hint text itself is the nudge — it steers the agent toward the right action rather than just saying "no." The denial is the hard rule; the hint is the nudge riding on top of it.

---

## Tier 2: Soft-Blocks (One-Time Stop)

A one-time blocking message that forces the agent to acknowledge before proceeding. The agent CAN override by retrying — the block catches mistakes, not deliberate choices.

**Mechanism:** Command returns exit code 2 on first attempt and writes a marker file. On second attempt, the marker is found, deleted, and the command succeeds. Think of it as a "are you sure?" checkpoint.

### Instances

| # | Name | Trigger | Marker File | Message |
|---|------|---------|-------------|---------|
| S1 | Role mismatch | Agent sets a different role than what the inbox dispatch specified. Skipped if agent already fulfilled the dispatched role and is intentionally switching. | `.role-nudge-{task}` | `You were dispatched as '{role}' for this task. If '{newRole}' fits better, run the command again.` |
| S2 | No-launch dispatch | Agent uses `--no-launch` flag (which means the target agent won't be activated automatically). | `.no-launch-nudge-{task}` | `You dispatched with the --no-launch flag... Unless the user was explicit about using no-launch... you shouldn't use this flag. If you insist you may run it again and it will pass.` |
| S3 | Unread message delivery | Any tool call when agent has unread inbox messages. Hard-blocks every operation until messages are read and cleared — this is a delivery mechanism, not a one-time check. Persists until inbox is empty. | *(inbox files themselves)* | `NOTICE: You have {n} unread message(s). [...] Read your message(s) to continue. After reading, retry your previous action — it will succeed.` |
| S4 | Pending wait registration | Any operation when agent has wait markers that aren't actively listening (e.g., dispatched `--wait` but hasn't run `dydo wait`). Self-heals dead listener PIDs. | `.waiting/{task}.json` | `BLOCKED: Register waits before continuing. Pending: [{tasks}]. Run: dydo wait --task <name> (in background)` |
| S5 | Inactive agent messaging | `dydo msg --to <agent>` where target agent is not currently active. Almost always a mistake — the message will sit unread until the agent is manually claimed. Override with `--force` for cases where the message needs to be persisted regardless. | *(none — uses --force flag)* | `Agent {name} is not currently active. The message will sit unread... Send anyway with: dydo msg --to {name} --body "..." --force` |

---

## Tier 3: Hard Rules (Uncrossable)

Absolute constraints the agent cannot bypass. No marker files, no retries, no flags — the operation is blocked and cannot proceed.

**Mechanism:** Guard or command returns exit code 2. The AI tool prevents the operation entirely.

### Access Control

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H1 | Role-based write permissions | Agent attempts to write a file outside its role's `writablePaths`. | `Agent {name} ({role}) cannot {action} {path}. {denialHint}` |
| H2 | Off-limits files | Any operation on paths matching patterns in `files-off-limits.md`. Checked before role permissions — applies to ALL agents regardless of role. | `BLOCKED: Path is off-limits to all agents. Path: ... Pattern: ...` |
| H3 | Staged read access | Read operations are gated by onboarding stage. Stage 0 (no identity): only bootstrap files. Stage 1 (claimed, no role): + own mode files. Stage 2 (role set): all reads. | `BLOCKED: Read access denied. No agent identity assigned...` |
| H4 | Other agent workflow | After setting a role, agent cannot read another agent's `workflow.md`. Prevents cross-contamination of agent instructions. | *(access denied)* |
| H5 | Must-read enforcement | Write operations blocked until all files marked `must-read: true` in the role's mode file have been read. | `BLOCKED: You have not read the required files for the {role} mode: - {file1} - {file2}` |
| H6 | Search tool lockout | Glob, Grep, and Agent tools require both identity AND role (Stage 2). Prevents broad codebase scanning before onboarding. | `BLOCKED: Read access denied...` |

### Onboarding & Identity

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H7 | No identity — writes | Any write operation without a claimed agent identity. | `BLOCKED: No agent identity assigned to this process. Run 'dydo agent claim auto'...` |
| H8 | No role — writes | Write operation with identity but no role set. | `BLOCKED: Agent {name} has no role set. 1. Read your mode file first... 2. Then set your role...` |
| H9 | No session ID | Guard hook invoked without session ID in input. | `BLOCKED: No session_id in hook input.` |

### Role Constraints (from `.role.json`)

These are defined in the `constraints` array of each role definition file, making them extensible for custom roles.

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
| H15 | Pending reply obligation | `dydo agent release` when agent was dispatched with `--wait` and hasn't sent a message back to the dispatching agent. | `Cannot release: pending reply on: '{task}' to {agent}. Send a message first: dydo msg --to <agent> --subject <task> --body "..."` |
| H16 | Pending worktree merge | `dydo agent release` when a review passed in a worktree but the merge hasn't been dispatched yet. | `Cannot release: review passed in worktree but merge not dispatched.` |

### Bash Command Safety

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H17 | Dangerous commands | Bash command matches destructive patterns: recursive delete of root/home, fork bombs, `dd` to disk, download-and-execute (`curl\|sh`), eval of variables, history clearing, SELinux disable, firewall flush, shadow/password file access. | `BLOCKED: Dangerous command pattern detected. Reason: {reason}` |
| H18 | Chained `cd` commands | `cd /path && command` or `cd /path; command`. Breaks the guard's ability to analyze the actual command. | `BLOCKED: Don't chain cd with other commands — it breaks auto-approval for whitelisted commands.` |
| H19 | Indirect dydo invocation | `npx dydo`, `dotnet dydo`, `bash dydo`, etc. Dydo should be called directly. | `BLOCKED: Don't use '{invoker}' to run dydo — it's already on your PATH. Just use: {command}` |
| H20 | `dydo wait` in foreground | `dydo wait` executed without `run_in_background: true`. Would block the agent's main thread. | `BLOCKED: 'dydo wait' must run in background.` |

### Messaging

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H21 | No self-messaging | `dydo msg --to <self>`. | `Cannot send a message to yourself.` |
| H22 | Cross-human messaging | `dydo msg --to <agent>` where the target agent belongs to a different human. | `Agent '{name}' is not assigned to you (assigned to: {human}).` |

### Dispatch

| # | Name | Trigger | Message |
|---|------|---------|---------|
| H23 | Double-dispatch | Dispatching to a task that another agent is already working on. | `{agent} is already working on task '{task}'. If you need to re-dispatch, have them release first.` |
| H24 | Conflicting launch flags | Both `--tab` and `--new-window` specified. | `Cannot specify both --tab and --new-window.` |

---

## Extensibility

The guardrail system is designed for extension through role definition files (`.role.json` in `dydo/_system/roles/`).

**What's soft-coded:**
- Write permissions (`writablePaths`, `readOnlyPaths`)
- Denial hints (`denialHint`) — the N8 nudge text
- Role constraints (`constraints` array) — H10/H11/H12 are all data-driven
  - `role-transition`: prevents role A → role B on same task
  - `requires-prior`: requires prior role experience on same task
  - `panel-limit`: caps concurrent agents in a role per task

**What's hard-coded:**
- Staged access control (H3, H6)
- Off-limits enforcement (H2)
- Bash safety analysis (H17–H20)
- Release blocking checks (H13–H16)
- Messaging restrictions (H21–H22)
- Soft-block marker file logic (S1–S4)

Custom roles inherit the hard-coded enforcement automatically. Adding a new `.role.json` with custom `constraints` entries extends the system without code changes.

---

## Related

- [Guard System](../understand/guard-system.md) — How the guard hook works end-to-end
- [Roles and Permissions](../understand/roles-and-permissions.md) — The role system conceptually
- [Agent Lifecycle](../understand/agent-lifecycle.md) — Claim → role → work → release
- [Configuration Reference](./configuration.md) — Role definition file schema
- [Troubleshooting](../guides/troubleshooting.md) — What to do when you hit a guardrail
