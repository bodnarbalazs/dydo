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
  "scanExclude": [
    "_system/.local/",
    "_system/audit/",
    "agents/"
  ],
  "nudges": [],
  "testing": {
    "runner": ["python", "scripts/gap_check.py"]
  }
}
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `version` | integer | Configuration schema version. |
| `structure.root` | string | Documentation root; defaults to `dydo`. |
| `integrations.claude` | boolean | Whether Claude Code integration is wired. |
| `integrations.codex` | boolean | Whether Codex integration is wired. |
| `scanExclude` | string[] | Paths excluded from documentation scanning. |
| `nudges` | object[] | Project guard rules. |
| `testing.runner` | nonempty string[] | Executable followed by fixed arguments for `dydo gap-check`. The executable is the first item; later empty arguments are preserved. |

Older 2.x configuration may still contain repository work-path, `skills` or `frameworkHashes`
fields. The 3.x runtime ignores those unknown properties safely. A fresh initialization emits
`structure.root` and no retired fields. A role is a plain skill folder, not a configuration entry.

`testing` is optional. When present, it must be an object containing a nonempty `runner` array of
strings, whose first item is a nonblank executable. The launcher starts that executable directly from
the directory containing the nearest `dydo.json`, appends the caller's arguments without shell parsing,
and inherits the terminal streams.

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

`dydo.json` carries no model and no effort, and a skill binds neither. The delegating admiral or
Issue Captain chooses the model — and the effort where the host exposes one — for each task it hands
out. There is no generated agent file carrying a default.

| Host | What the caller sets | Left to the session |
|---|---|---|
| Claude Code | the model on each Agent call | effort from the session |
| Codex | model and supported reasoning effort on each spawn | — |

A role reached as a skill inherits the session's model and effort. An explicit spawn value is the
last word over the host's agents defaults.

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
session but not that variable. dydo writes no agent file, so the session's effort stands for every role.

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

- `.claude/skills/<role>/` and `.agents/skills/<role>/` — the role folders; edit each host's copy directly.
- `dydo/files-off-limits.md` — the two universal path tiers: **off-limits** patterns, which no tool may
  read or write, and `## Protected Patterns`, which every tool may read and none may write or delete.
  Whitelist entries lift off-limits patterns only; [Guard System](../understand/guard-system.md) owns
  how each tier binds.

A role is its own source. Edit the skill folder in place; there is no compile step and no automatic
reconciliation.

## Documentation exclusion layers

| Layer | Owner | Question |
|---|---|---|
| Scan boundary | `Services/DocScanner.cs` and `scanExclude` | Should the path enter the documentation set? |

These layers answer different questions and are intentionally separate. Use off-limits rules for secret
or protected paths, not scan exclusions.

## Related

- [Getting Started](../guides/getting-started.md)
- [CLI Commands](./dydo-commands.md)
- [Templates and Customization](../understand/templates-and-customization.md)
- [Guard System](../understand/guard-system.md)
