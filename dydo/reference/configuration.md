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
  "integrations": {
    "claude": true,
    "codex": false
  },
  "models": {
    "tiers": { "anthropic": { "strong": "claude-fable-5" } },
    "roles": { "code-writer": "standard" }
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
| `integrations.claude` | bool | `true` | Whether Claude Code integration is active |
| `integrations.codex` | bool | `false` | Whether Codex integration is active |
| `models.tiers` | dict | — | Per-vendor tier → model bindings ([Decision 028](../project/decisions/028-model-tier-abstraction.md)) |
| `models.roles` | dict | — | Role → tier map; `dydo sync` resolves each to a concrete model |
| `frameworkHashes` | dict | — | SHA256 hashes of framework-owned files for `dydo template update` |

---

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|

---

## Hook Configuration

`dydo init claude` and `dydo init codex` wire the selected runtime's guard hooks automatically. Claude Code stores its hook configuration in `.claude/settings.local.json`; Codex stores it in `.codex/hooks.json`.

Claude Code example:

```json
{
  "permissions": {
    "allow": [
      "Bash(dydo check:*)",
      "Bash(dydo fix:*)",
      "Bash(dydo task:*)",
      "Bash(dydo sync:*)",
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

The `PreToolUse` hook intercepts each matched tool call and sends it to `dydo guard`; Codex additionally includes `apply_patch` because Codex exposes file edits through that tool. The guard receives JSON via stdin and returns exit code `0` (allow) or `2` (block). The `Stop` hook calls `dydo guard --stop`, which is a retained no-op — the agent-identity needs-human machinery it once drove was removed with the claim ceremony ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)); the wiring stays so existing installs keep resolving.

For Claude Code, the `permissions.allow` list also pre-approves common dydo commands so the human doesn't get prompted for every call.

---

## Customization Points

### Template Overrides

`dydo/_system/templates/` contains the role mode templates `dydo sync` compiles into native agents and skills. Edit these directly to change agent behavior, then re-run `dydo sync`.

Files: `mode-code-writer.template.md`, `mode-reviewer.template.md`, `mode-planner.template.md`, etc.

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

The mode template is the role: drop a `mode-<name>.template.md` into `dydo/_system/templates/` and run `dydo sync`. Frontmatter (`mode`, `description`, `emit`, `read-only`) declares the metadata; the body is the methodology.

See [Customizing Roles](../guides/customizing-roles.md) for the full guide.

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
