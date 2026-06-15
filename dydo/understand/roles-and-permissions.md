---
area: understand
type: concept
---

# Roles and Permissions

The role system: what roles are, how they define agent capabilities, and how permissions are enforced.

---

## What a Role Is

A role is the combination of three things:

1. **Mode file** — A markdown template (e.g., `mode-code-writer.template.md`) describing the role's responsibilities, must-reads, workflow, and completion steps.
2. **Permission set** — File paths the agent can read and write, defined in `.role.json`.
3. **Behavioral constraints** — Data-driven rules governing role transitions, prerequisites, and panel limits.

When an agent sets a role with `dydo agent role <role> --task <task>`, all three components are loaded and enforced by the guard.

---

## Base Roles

Nine built-in roles ship with dydo, marked `"base": true` in their definitions:

| Role | Purpose | Key Permissions |
|------|---------|-----------------|
| **code-writer** | Implements features and fixes bugs | Source code, tests, own workspace |
| **reviewer** | Reviews code for quality and correctness | Source code (read-only), own workspace |
| **co-thinker** | Collaborates on design decisions | Documentation, own workspace |
| **planner** | Creates implementation plans and task breakdowns | Documentation, own workspace |
| **docs-writer** | Creates and maintains documentation | Documentation tree, own workspace |
| **test-writer** | Writes and maintains test suites | Tests, own workspace |
| **orchestrator** | Coordinates multi-agent workflows | Documentation, own workspace |
| **inquisitor** | Conducts documentation and knowledge audits | Documentation, own workspace |
| **judge** | Evaluates inquisition reports and arbitrates disputes | Documentation, own workspace |

Base roles are immutable — they cannot be deleted or renamed, and `dydo roles reset` regenerates them from defaults.

---

## Custom Roles

Projects can define additional roles:

```bash
dydo roles create <name>
```

This scaffolds a minimal `.role.json` in `dydo/_system/roles/` and suggests next steps: set the description, writable paths, denial hint, and optionally create a mode template. Custom roles are marked `"base": false` and participate in all the same guard checks as base roles.

---

## Role Definition Schema

Role definitions live at `dydo/_system/roles/<name>.role.json`:

```json
{
  "name": "code-writer",
  "description": "Implements features and fixes bugs in source code.",
  "base": true,
  "writablePaths": ["{source}", "{tests}", "dydo/agents/{self}/**"],
  "readOnlyPaths": ["dydo/**"],
  "templateFile": "mode-code-writer.template.md",
  "denialHint": "Code-writer role can only edit configured source/test paths and own workspace.",
  "canOrchestrate": false,
  "constraints": [...],
  "conditionalMustReads": []
}
```

The full schema is `Models/RoleDefinition.cs`. For a complete real example, see `dydo/_system/roles/orchestrator.role.json` — note `canOrchestrate: true` (line 15) and `conditionalMustReads: []` (line 31).

**Field notes:**
- `canOrchestrate` (boolean) — marks oversight roles (`orchestrator`, `inquisitor`, `judge`); all other base roles set it to `false`. It no longer gates any dispatch flag — dispatch is uniform across roles.
- `conditionalMustReads` — reserved for future use (currently `[]` for all base roles). Per [decision 013](../project/decisions/013-conditional-must-reads.md), the two existing conditional must-reads (merge code-writer, merge reviewer) are hard-coded in `MustReadTracker`; this field is the soft-coding path if a third case emerges.

**Path tokens** are expanded at role assignment time:
- `{self}` — the agent's name (e.g., `dydo/agents/Brian/**`)
- `{source}` — configured source directories from `dydo.json`
- `{tests}` — configured test directories from `dydo.json`

**Glob patterns:**
- `**/` — optional directory prefix (zero or more leading directories)
- `**` — any path depth (matches across `/`)
- `*` — any characters within a single segment (does not cross `/`)
- `?` — a single character

These four are the full set supported by `Utils/GlobMatcher.cs`. Matching is case-insensitive.

