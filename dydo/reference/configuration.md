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
    "claude": true,
    "codex": false
  },
  "dispatch": {
    "launchInTab": true,
    "autoClose": false,
    "codex": {
      "sandbox": "workspace-write",
      "approvalPolicy": "on-request"
    }
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
| `integrations.codex` | bool | `false` | Whether Codex integration is active |
| `dispatch.launchInTab` | bool | `true` | Open dispatched agents in a new tab (vs new window) |
| `dispatch.autoClose` | bool | `false` | Auto-close terminal when agent releases |
| `dispatch.codex.sandbox` | string | `"workspace-write"` | Codex sandbox mode — `read-only`, `workspace-write`, or `danger-full-access` |
| `dispatch.codex.approvalPolicy` | string | `"on-request"` | Codex approval prompting — `untrusted`, `on-request`, or `never` |
| `frameworkHashes` | dict | — | SHA256 hashes of framework-owned files for `dydo template update` |

---

## Codex Launch Posture

Every dispatched Codex session launches with a configured approval-and-sandbox posture
(issue 0253) so it can run shell commands and `dydo` CLI calls inside its workspace without a
human hand-approving each action — the sandbox is the enforcement boundary, and Codex prompts
only when an action would exceed it. The dydo guard hook remains the project-boundary
defense-in-depth on top of this.

The `dispatch.codex` section surfaces two keys, both validated against the Codex CLI's accepted
values:

| Key | Accepted values | Default | Emitted flag |
|-----|-----------------|---------|--------------|
| `sandbox` | `read-only`, `workspace-write`, `danger-full-access` | `workspace-write` | `--sandbox` |
| `approvalPolicy` | `untrusted`, `on-request`, `never` | `on-request` | `--ask-for-approval` |

An absent `dispatch.codex` section resolves to these shipped defaults — never a bare, maximally
restrictive launch. An unknown value fails validation, naming the accepted list. The Codex
`on-failure` approval value is deprecated and never emitted.

The dangerous-bypass flag (`--dangerously-bypass-approvals-and-sandbox`, alias `--yolo`) is
**not a configuration value and is never emitted** under any posture.

The same posture is carried on the watchdog's resume path. `codex resume` accepts the same global
flags as `codex`, so the posture flags are emitted **before** the `resume` subcommand
(`codex --sandbox … --ask-for-approval … resume <id>`) — the documented form.

### Windows sandbox prerequisite

On Windows the `workspace-write` posture runs under Codex's elevated sandbox, which must be
provisioned per machine before the first dispatch. The provisioning tool
`codex-windows-sandbox-setup.exe` ships with the Codex CLI, installed under
`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin`. It sets up the sandbox users, ACLs, and firewall
rules the elevated sandbox needs, and its first run must be approved by an administrator. Run it
once from an elevated PowerShell before dispatching Codex agents:

```powershell
& "$env:LOCALAPPDATA\Programs\OpenAI\Codex\bin\codex-windows-sandbox-setup.exe"
```

If the helper reports "program not found" on a clean install, that matches a known upstream Codex
bug in the bin-junction lookup ([openai/codex issue #30829](https://github.com/openai/codex/issues/30829)):
re-run the sandbox setup, or verify the bin junction under
`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin` exists and resolves. Do not work around a missing helper
by lowering the posture to `danger-full-access` — provision the sandbox instead.

The dispatch-time preflight (`Services/DispatchPreflight.cs`) surfaces an absent prerequisite as an
actionable error before any agent is reserved: a `workspace-write` Codex dispatch on Windows fails
fast naming `codex-windows-sandbox-setup.exe` and pointing back at this section, and the sandbox is
never silently downgraded. Modes that do not use the elevated sandbox (`read-only`,
`danger-full-access`) skip the check.

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

`dydo init claude` and `dydo init codex` wire the selected runtime's guard hooks automatically. Claude Code stores its hook configuration in `.claude/settings.local.json`; Codex stores it in `.codex/hooks.json`.

Claude Code example:

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
        "matcher": "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit|AskUserQuestion",
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard"
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard --stop"
          }
        ]
      }
    ]
  }
}
```

Codex example:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit|AskUserQuestion|apply_patch",
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard"
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard --stop"
          }
        ]
      }
    ]
  }
}
```

The `PreToolUse` hook intercepts each matched tool call and sends it to `dydo guard`; Codex additionally includes `apply_patch` because Codex exposes file edits through that tool. The guard receives JSON via stdin and returns exit code `0` (allow) or `2` (block). The `Stop` hook calls `dydo guard --stop` so dydo can derive a needs-human attention flag when a turn ends mid-task.

For Claude Code, the `permissions.allow` list also pre-approves common dydo commands so the human doesn't get prompted for every agent lifecycle call.

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

## Exclusion Layers

Path exclusions are enforced at three independent layers. They are intentionally not unified — each runs in a different stage of the pipeline and answers a different question. Editing one does not affect the others.

| Layer | Where | Question |
|-------|-------|----------|
| 1. Scan boundary | `Services/DocScanner.cs` (`GetScanExcludes`) | "Should this file enter the doc set at all?" Merges `dydo.json` `scanExclude` with the `ConfigFactory.DydoInternalScanExclude` invariants. |
| 2. Hub generation | `Services/HubGenerator.cs` `IsExcludedPath` (lines 310-320) | "Should hub files be generated for this path?" Skips `_system/`, `agents/`, and any hidden (`/.`) paths. |
| 3. Hub fix-up | `Commands/FixHubHandler.cs` `IsExcludedFolder` (lines 152-164), plus the `project/tasks` skip in `DeleteStaleTasksIndex` | "Should `dydo fix` create or rewrite a hub here?" Skips `_system`, `agents`, `_assets`, and refuses to clobber the manually-managed `project/tasks/_index.md`. |

If you need a path hidden from agents, use the off-limits patterns above. If you need a path scanned but not hubbed (or vice versa), pick the matching layer — don't try to unify them.

## Project Hub Tasks Prose

`HubGenerator` injects a hardcoded `## Tasks` section into `dydo/project/_index.md` whenever it (re)generates that hub. The prose lives in `HubGenerator.ProjectTasksProse` and is appended after the subfolder links (D4 lock: tasks are no longer auto-indexed as a subfolder, so the prose section is what tells agents how to find tasks). Edits to that section in `_index.md` will be overwritten by `dydo fix` — change the constant in `HubGenerator.cs` instead.

`HubGenerator.AutoGenComment` (the `<!-- Auto-generated by 'dydo fix' -->` banner emitted at the top of every generated hub) is now `public` so other commands — notably `FixHubHandler.DeleteStaleTasksIndex` — can detect generated files without redeclaring the literal.

---

## Related

- [Getting Started](../guides/getting-started.md)
- [CLI Commands Reference](./dydo-commands.md)
- [Templates and Customization](../understand/templates-and-customization.md)
- [Guard System](../understand/guard-system.md)
