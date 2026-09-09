---
area: reference
type: reference
---

# Configuration Reference

Complete reference for the active `dydo.json` configuration, runtime hooks, documentation scanning,
and customization. The configuration has no Linear client or schema: live work is managed
through Linear's official surfaces.

## dydo.json

`dydo.json` lives at the project root and is created by `dydo init`.

### Active schema

```json
{
  "version": 1,
  "structure": {
    "root": "dydo"
  },
  "integrations": {
    "claude": true,
    "codex": true
  },
  "skills": {
    "writing-for-humans": {
      "enabled": true,
      "origin": "shipped",
      "emitAgent": false,
      "codexMetadata": false,
      "resources": []
    }
  },
  "scanExclude": [
    "_system/.local/",
    "_system/audit/",
    "_system/templates/",
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
| `structure.root` | string | Documentation root; defaults to `dydo`. |
| `integrations.claude` | boolean | Whether Claude Code integration is wired. |
| `integrations.codex` | boolean | Whether Codex integration is wired. |
| `skills.<name>.enabled` | boolean | The human-authored switch controlling whether the local source emits. |
| `skills.<name>.origin` | `shipped` \| `custom` | Generated source ownership and retirement provenance. |
| `skills.<name>.emitAgent` | boolean | Generated prior agent-output shape used for exact cleanup. |
| `skills.<name>.codexMetadata` | boolean | Generated prior `agents/openai.yaml` shape, emitted by explicit invocation or an argument hint. |
| `skills.<name>.resources` | string[] | Generated, sorted prior resource-output shape used for exact cleanup. |
| `scanExclude` | string[] | Paths excluded from documentation scanning. |
| `nudges` | object[] | Project guard rules. |
| `frameworkHashes` | object | Product-managed hashes used by `dydo template update`. |

Older 2.x configuration may still contain repository work-path fields. The 3.x runtime ignores those
unknown properties safely and does not migrate them into another local work model. A fresh
initialization emits `structure.root` and no retired work-path fields.

`enabled` is the only hand-authored member of a skill switch. A new custom source may begin with the
minimal `{ "enabled": true }`; the next successful sync fills the generated members. Discovery adds
a valid source missing from the switchboard as enabled and never changes an existing true or false.
Malformed switches fail update, sync, check, and validate rather than receiving defaults.

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

## Model and effort at dispatch

`dydo.json` carries no model and no effort. `dydo sync` emits every role in the shape that leaves
its host selectable, and the delegating admiral or Issue Captain chooses the model — and the effort
where the host exposes one — for each task it hands out.

| Host | The generated agent carries | Left to the caller |
|---|---|---|
| Claude Code | `model: inherit`, and no effort key | the model on each Agent call; effort from the session |
| Codex | neither `model` nor `model_reasoning_effort` | both values on each spawn |

`model: inherit` is inheritance behaviour rather than a pinned model: it resolves to the main
conversation's model. Codex omits both keys because a custom agent file's own keys are the last
word over everything below, so emitting either would defeat the caller's choice.

### Claude Code precedence

From 2.1.251 onward a subagent's model resolves in this order:

1. the per-invocation `model` on the Agent call;
2. the agent file's `model`, where `inherit` means the main conversation model;
3. the `CLAUDE_CODE_SUBAGENT_MODEL` environment override;
4. the parent model.

Earlier versions place the environment override first. Organization policy may substitute another
model for a blocked one, so the model that runs is not always the model that was asked for.

Claude Code exposes no per-Agent-call effort argument: effort belongs to the session (`--effort`)
and to the environment variable that outranks it, and an agent file's own `effort` overrides the
session but not that variable. dydo emits none, so the session's effort stands for every role.

### Codex precedence

A subagent's model and reasoning effort each resolve in this order:

1. the explicit spawn value;
2. the matching agents default in `[agents]`;
3. the parent value.

A custom agent file's `model` or `model_reasoning_effort` then overrides whatever that produced.
An explicit task choice supplies model and supported reasoning effort together: a model chosen
without an effort takes that model's own default, which is rarely the one the task wanted.

### Identity is three separate fields

| Field | Meaning | Where it comes from |
|---|---|---|
| requested model, requested session effort | what the caller asked for | the dispatch argument itself |
| configured model, configured effort | what files and settings declare | the agent file, host configuration |
| effective model, effective effort | what actually ran | host telemetry only |

A prompt, or a child's report of its own identity, never proves an effective value. Where no
trustworthy telemetry exposes one, the honest record is `unproved`: Claude Code's session metadata
attributes an effective model, while neither host's documented machine-readable output promises an
effective effort. A claim that depends on host behaviour names the host versions the native canary
evidence observed.

## Nudges

Each nudge has a regular-expression `pattern`, a `message`, a `severity` (`notice`, `warn`, or
`block`). Notices inform, warnings require a deliberate retry,
and blocks reject the action. Nudges enforce project process; they do not create or update work records.

## Customization points

- `dydo/_system/template-additions/` — durable `{{include:name}}` fragments.
- `dydo/_system/templates/` — flat local skill/resource sources; shipped copies are overwritten on update and distinctly named custom sources survive.
- `dydo/files-off-limits.md` — the two universal path tiers: **off-limits** patterns, which no tool may
  read or write, and `## Protected Patterns`, which every tool may read and none may write or delete.
  Whitelist entries lift off-limits patterns only; [Guard System](../understand/guard-system.md) owns
  how each tier binds.

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
