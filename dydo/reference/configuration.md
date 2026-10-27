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
  "dydoVersion": "3.5.0",
  "structure": {
    "root": "dydo"
  },
  "integrations": {
    "claude": true,
    "codex": true
  },
  "scanExclude": [
    "_system/.local/",
    "agents/"
  ],
  "nudges": [],
  "testing": {
    "runner": ["python", "scripts/gap_check.py"]
  },
  "clean": {
    "archiveSizeCapMb": 1024
  }
}
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `version` | integer | Configuration schema version. |
| `dydoVersion` | string | The dydo release whose skills and documents the project last adopted. A fresh `dydo init` writes it, `--join` leaves it alone, and `dydo adopt <version>` sets it after an update (the `update-dydo` skill runs it). |
| `structure.root` | string | Documentation root; defaults to `dydo`. |
| `integrations.claude` | boolean | Whether Claude Code integration is wired. |
| `integrations.codex` | boolean | Whether Codex integration is wired. |
| `scanExclude` | string[] | Paths excluded from documentation scanning. |
| `nudges` | object[] | Project guard rules. |
| `testing.runner` | nonempty string[] | Executable followed by fixed arguments for `dydo gap-check`. The executable is the first item; later empty arguments are preserved. |
| `clean.archiveSizeCapMb` | positive integer | Size cap of `dydo clean`'s archive in megabytes; past it the oldest entries are pruned. Defaults to 1024. |

Older 2.x configuration may still contain repository work-path, `skills` or `frameworkHashes`
fields. The 3.x runtime ignores those unknown properties safely. A fresh initialization emits
`structure.root` and no retired fields. A role is a plain skill folder, not a configuration entry.

`testing` is optional. When present, it must be an object containing a nonempty `runner` array of
strings, whose first item is a nonblank executable. The launcher starts that executable directly from
the directory containing the nearest `dydo.json`, appends the caller's arguments without shell parsing,
and inherits the terminal streams.

`clean` is optional. `dydo clean` reads `clean.archiveSizeCapMb` from the nearest `dydo.json` and refuses to
run, exit `2`, when it is not a positive whole number; without the field the cap is 1024 MB. Entries older
than 90 days are pruned whatever the cap. See [CLI Commands](./dydo-commands.md#dydo-clean).

## Work-management boundary

There is no Linear token, object schema, cache path, poll interval, webhook, or synchronization field in
the active configuration. Linear owns Initiatives, Projects, tickets, FutureFeatures, optional Milestones and Cycles,
along with live workflow state. Git owns Decisions, reviewed Project plans, guides, audits, assimilation
briefs, and changelog.

## Hook configuration

`dydo init claude` and `dydo init codex` wire the selected runtime's guard hooks automatically.
Claude Code uses `.claude/settings.local.json`; Codex uses `.codex/hooks.json`. Both also get an allow
rule for `dydo clean`, so a host's auto mode does not stop it: `Bash(dydo clean:*)` and
`PowerShell(dydo clean:*)` in Claude Code's `permissions.allow`, and a `prefix_rule` in
`.codex/rules/dydo.rules` for Codex.

The `PreToolUse` hook sends matched tool calls to `dydo guard`. Exit `0` with empty stdout allows
the action; exit `0` with one `permissionDecision: "deny"` object on stdout blocks it, a contract
that survives Codex running hooks through PowerShell (see [Guard System](../understand/guard-system.md)). Codex includes `apply_patch` in its matcher because file edits use that tool.

## Model and effort at dispatch

`dydo.json` carries no model and no effort, and a skill binds neither. The delegating admiral or
Ticket Captain chooses the model - and the effort where the host exposes one - for each task it hands
out. There is no generated agent file carrying a default.

| Host | What the caller sets | Left to the session |
|---|---|---|
| Claude Code | the model on each Agent call | effort from the session |
| Codex | model and supported reasoning effort on each spawn | - |

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
The pattern is matched case-insensitively, and `$1`, `$2` and so on in the message take the
matching capture groups.

An optional `match` picks what the pattern is matched against:

| `match` | Pattern is matched against |
|---|---|
| absent or `"command"` | The shell command line of a shell tool call (Bash, PowerShell, Codex's shell tools). |
| `"new-file"` | The path of each file a write tool is about to create. |

A `new-file` nudge reads the target of Claude's Write, Edit, MultiEdit and NotebookEdit
(`file_path` or `notebook_path`) and every `*** Add File:` and `*** Move to:` path in a Codex
`apply_patch`. Each target is resolved against the hook's `cwd` and matched as a path relative to the
project root (the directory holding `dydo.json`), with forward slashes, such as `tools/x.py`. A target
outside the project root never matches. It fires only when the target exists neither as a file nor as
a directory, so edits to existing files pass. When several targets match, the first in call order
fires and fills the message's capture groups. Severities behave as for command nudges; a `warn`
blocks the first write and lets the retried write through, with a pass-through marker separate from
any command nudge of the same pattern. It applies to main sessions and subagents alike, after the
off-limits and protected checks.

```json
{
  "pattern": "^tools/(?!one-off/)",
  "message": "New tools go under tools/one-off/ unless the user asked for a permanent one.",
  "severity": "warn",
  "match": "new-file"
}
```

A file created through a shell command, including an `apply_patch` run inside a shell, is invisible
to `new-file` nudges; only command nudges see that call. The shipped default nudges are all command
nudges and stay in force whatever new-file nudges a project adds. A dydo release older than this
field ignores `match` and would treat the entry as a command nudge, so adopt the release before
adding one. `dydo validate` reports any other `match` value.

## Customization points

- `skills/<category>/<name>/` - the only editable skill folders. Run `node setup-skills.mjs` to create the
  Claude and Codex discovery projections; init gitignores both roots, in every mode. OpenCode is not a supported host: it may read both roots,
  but that is untested and there is no OpenCode init mode.
- `dydo/files-off-limits.md` - the two universal path tiers: **off-limits** patterns, which no tool may
  read or write, and `## Protected Patterns`, which every tool may read and none may write or delete.
  Whitelist entries lift off-limits patterns only; [Guard System](../understand/guard-system.md) owns
  how each tier binds.

A role's canonical folder is its source. Edit it in place; there is no compile step and no automatic
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
- [Scaffold and Customization](../understand/scaffold-and-customization.md)
- [Guard System](../understand/guard-system.md)
