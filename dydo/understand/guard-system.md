---
area: understand
type: concept
---

# Guard System

How dydo enforces agent behavior through the PreToolUse hook. Every file operation passes through `dydo guard` before execution.

---

## How the Hook Intercepts Tool Calls

The guard integrates with Claude Code through the **PreToolUse** hook event. Before every tool call (Read, Write, Edit, Bash, Glob, Grep), Claude Code pipes a JSON payload to `dydo guard` via stdin:

```json
{
  "session_id": "abc123",
  "tool_name": "write",
  "tool_input": {
    "file_path": "src/foo.cs",
    "content": "..."
  },
  "hook_event_name": "PreToolUse"
}
```

The guard evaluates the request and returns:
- **Exit 0** — action allowed (tool proceeds)
- **Exit 2** — action blocked (error message sent to stderr, tool fails)

---

## Staged Onboarding Enforcement

The guard enforces a progressive unlock. An agent cannot skip stages — each gate must be passed in order.

**Stage 0 (No Identity):** Only bootstrap files are readable (`dydo/index.md`, agent workflows, root-level files). All writes and search tools blocked.

**Stage 1 (Claimed, No Role):** Adds own mode files to readable set (`dydo/agents/{self}/modes/*.md`). Writes and search tools still blocked.

**Stage 2 (Claimed + Role):** All reads allowed. Writes permitted to role's `WritablePaths`, but only after all `must-read: true` files have been read. Search tools (Glob, Grep, Agent) unlocked.

See [Agent Lifecycle](./agent-lifecycle.md) for the full stage progression.

---

## Role-Based Permission Checking

When an agent attempts a write operation, the guard checks the file path against the role's permission set:

1. **Off-limits check first** — if the path matches a global off-limits pattern, the operation is blocked regardless of role.
2. **WritablePaths match** — if the path matches any pattern in the role's `WritablePaths`, the write is allowed.
3. **Denial** — if no pattern matches, the write is blocked. The role's `denialHint` is appended to the error message to guide the agent.

Path patterns use glob syntax (`**` for any path, `*` within a segment) and support tokens like `{self}`, `{source}`, and `{tests}` that are expanded at role assignment time.

---

## Off-Limits File Enforcement

Global off-limits patterns are defined in `dydo/files-off-limits.md`. These apply to **all** agents regardless of role.

**Protected categories include:**
- DynaDocs system files (workflow files, mode files, state files, `index.md`)
- Secrets and credentials (`.env*`, `*.pem`, `*.key`, `secrets.json`, database configs)

**Whitelist exceptions:** The file supports a `## Whitelist` section where patterns can override off-limits rules (e.g., `.env.example`).

**Bootstrap bypass:** Files needed for onboarding (bootstrap files, mode files) bypass off-limits checks based on the agent's current stage.

---

## Bash Command Analysis

Bash commands go through multi-stage analysis:

**1. Dangerous pattern detection** (immediate block): Recursive root deletes, fork bombs, direct disk writes (`dd`), download-and-execute (`curl | sh`), eval of untrusted input, history clearing, security disables.

**2. Bypass detection** (warnings, not blocks): Command substitution (`$(...)`), base64/hex decode, variable expansion, embedded newlines — flagged because they may obscure the actual file paths being operated on.

**3. File operation extraction**: Commands are tokenized and categorized — reads (`cat`, `grep`), writes (`tee`, `>`, `>>`), deletes (`rm`), copies/moves (`cp`, `mv`), and permission changes (`chmod`). `sed -i` is classified as a write.

**4. File operation validation**: Each extracted file operation is checked individually — off-limits patterns are enforced for all operations, staged access control (read gating by onboarding stage) is enforced for reads, and RBAC (role permission matching) is enforced for writes, deletes, moves, copies, and permission changes. The same rules apply as for direct tool calls, but the mechanism differs: bash commands are first split and each file operation is checked separately, whereas direct tool calls check the path in the tool input directly.

**Special blocks:**
- Chained `cd` (`cd /path && command`) — breaks path analysis; run `cd` separately
- Indirect dydo invocation (`npx dydo`, `dotnet dydo`) — use `dydo` directly
- `dydo wait` without `run_in_background: true` — would block the terminal
- `git stash` outside worktrees — global stash interferes in multi-agent setups

---

## Three-Tier Guardrail System

All guardrails fall into three tiers:

**Nudges (N-tier):** Exit 0, action allowed but guidance injected. Examples: release hints when inbox is empty, bash command warnings about variable expansion, role-specific denial hints.

**Soft-Blocks (S-tier):** Exit 2 on first encounter; a marker file is created so the same check passes on retry. Examples: role mismatch warning on dispatch, `--no-launch` confirmation, unread messages blocking work, pending wait registration.

**Hard Rules (H-tier):** Exit 2, no override, no retry. Categories include:
- **Access control** (H1–H6): Role permissions, off-limits, staged reads, must-read enforcement, search tool lockout
- **Onboarding** (H7–H9): No identity/role blocks writes, session ID required
- **Role constraints** (H10–H12): No self-review, orchestrator graduation, judge panel limit
- **Release blocking** (H13–H16, H25): Unprocessed inbox, active waits, pending replies, worktree merges, code-writer review enforcement
- **Bash safety** (H17–H20, H26): Dangerous commands, chained cd, indirect dydo, foreground wait, git stash
- **Messaging** (H21–H22): No self-messaging, no cross-human messaging
- **Dispatch** (H23–H24): Double-dispatch protection, conflicting launch flags

See [Guardrails Reference](../reference/guardrails.md) for the full catalog.

---

## Guard Exit Codes and Error Messages

| Exit Code | Meaning |
|-----------|---------|
| 0 | Action allowed (nothing printed to stdout) |
| 2 | Action blocked (error message to stderr) |

Error messages follow a consistent format:

```
BLOCKED: <reason>
  <details>
  <guidance for recovery>
```

Examples:
- `BLOCKED: Path is off-limits to all agents.` — with the matched pattern and path
- `BLOCKED: Agent Brian (code-writer) cannot edit src/test.cs.` — with the role's denial hint
- `BLOCKED: You have not read the required files for the code-writer mode:` — listing unread must-reads
- `BLOCKED: Dangerous command pattern detected.` — with reason and the flagged command

---

## Integration for Other AI Tools

Any AI coding tool can integrate with dydo's guard system through two input modes:

**Stdin JSON (preferred for hooks):** Pipe a JSON object with `session_id`, `tool_name`, `tool_input`, and `hook_event_name` to `dydo guard` via stdin. The tool input schema varies by tool type (`file_path` for file tools, `command` for bash, `path` for search tools).

**CLI arguments (for testing):** Pass `--action {edit|write|delete|read}`, `--path <path>`, and `--command <command>` directly. Useful for development and integration testing.

The return contract is the same for both modes: exit 0 allows the action, exit 2 blocks it with an error message on stderr.

---

## Related

- [Guardrails Reference](../reference/guardrails.md) — Full catalog of nudges, soft-blocks, and hard rules
- [Agent Lifecycle](./agent-lifecycle.md)
- [Roles and Permissions](./roles-and-permissions.md)
