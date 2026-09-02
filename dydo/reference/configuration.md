---
area: reference
type: reference
---

# Configuration Reference

Complete reference for the active `dydo.json` configuration, runtime hooks, models, documentation
scanning, and customization. The configuration has no Linear client or schema: live work is managed
through Linear's official surfaces.

## dydo.json

`dydo.json` lives at the project root and is created by `dydo init`.

### Active schema

```json
{
  "version": 1,
  "name": "optional-project-slug",
  "structure": {
    "root": "dydo"
  },
  "paths": {
    "source": ["src/**"],
    "tests": ["tests/**"],
    "pathSets": null
  },
  "integrations": {
    "claude": true,
    "codex": true
  },
  "models": {
    "tiers": {
      "anthropic": { "strong": "claude-fable-5" },
      "openai": { "strong": "gpt-5.6-sol" }
    },
    "agents": {
      "reviewer": "strong",
      "code-writer": "standard"
    },
    "efforts": {},
    "fallback": null
  },
  "scanExclude": [
    "_system/.local/",
    "_system/audit/",
    "agents/"
  ],
  "nudges": [],
  "frameworkHashes": {}
}
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `version` | integer | Configuration schema version. |
| `name` | string or null | Optional project slug used by temporary migration compatibility where documented. |
| `structure.root` | string | Documentation root; defaults to `dydo`. |
| `paths.source` | string[] | Source globs exposed to role compilation and project guidance. |
| `paths.tests` | string[] | Test globs exposed to role compilation and project guidance. |
| `paths.pathSets` | object or null | Custom named path groups for roles. |
| `integrations.claude` | boolean | Whether Claude Code integration is wired. |
| `integrations.codex` | boolean | Whether Codex integration is wired. |
| `models.tiers` | object | Vendor-specific model bindings for abstract tiers. |
| `models.agents` | object | Agent-to-tier bindings resolved by `dydo sync`. |
| `models.efforts` | object | Optional reasoning-effort overrides. |
| `models.fallback` | string or null | Optional fallback model for temporary caps. |
| `scanExclude` | string[] | Paths excluded from documentation scanning. |
| `nudges` | object[] | Project guard rules. |
| `frameworkHashes` | object | Product-managed hashes used by `dydo template update`. |

Older 2.x configuration may still contain repository work-path fields. The 3.x runtime ignores those
unknown properties safely and does not migrate them into another local work model. A fresh
initialization emits only `structure.root`.

## Work-management boundary

There is no Linear token, object schema, cache path, poll interval, webhook, or synchronization field in
the active configuration. Linear owns Initiatives, Projects, Issues, optional Milestones and Cycles,
along with live workflow state. Git owns Decisions, reviewed Project plans, guides, audits, assimilation
briefs, changelog, and FutureFeature ideas.

## Hook configuration

`dydo init claude` and `dydo init codex` wire the selected runtime's guard hooks automatically.
Claude Code uses `.claude/settings.local.json`; Codex uses `.codex/hooks.json`.

The `PreToolUse` hook sends matched tool calls to `dydo guard`. Exit `0` allows the action and exit
`2` blocks it. Codex includes `apply_patch` in its matcher because file edits use that tool. The
retained `Stop` hook calls `dydo guard --stop`, a compatibility no-op after dydo ceded lifecycle
orchestration to the host runtime.

## Model tiers

Agents bind to abstract tiers such as `strong`, `standard`, and `light`; vendor blocks bind those
tiers to concrete models. `dydo sync` resolves the current bindings when it compiles native artifacts.
Use `dydo model cap`, `dydo model status`, and `dydo model uncap` for temporary availability caps
instead of editing compiled agents.

## Nudges

Each nudge has a regular-expression `pattern`, a `message`, a `severity` (`notice`, `warn`, or
`block`), and optionally a `tools` allow-set. Notices inform, warnings require a deliberate retry,
and blocks reject the action. Nudges enforce project process; they do not create or update work records.

## Customization points

- `dydo/_system/templates/` — project-local role, resource, workflow, and framework template overrides.
- `dydo/_system/template-additions/` — durable `{{include:name}}` fragments.
- `dydo/files-off-limits.md` — the two universal path tiers: **off-limits** patterns, which no tool may
  read or write, and `## Protected Patterns`, which every tool may read and none may write or delete.
  Whitelist entries lift off-limits patterns only; [Guard System](../understand/guard-system.md) owns
  how each tier binds.
- `paths.pathSets` — named source/test groupings available to compiled methods.

Change source templates and run `dydo sync`; never hand-edit compiled `.claude/`, `.codex/`, or
`.agents/skills/` artifacts.

## Documentation exclusion layers

| Layer | Owner | Question |
|---|---|---|
| Scan boundary | `Services/DocScanner.cs` and `scanExclude` | Should the path enter the documentation set? |
| Hub generation | `Services/HubGenerator.cs` | Should a documentation hub be generated here? |
| Hub fix-up | `Commands/FixHubHandler.cs` | Should `dydo fix` create or rewrite a hub here? |

These layers answer different questions and are intentionally separate. Use off-limits rules for secret
or protected paths, not scan exclusions.

## Related

- [Getting Started](../guides/getting-started.md)
- [CLI Commands](./dydo-commands.md)
- [Templates and Customization](../understand/templates-and-customization.md)
- [Guard System](../understand/guard-system.md)