---

## How Permissions Map to File Paths

The guard pipeline checks off-limits patterns and role permissions as separate stages. Off-limits checking happens first in the guard pipeline (see [Guard System](./guard-system.md)). The role permission check (`IsPathAllowed`) then resolves in this order:

1. **No-role check** — Agent must have a role set, otherwise block.
2. **ReadOnlyPaths check** — If the path matches a `ReadOnlyPaths` pattern and is NOT also in `WritablePaths`, block. This is how roles like reviewer get read-only access to source code — the path is in ReadOnlyPaths but not WritablePaths.
3. **Empty-writable check** — If the role has no `WritablePaths` defined at all, block.
4. **WritablePaths match** — Does the path match any pattern in the role's `WritablePaths`? If yes, allow. If no match, block with the role's `denialHint` appended to the error.

Path matching converts glob patterns to regex (`**/` → `(.*/)?`, `**` → `.*`, `*` → `[^/]*`, `?` → `.`) and performs case-insensitive comparison.

---

## Role Transitions and Restrictions

Roles define **constraints** — data-driven rules evaluated when an agent attempts to take a role:

### No Self-Review (H10)

The reviewer role has a `role-transition` constraint blocking agents that were previously `code-writer` on the same task. Checked against `TaskRoleHistory`.

### Orchestrator Graduation (H11)

The orchestrator role has a `requires-prior` constraint: the agent must have been `co-thinker` or `planner` on the same task first. This ensures orchestrators have project context before coordinating work.

### Judge Panel Limit (H12)

The judge role has a `panel-limit` constraint: maximum 3 judges can be active on the same task simultaneously. Beyond that, escalate to the human.

### Constraint Types

| Type | Checks | Evaluated In | Example |
|------|--------|-------------|---------|
| `role-transition` | Agent held `fromRole` on same task | `CanTakeRole` | code-writer → reviewer blocked |
| `requires-prior` | Agent held one of `requiredRoles` on same task | `CanTakeRole` | orchestrator requires co-thinker or planner |
| `panel-limit` | Fewer than `maxCount` agents in role on task | `CanTakeRole` | max 3 judges |

Constraints are data-driven — adding new constraints to a `.role.json` does not require code changes.

> **Note:** The dispatch-runtime constraint types `requires-dispatch` (release gate) and `dispatch-restriction` (dispatch gate) are no longer enforced. They may still appear in role JSON for documentation, but the runtime no longer blocks release or dispatch on them — dispatch is uniform across roles.

---

## How the Guard Resolves Permissions at Runtime

The guard processes every tool call through this pipeline:

1. **Session lookup** — Find the agent bound to this session ID
2. **Staged access** — Check onboarding stage (no identity → no role → full working)
3. **Off-limits** — Check global patterns (bypass for bootstrap/mode files)
4. **Bash analysis** — For bash tools: dangerous patterns, file operation extraction, per-operation permission checks
5. **Must-read enforcement** — For writes: verify all `must-read: true` files have been read
6. **Role permissions** — For writes: check path against `WritablePaths`
7. **Audit logging** — Log the action (allowed or blocked) for the audit trail

---

## Role History Tracking

`TaskRoleHistory` is a per-agent, per-task dictionary mapping task names to lists of roles held:

```
{"auth-login": ["planner", "orchestrator"], "db-schema": ["code-writer"]}
```

This history **persists across agent releases and reclaims**. It is used for:
- **Self-review prevention** — An agent that was code-writer on a task cannot become reviewer on that same task, even in a later session
- **Orchestrator graduation** — Verifying the agent has prior co-thinker or planner experience on the task

The history is stored in the agent's `state.md` file and survives workspace archival.

---

## Related

- [Guard System](./guard-system.md)
- [Agent Lifecycle](./agent-lifecycle.md)
- [Guardrails Reference](../reference/guardrails.md) — Full catalog of all guardrail rules

