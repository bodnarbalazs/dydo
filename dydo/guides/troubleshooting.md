---
area: guides
type: guide
---

# Troubleshooting

Common errors, guard blocks, and recovery patterns.

---

## Guard blocks

### "Path is off-limits to all agents"

```
BLOCKED: Path is off-limits to all agents.
  Path: .env
  Pattern: **/.env*
```

**Cause:** The path matches a pattern in `dydo/files-off-limits.md`. Off-limits applies to every caller — there is no identity or role that bypasses it.

**Fix:** If the file genuinely needs to be accessible, add a pattern to the `## Whitelist` section of `dydo/files-off-limits.md` (e.g. `.env.example`). Otherwise, leave it alone — that's the point.

### "Dangerous command pattern detected"

```
BLOCKED: Dangerous command pattern detected.
  Reason: Recursive delete with dangerous glob pattern
```

**Cause:** The bash command matched a destructive pattern (recursive root deletes, fork bombs, `dd` to disk, download-and-execute, …).

**Fix:** There is no override. Rewrite the command to do the narrow thing you actually intend.

### "Don't chain cd with other commands"

```
BLOCKED: Don't chain cd with other commands — it breaks auto-approval.
```

**Fix:** Run `cd` and your command separately, or use absolute paths.

### Blocked once, with "Run the same command again to proceed anyway"

**Cause:** A `warn`-severity nudge. It exists to make you pause, not to stop you.

**Fix:** Read the message. If the command is still right, run it again — the second attempt passes.

### "Don't use 'npx' to run dydo"

**Cause:** Indirect dydo invocation (`npx dydo`, `dotnet dydo`, `python dydo`, …). dydo is already on your PATH.

**Fix:** Call `dydo` directly, exactly as the message shows.

### "Dydo agents don't use Claude Code's built-in plan mode"

**Cause:** `EnterPlanMode`/`ExitPlanMode` are blocked by design.

**Fix:** Plan through the planner skill — plans are records under `dydo/project/`, not platform plan-mode state.

---

## Validation errors

### Running dydo check

```bash
dydo check                       # Check all docs
dydo check dydo/guides/          # Check specific folder
```

Common violations:

| Error | Meaning | Fix |
|-------|---------|-----|
| Missing frontmatter | File lacks `area` or `type` fields | Add YAML frontmatter block |
| Bad naming | File not in kebab-case | Rename to `kebab-case.md` |
| Broken link | Relative link points to non-existent file | Fix the path or remove the link |
| Missing hub file | Folder lacks `_index.md` | Run `dydo fix` to generate it |
| Orphan document | File not linked from anywhere | Add a link from a parent or hub file |
| Missing summary | No paragraph after the title | Add a 1-3 sentence summary |

### Auto-fixing

```bash
dydo fix                         # Auto-fix what's possible
```

Auto-fixes: kebab-case renaming, wikilink conversion, missing hub files, missing folder meta files.

---

## Sync issues

### Compiled skills look stale

`dydo sync` compiles templates into `.claude/skills`, `.claude/agents`, and `.claude/workflows`. If a compiled artifact doesn't reflect a template change, re-run `dydo sync`; when in doubt, delete the compiled output and sync fresh — the templates are the source of truth.

### Template update skipped a file

`dydo template update` only refreshes files whose content still matches the shipped original (tracked by hash). A file you customized is deliberately left alone — reconcile it by hand, or delete it and re-run the update to take the shipped version.

---

## Related

- [Guard System](../understand/guard-system.md) — How the guard hook works end-to-end
- [CLI Commands Reference](../reference/dydo-commands.md) — Full command documentation
- [Customizing Roles](./customizing-roles.md) — Templates, overrides, and what sync emits
