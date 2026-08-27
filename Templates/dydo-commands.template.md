---
area: reference
type: reference
---

# CLI Commands Reference

Complete reference for dydo's local documentation, compilation, guard, and configuration commands.
Live work is managed in Linear through its official surfaces; dydo intentionally provides no command
that creates, updates, caches, polls, or mirrors Linear objects. FutureFeatures remain repo-native ideas
under `dydo/project/future-features/` and are promoted only by a human.

---

## Setup Commands

### dydo init

Initialize DynaDocs in a project.

```bash
dydo init <integration>              # claude, codex, all, or none
dydo init <integration> --join       # wire another runtime or machine into an existing project
```

`claude` and `codex` install their native entry files and hook configuration; `all` wires both;
`none` creates the documentation framework without a supported runtime integration.

### dydo sync

Compile authored role templates into native Claude Code and Codex agents, skills, and resources, plus
Claude workflows.

```bash
dydo sync
```

Roles are discovered from `mode-<name>.template.md` files. Project overrides live in
`dydo/_system/templates/`. Change source templates and re-run this command; never hand-edit compiled
artifacts.

---

## Documentation Commands

### dydo check

Validate documentation structure, frontmatter, links, includes, summaries, and configured rules.

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

Evaluate universal off-limits rules, dangerous-command checks, and project nudges. Runtime hooks invoke
this command automatically; argument mode is available for diagnostics.

```bash
# Hook mode
echo '{"session_id":"manual","tool_name":"Edit","tool_input":{"file_path":"src/file.cs"}}' | dydo guard

# Diagnostic mode
dydo guard --action edit --path src/file.cs
dydo guard --command "git status"
dydo guard --stop
```

`--stop` is a retained no-op for compatible hook wiring. Guard exit `2` means the action was blocked.

---

## Template Command

### dydo template update

Update framework-owned templates and documents to the installed dydo version.

```bash
dydo template update
dydo template update --diff
dydo template update --force
```

- `--diff` previews changes without writing.
- `--force` overwrites when user include hooks cannot be re-anchored and creates backups first.

User-added `{{include:...}}` hooks are re-anchored when possible. Other edits to framework-owned files
can be replaced, so keep durable customization in supported overrides and additions.

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

Temporary model caps keep native workflows available during a provider limit or outage. Rebinding a
tier re-runs native artifact compilation and records enough local state to restore it.

### dydo model cap

```bash
dydo model cap <model> --until "08-28 09:00"
dydo model cap <model> --until "2026-08-28 09:00" --fallback <fallback-model>
```

### dydo model status

```bash
dydo model status
```

Shows active caps, fallback bindings, and reset times.

### dydo model uncap

```bash
dydo model uncap <model>
```

Restores the original bindings and clears the cap marker.

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

---

## Transition Compatibility Commands

Some dydo 2.x installations temporarily retain the commands below for final Notion export, rollback,
and migration proof. They do not define dydo 3's work model and must not be used to build a new mirror
of Linear. New live work belongs only in Linear; durable knowledge belongs in Git.

### dydo notion connect

Store the migration workspace's Notion integration token using the local secret store, or an explicitly
requested encrypted vault.

```bash
dydo notion connect
dydo notion connect --parent-page <id>
dydo notion connect --vault
```

### dydo notion reveal-token

Print the stored token through the guarded break-glass path.

```bash
dydo notion reveal-token --yes
```

### dydo notion sync

Run the retained 2.x reconciliation for migration verification only.

```bash
dydo notion sync --dry-run
dydo notion sync
dydo notion sync --prune
dydo notion sync --docs
dydo notion sync --docs-only --parent-page <id>
dydo notion sync --spine-only
dydo notion sync --allow-mass-delete
```

Do not infer a Linear integration from this command. dydo has no Linear token, schema, client, poller,
webhook receiver, cache, or Markdown mirror.

### dydo notion reset

Archive the retained Notion databases and recreate them from the 2.x model. This is destructive migration
compatibility behavior and requires deliberate confirmation.

```bash
dydo notion reset --dry-run
dydo notion reset
dydo notion reset --yes --parent-page <id>
```

### dydo watchdog

Run or stop the retained Notion reconciliation loop during the migration window.

```bash
dydo watchdog start
dydo watchdog start --interval 30 --census-interval 120
dydo watchdog stop
```

The watchdog is not a Linear integration and must not be repurposed as one.

---

## Environment Variables

The following variables belong only to the temporary Notion migration surface:

| Variable | Description |
|----------|-------------|
| `DYDO_NOTION_TOKEN` | Migration workspace token |
| `DYDO_NOTION_PARENT_PAGE` | Migration workspace parent page |

dydo defines no Linear environment variable.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success or action allowed |
| `1` | Validation errors or command failure |
| `2` | Tool error or guard block |

## Related

- [DynaDocs](./about-dynadocs.md) — Product boundary and operating model
- [Writing Documentation](./writing-docs.md) — Documentation conventions validated by dydo
