---
area: reference
type: reference
---

# CLI Commands Reference

Complete reference for dydo's local documentation, compilation, guard, and configuration commands.
Live work is managed in Linear through its official surfaces; no dydo command creates, updates,
caches, polls, or mirrors a Linear object. FutureFeatures live in Linear and are promoted by the human; historical repository records remain
durable evidence rather than a second work board.

Commands find the project by walking up to the nearest `dydo.json`; `dydo validate` is the exception
and reads it from the current directory. `dydo help` prints the one-screen summary;
`dydo <command> --help` is the authoritative option list.

---

## Setup Commands

### dydo init

Create the project's durable knowledge tree and wire a runtime's guard hook.

```bash
dydo init <integration>              # claude, codex, all, or none
dydo init <integration> --join       # wire this machine, or an added runtime, into an existing project
```

Writes `dydo.json`, scaffolds the `dydo/` folders with their framework documents,
`files-off-limits.md`, `_system/types.json`, updates `.gitignore`, and writes the `CLAUDE.md` entry
point — plus `AGENTS.md` when `codex` is selected. `claude` and `codex` also install that runtime's
`PreToolUse` hook, so every matched tool call reaches `dydo guard`; `none` creates the documentation
framework with no runtime integration.

`--join` targets an already-initialized project: a fresh clone, or a second runtime added later. It
wires this machine's hook and entry point without re-scaffolding or overwriting the tree, and records
the integration in `dydo.json`.

dydo does not compile or install skills. A role is a plain `SKILL.md` folder committed under
`.claude/skills/` and `.agents/skills/`, edited directly.

---

## Documentation Commands

### dydo check

Validate documentation naming, frontmatter, titles, links, and project-specific rules. It also validates
the off-limits file, legacy FutureFeature shape under `project/future-features/`, retired v2 work records,
uncustomized foundation docs (warning), and `dydo.json` itself; config errors count toward exit `1`.

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

Repairs include filename normalization, wikilink conversion, and restoration of
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
Bash included. Both tiers bind on every caller; [Files Off-Limits](../files-off-limits.md) declares
them, their glob syntax, and the whitelist that lifts off-limits patterns. Nudges are configured in
`dydo.json` — see [DynaDocs](./about-dynadocs.md). `--stop` is a retained no-op so existing Stop-hook
wiring keeps resolving.

---

## Validation Command

### dydo validate

Validate `dydo.json` deserialization and nudge definitions.

```bash
dydo validate
```

This validates dydo's local configuration. It does not validate or provision Linear.

---

## Testing Command

### dydo gap-check

Run the project-configured coverage gap check.

```bash
dydo gap-check
dydo gap-check --force-run
```

The nearest `dydo.json` must contain `testing.runner`: a nonempty string array whose first item is
the executable and whose remaining items are fixed arguments. `dydo gap-check` starts it directly in
the configuration directory and appends every caller argument exactly as supplied. Its exit code is the
runner's exit code; configuration, startup, and cancellation failures exit `2`.

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
- [Files Off-Limits](../files-off-limits.md) — The two path tiers `dydo guard` enforces
- [Writing Documentation](./writing-docs.md) — Documentation conventions validated by dydo
