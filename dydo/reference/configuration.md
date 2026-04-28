---
area: reference
type: reference
---

# Configuration Reference

Complete reference for `dydo.json`, environment variables, hook configuration, and project customization points.

---

## dydo.json

Located at the project root. Created by `dydo init`. This is the primary configuration file.

### Schema

```json
{
  "version": 1,
  "structure": {
    "root": "dydo",
    "tasks": "project/tasks",
    "issues": "project/issues"
  },
  "paths": {
    "source": ["src/**"],
    "tests": ["tests/**"],
    "pathSets": null
  },
  "agents": {
    "pool": ["Adele", "Brian", "..."],
    "assignments": {
      "alice": ["Adele", "Brian"],
      "bob": ["Charlie"]
    }
  },
  "integrations": {
    "claude": true
  },
  "dispatch": {
    "launchInTab": true,
    "autoClose": false
  },
  "frameworkHashes": { }
}
```

### Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `version` | int | `1` | Schema version |
| `structure.root` | string | `"dydo"` | Root folder for documentation |
| `structure.tasks` | string | `"project/tasks"` | Task files location (relative to root) |
| `structure.issues` | string | `"project/issues"` | Issue files location (relative to root) |
| `paths.source` | string[] | `["src/**"]` | Source code glob patterns — used by roles via `{source}` |
| `paths.tests` | string[] | `["tests/**"]` | Test code glob patterns — used by roles via `{tests}` |
| `paths.pathSets` | dict | `null` | Custom named path groupings for role definitions |
| `agents.pool` | string[] | — | All available agent names |
| `agents.assignments` | dict | — | Maps human names to their assigned agent names |
| `integrations.claude` | bool | `true` | Whether Claude Code integration is active |
| `dispatch.launchInTab` | bool | `true` | Open dispatched agents in a new tab (vs new window) |
| `dispatch.autoClose` | bool | `false` | Auto-close terminal when agent releases |
| `frameworkHashes` | dict | — | SHA256 hashes of framework-owned files for `dydo template update` |

---

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `DYDO_HUMAN` | Yes | Identifies the human operating the terminal. Used for agent assignment, permission checks, and audit trails. |
| `DYDO_WINDOW` | No | GUID for Windows Terminal routing. Set automatically by the dispatcher to direct child dispatches to the same window. |

Set `DYDO_HUMAN` before running any commands:

```bash
# Bash/Zsh
export DYDO_HUMAN="your_name"

# PowerShell
$env:DYDO_HUMAN = "your_name"
```

---

## Hook Configuration

For Claude Code, `dydo init claude` writes `.claude/settings.local.json` automatically:

```json
{
  "permissions": {
    "allow": [
      "Bash(dydo agent claim:*)",
      "Bash(dydo agent:*)",
      "Bash(dydo dispatch:*)",
      "Bash(dydo whoami:*)",
      "..."
    ]
  },
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell",
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard"
          }
        ]
      }
    ]
  }
}
```

The `PreToolUse` hook intercepts every Edit, Write, Read, Bash, Glob, Grep, Agent, EnterPlanMode, ExitPlanMode, and PowerShell tool call. The guard receives JSON via stdin and returns exit code `0` (allow) or `2` (block).

The `permissions.allow` list pre-approves common dydo commands so the human doesn't get prompted for every agent lifecycle call.

---

## Customization Points

### Template Overrides

`dydo/_system/templates/` contains the templates used to generate agent workflow and mode files. Edit these directly to change agent behavior. Changes take effect when agents are next claimed (workspace regenerated).

Files: `agent-workflow.template.md`, `mode-code-writer.template.md`, `mode-reviewer.template.md`, etc.

### Template Additions

`dydo/_system/template-additions/` contains markdown files injected into templates via `{{include:name}}` tags. This lets you extend agent workflows without editing templates directly.

Shipped hook points:

| Tag | Template | Position |
|-----|----------|----------|
| `{{include:extra-must-reads}}` | All modes | After must-reads list |
| `{{include:extra-verify}}` | code-writer | After verify step |
| `{{include:extra-review-steps}}` | reviewer | After "Run tests" step |
| `{{include:extra-review-checklist}}` | reviewer | End of review checklist |

Custom tags: add any `{{include:whatever}}` to a template, create `whatever.md` in `template-additions/`. On `dydo template update`, user-added tags are detected, re-anchored into the updated template.

### Custom Roles

`dydo/_system/roles/` contains `.role.json` files defining role permissions. Base roles are built-in; add custom roles by creating new `.role.json` files with `"base": false`.

See [Roles and Permissions](../understand/roles-and-permissions.md) for the role definition schema.

### Off-Limits Patterns

`dydo/files-off-limits.md` defines glob patterns that block ALL agents from accessing files — secrets, credentials, system state files. A whitelist section can carve exceptions (e.g., `.env.example`).

---

## Related

- [Getting Started](../guides/getting-started.md)
- [CLI Commands Reference](./dydo-commands.md)
- [Templates and Customization](../understand/templates-and-customization.md)
- [Guard System](../understand/guard-system.md)
