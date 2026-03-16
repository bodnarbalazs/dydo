---
area: guides
type: guide
---

# Customizing Roles

How to create custom roles, modify permission sets, and extend the role system.

---

## When to create a custom role

Base roles (code-writer, reviewer, planner, etc.) cover most workflows. Create a custom role when:

- You need a different set of writable/read-only paths
- You want role-specific constraints (e.g., requiring prior experience)
- A recurring workflow doesn't map cleanly to any base role

**Don't create a role** for a one-off task — just use the closest base role.

---

## Creating a custom role

```bash
dydo roles create my-role
```

This scaffolds a new `.role.json` file in `dydo/_system/roles/`:

```
dydo/_system/roles/my-role.role.json
```

---

## The .role.json schema

A role definition file has this structure:

```json
{
  "name": "my-role",
  "description": "What this role does.",
  "base": false,
  "writablePaths": [
    "dydo/agents/{self}/**",
    "infrastructure/**"
  ],
  "readOnlyPaths": [
    "dydo/**"
  ],
  "templateFile": "mode-my-role.template.md",
  "denialHint": "my-role can only edit infrastructure files and own workspace.",
  "constraints": []
}
```

### Fields

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Role identifier (kebab-case, matches filename) |
| `description` | Yes | One-line description shown in `dydo roles list` |
| `base` | Yes | `true` for built-in roles, `false` for custom |
| `writablePaths` | Yes | Glob patterns the role can write to |
| `readOnlyPaths` | Yes | Glob patterns the role can read (beyond defaults) |
| `templateFile` | No | Template for the mode file (in `_system/templates/`) |
| `denialHint` | No | Message shown when a write is blocked (nudge N8) |
| `constraints` | No | Array of constraint objects (see below) |

### Path variables

Use these placeholders in `writablePaths` and `readOnlyPaths`:

| Variable | Resolves to |
|----------|-------------|
| `{self}` | Current agent name |
| `{source}` | Source code paths from `dydo.json` |
| `{tests}` | Test paths from `dydo.json` |

---

## Constraints

Constraints add behavioral rules enforced by the guard. Three types are supported:

### role-transition

Prevents an agent from switching to this role if it previously held a specific role on the same task.

```json
{
  "type": "role-transition",
  "fromRole": "code-writer",
  "message": "Agent {agent} was code-writer on task '{task}' and cannot review its own code."
}
```

**Use case:** The reviewer role uses this to prevent self-review.

### requires-prior

Requires the agent to have held one of the specified roles on the same task before assuming this role.

```json
{
  "type": "requires-prior",
  "requiredRoles": ["co-thinker", "planner"],
  "message": "Orchestrator requires prior planning experience on this task."
}
```

**Use case:** The orchestrator role requires prior co-thinker or planner experience.

### panel-limit

Caps the number of agents that can hold this role concurrently on the same task.

```json
{
  "type": "panel-limit",
  "maxCount": 3,
  "message": "Maximum 3 judges active on task '{task}'. Escalate to the human."
}
```

**Use case:** The judge role limits the panel size to 3.

---

## Managing roles

```bash
dydo roles list              # List all roles (base + custom)
dydo roles reset             # Regenerate base role files only
dydo roles reset --all       # Remove all roles (including custom) and regenerate base
```

**Warning:** `dydo roles reset --all` deletes custom role files. Back them up first.

---

## Examples

### DBA role

A role for database migration work:

```json
{
  "name": "dba",
  "description": "Manages database schemas and migrations.",
  "base": false,
  "writablePaths": [
    "dydo/agents/{self}/**",
    "migrations/**",
    "database/**"
  ],
  "readOnlyPaths": [
    "{source}",
    "{tests}"
  ],
  "denialHint": "DBA role can only edit migration and database files.",
  "constraints": []
}
```

### DevOps role

A role for infrastructure and CI/CD:

```json
{
  "name": "devops",
  "description": "Manages infrastructure, CI/CD, and deployment configuration.",
  "base": false,
  "writablePaths": [
    "dydo/agents/{self}/**",
    ".github/**",
    "infrastructure/**",
    "docker/**",
    "Dockerfile",
    "docker-compose*.yml"
  ],
  "readOnlyPaths": [
    "{source}",
    "{tests}"
  ],
  "denialHint": "DevOps role can only edit infrastructure, CI/CD, and Docker files.",
  "constraints": []
}
```

---

## Mode files for custom roles

If you specify a `templateFile`, create the corresponding template in `dydo/_system/templates/`. The template is used to generate the mode file in each agent's workspace when they claim an identity.

If you omit `templateFile`, agents assigned to the role won't have a mode file with role-specific guidance — they'll rely on the workflow file and the role's path permissions alone.

---

## Related

- [Roles and Permissions](../understand/roles-and-permissions.md) — The role system conceptually
- [Role Reference Pages](../reference/roles/_index.md) — Per-role reference docs
- [CLI Commands Reference](../reference/dydo-commands.md) — Full command documentation
