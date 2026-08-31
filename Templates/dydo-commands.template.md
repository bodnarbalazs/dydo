---
area: reference
type: reference
---

# CLI Commands Reference

Complete reference for dydo's local documentation, compilation, guard, and configuration commands.
Live work is managed in Linear through its official surfaces; no dydo command creates, updates,
caches, polls, or mirrors a Linear object. FutureFeatures stay repo-native ideas under
`dydo/project/future-features/` and are promoted only by a human.

Commands find the project by walking up to the nearest `dydo.json`. `dydo help` prints the one-screen
summary; `dydo <command> --help` is the authoritative option list.

---

## Setup Commands

### dydo init

Create the project's durable knowledge tree and wire a runtime's guard hook.

```bash
dydo init <integration>              # claude, codex, all, or none
dydo init <integration> --join       # wire this machine, or an added runtime, into an existing project
```

Writes `dydo.json`, scaffolds the `dydo/` folders with their framework documents and
`files-off-limits.md`, mirrors the shipped templates into `dydo/_system/templates/`, updates
`.gitignore`, and writes the `CLAUDE.md` entry point — plus `AGENTS.md` when `codex` is selected.
`claude` and `codex` also install that runtime's `PreToolUse` hook, so every matched tool call reaches
`dydo guard`; `none` creates the documentation framework with no runtime integration. Nothing is
compiled here — run `dydo sync` next.

`--join` targets an already-initialized project: a fresh clone, or a second runtime added later. It
wires this machine's hook and entry point without re-scaffolding or overwriting the tree, and records
the integration in `dydo.json` so `dydo sync` emits for it.

### dydo sync

Compile the authored skill templates into native Claude Code and Codex artifacts.

```bash
dydo sync
```

Roles are discovered by enumerating `skill-<name>.template.md`: the shipped set plus any project-local
template in `dydo/_system/templates/`, which is how a project overrides a role or adds one of its own.
Frontmatter decides each artifact's shape — `emit: agent` (the default) produces an agent definition
*and* a skill, `emit: skill` produces the skill alone, `read-only: true` withholds the editing tools,
`delegates: true` grants the `Agent` tool, and `invocation: explicit` disables model invocation on
both hosts. A role's `## Must-Reads` links become its agent's context list, links in the compiled body
are rewritten to resolve from the emitted skill folder, `<role>-resource-<name>.template.md` files
compile into that skill's `resources/`, and workflow harnesses compile into Claude's workflow folder.

Only the integrations recorded in `dydo.json` are emitted. Every run also deletes outputs dydo no
longer ships: retired workflows, resources retired by rename, and retired roles — unless a
project-local template of that name keeps the role alive.

Change the source template and re-run this command; never hand-edit a compiled artifact.

---

## Documentation Commands

### dydo check

Validate documentation naming, frontmatter, summaries, links, hub and folder-meta coverage, orphans,
and the off-limits patterns.

```bash
dydo check
dydo check <path>
```

Exit `0` means no errors. Exit `1` means validation errors were found.

### dydo fix

Apply supported documentation repairs.

```bash
dydo fix
dydo fix <path>
```

Repairs include filename normalization, wikilink conversion, index/meta maintenance, and restoration of
required scan exclusions. Review the Git diff afterward.

### dydo index

Regenerate documentation indexes from the configured structure.

```bash
dydo index
dydo index <path>
```

### dydo graph

Show incoming and outgoing documentation links for one file.

```bash
dydo graph <file>
dydo graph <file> --incoming
dydo graph <file> --degree 2
```

### dydo graph stats

Show repository-wide documentation graph statistics.

```bash
dydo graph stats
dydo graph stats --top 20
```

---

## Guard Command

### dydo guard

Evaluate one tool call against the two path tiers, the dangerous-command rules, and the project's
nudges. Runtime hooks invoke this command automatically; the argument form is for diagnostics.

```bash
# Hook mode
echo '{"session_id":"manual","tool_name":"Edit","tool_input":{"file_path":"src/file.cs"}}' | dydo guard

# Diagnostic mode
dydo guard --action edit --path src/file.cs
dydo guard --command "git status"
dydo guard --stop
```

Exit `0` allows the action; exit `2` blocks it with `BLOCKED:` on stderr. **Off-limits** paths block
every operation, reads included. **Protected** paths are readable by any tool and writable by none,
Bash included. Both tiers are declared in `dydo/files-off-limits.md` and bind on every caller.
`--stop` is a retained no-op so existing Stop-hook wiring keeps resolving.

[Guard System](../understand/guard-system.md) owns the layers, the tiers, and the hook payload;
[Configuration Reference](./configuration.md) owns the nudge format.

---

## Template Command

### dydo template update

Refresh this project's framework-owned templates and documents to the running dydo version.

```bash
dydo template update
dydo template update --diff
dydo template update --force
```

- `--diff` previews changes without writing.
- `--force` overwrites when user include hooks cannot be re-anchored, backing the file up first.

The mirrored templates under `dydo/_system/templates/` and the framework documents in
`dydo/reference/` and `dydo/guides/` are compared against the shipped set. An unmodified copy is
overwritten. In a template, your `{{include:...}}` hooks are re-anchored into the new text and other
edits are replaced; an edited framework document is left alone and reported instead. A mirrored
template dydo no longer ships is deleted, but only when its stored hash proves the copy is dydo's — a
role a project authored itself is untracked and survives. The run also tops up default nudges, scan
exclusions, and frontmatter types. Warnings exit `1` unless `--force` is given.

Durable customization belongs in `{{include:...}}` fragments; other edits to framework-owned files can
be replaced.

---

## Validation Command

### dydo validate

Validate `dydo.json` deserialization and nudge definitions.

```bash
dydo validate
```

This validates dydo's local configuration. It does not validate or provision Linear.

---

## Model Commands

Temporary model caps keep native agents available during a provider limit or outage. Capping rebinds
every tier that names the model, re-runs compilation, and records enough local state to restore it;
the guard lifts an expired cap on a later run, without human intervention.

### dydo model cap

```bash
dydo model cap <model> --until "08-28 09:00"
dydo model cap <model> --until "2026-08-28 09:00" --fallback <fallback-model>
```

`--until` is required and takes `[yyyy-]mm-dd hh:mm` in local time — the reset the limit error states.
`--fallback` defaults to `models.fallback` in `dydo.json`.

### dydo model status

```bash
dydo model status
```

Shows active caps with their fallback and reset time, and expired caps still awaiting restoration.

### dydo model uncap

```bash
dydo model uncap <model>
```

Restores the original bindings, clears the cap marker, and re-compiles.

---

## Utility Commands

### dydo completions

Generate a completion script for the requested shell.

```bash
dydo completions bash
dydo completions zsh
dydo completions powershell
```

### dydo version

```bash
dydo version
```

### dydo help

```bash
dydo help
```


## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success or action allowed |
| `1` | Validation errors or command failure |
| `2` | Tool error or guard block |

## Related

- [DynaDocs](./about-dynadocs.md) — Product boundary and operating model
- [Configuration Reference](./configuration.md) — `dydo.json`, hooks, models, and customization points
- [Guard System](../understand/guard-system.md) — What `dydo guard` enforces and how
- [Writing Documentation](./writing-docs.md) — Documentation conventions validated by dydo
