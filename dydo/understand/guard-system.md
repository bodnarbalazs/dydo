---
area: understand
type: concept
---

# Guard System

How dydo enforces boundaries through the PreToolUse hook. Every tool call — reads, writes, searches, bash, in the main thread and inside every subagent and workflow — passes through `dydo guard` before execution. Three layers: off-limits paths, dangerous-bash detection, and nudges.

---

## How the Hook Intercepts Tool Calls

The guard integrates with the platform through the **PreToolUse** hook event. Before every tool call, the platform pipes a JSON payload to `dydo guard` via stdin:

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

- **Exit 0** — action allowed (a `NOTICE:` on stderr may ride along)
- **Exit 2** — action blocked (`BLOCKED: <reason>` on stderr, tool fails)

There is no identity, no staging, no per-role permission matrix: the same rules apply to every caller, every time ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)).

---

## Layer 1: Off-Limits Paths

Global patterns in `dydo/files-off-limits.md` hard-block **every** operation — read, write, search, or bash — for all callers.

- **Protected categories:** secrets and credentials (`.env*`, `*.pem`, `*.key`, `secrets.json`, database configs) and dydo system files.
- **Whitelist:** a `## Whitelist` section carves exceptions (e.g. `.env.example`).
- **Patterns** use glob syntax: `**/` for optional directory prefix, `**` for any path, `*` within a segment, `?` for a single character.
- The only exemption is the platform's native auto-memory directory outside the repo.

```
BLOCKED: Path is off-limits to all agents.
  Path: .env
  Pattern: **/.env*
  Configure exceptions in dydo/files-off-limits.md
```

---

## Layer 2: Bash Command Analysis

Bash commands get deeper treatment than direct tool calls:

1. **Dangerous pattern detection** (immediate block): recursive root/home deletes, fork bombs, direct disk writes (`dd`), download-and-execute (`curl | sh`), eval of untrusted input, history clearing, security disables.
2. **Bypass detection** (warnings, not blocks): command substitution (`$(...)`), base64/hex decode, variable expansion, embedded newlines — flagged because they can obscure the paths actually being touched.
3. **File operation extraction**: the command is tokenized into reads (`cat`, `grep`), writes (`tee`, `>`, `>>`, `sed -i`), deletes (`rm`), copies/moves (`cp`, `mv`), and permission changes (`chmod`) — and each extracted path is checked against off-limits individually. A chain can't smuggle a protected path past the guard.
4. **Chained `cd` block**: `cd /path && command` breaks path analysis — run `cd` separately or use absolute paths.

The guard fires on `dydo` commands themselves too — nudges and off-limits apply to dydo's own CLI like anything else.

---

## Layer 3: Nudges

Nudges are project-configurable rules in `dydo.json`: a pattern plus a message, at one of three severities.

| Severity | Behavior |
|----------|----------|
| `notice` | `NOTICE:` on stderr, never blocks (exit 0) |
| `warn` | Blocks once with "(Run the same command again to proceed anyway.)"; the retry passes. The pass-through marker lives in `dydo/_system/.local/` (gitignored), keyed by pattern hash. |
| `block` | Always blocks |

Two kinds:

- **Command nudges** — regex matched against bash command text. Capture groups substitute into the message (`$1`, `$2`, …).
- **File nudges** (`tools` key) — glob patterns matched against direct tool-call paths; `{source}` and `{tests}` expand to the path sets in `dydo.json`. The shipped example is the Tier-1 source-write reminder ([Decision 026](../project/decisions/026-tier1-managers-doctrine.md)): a `notice` that reminds managers to route implementation through worker skills without ever blocking the trivial-edit exception.

**Shipped defaults and self-healing:** the indirect-dydo-invocation nudges (`npx dydo`, `dotnet dydo`, `python dydo`, …) are severity-pinned — `MergeSystemNudges` reconciles config against the shipped set on every guard call: a deleted block-default is re-added, a downgraded severity is restored to `block`, and a nudge still carrying a known-stale shipped message is healed to the current text or dropped if its default was retired. A message the user customized matches no known-stale text and is never clobbered.

---

## Also Enforced

- **Plan-mode block**: `EnterPlanMode`/`ExitPlanMode` are blocked — planning happens through the planner skill and plan records, not the platform's plan mode.
- **Agent-tool notice**: invoking the platform's built-in `Agent` tool passes with a stderr reminder that sub-agent calls run anonymous and governed by the same three layers.

## Housekeeping Rides Along

Because the guard runs on every tool call, it carries two throttled maintenance jobs: a **daily validation** (config checks, report-only, never blocks) and **model-cap auto-restore** (expired `dydo model cap` fallbacks are lifted without human intervention).

---

## Integration for Other AI Tools

Any coding tool can integrate through two input modes with the same contract (exit 0 allows, exit 2 blocks with stderr message):

- **Stdin JSON** (preferred for hooks) — the payload shown above; `file_path` for file tools, `command` for bash, `path` for search tools.
- **CLI arguments** (for testing) — `--action {edit|write|delete|read}`, `--path <path>`, `--command <command>`.

---

## Related

- [Configuration Reference](../reference/configuration.md) — nudge format, off-limits, path sets
- [Architecture Overview](./architecture.md) — where the guard sits in the system
- [Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) — why identity-gated enforcement left the guard
