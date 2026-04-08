---
area: understand
type: concept
---

# Templates and Customization

How dydo's template system works: overridable templates, include tags, and the update mechanism. Templates define agent behavior — workflow steps, mode-specific instructions, and must-read lists. You customize them to match your project's process.

---

## What Templates Are

Templates generate the files agents follow: the workflow file (`workflow.md`) and mode files (`modes/code-writer.md`, `modes/reviewer.md`, etc.). When an agent is created or workspaces are initialized, templates are rendered into the agent's workspace.

Templates are not documentation — they're the scaffolding that tells agents how to behave in each role.

---

## Template Location

Templates live in `dydo/_system/templates/`:

| Template | Generates |
|----------|-----------|
| `agent-workflow.template.md` | `agents/<name>/workflow.md` |
| `mode-code-writer.template.md` | `agents/<name>/modes/code-writer.md` |
| `mode-reviewer.template.md` | `agents/<name>/modes/reviewer.md` |
| ... | One per role |

These are the local copies. Dydo ships embedded defaults — your local copies override them. Edit freely.

---

## Include Tags

Templates use `{{include:name}}` tags to pull in content from separate files. This lets you extend agent workflows without editing the templates directly.

### How It Works

1. Create a markdown file in `dydo/_system/template-additions/` (e.g., `extra-verify.md`)
2. Any template containing `{{include:extra-verify}}` will inline that file's content
3. When templates are rendered, the tag is replaced with the file contents

### Shipped Hooks

These tags are pre-placed at natural extension points:

| Tag | Template | Position |
|-----|----------|----------|
| `{{include:extra-must-reads}}` | All mode templates | After the must-reads list |
| `{{include:extra-verify}}` | code-writer, test-writer | After the verify step |
| `{{include:extra-review-steps}}` | reviewer | After "Run tests" step |
| `{{include:extra-review-checklist}}` | reviewer | End of review checklist |
| `{{include:extra-complete-gate}}` | code-writer, reviewer | End of complete section |
| `{{include:extra-test-guidance}}` | test-writer | After test guidance section |

You can also add `{{include:whatever}}` anywhere in a template — the system isn't limited to shipped hooks.

### The Two Paths

**The proper path:** Create files in `template-additions/` and use shipped hooks. Templates stay stock. Updates apply cleanly.

**The quick path:** Edit templates directly, adding your own `{{include:...}}` tags. On `dydo template update`, user-added tags are detected and re-anchored into the new template version.

---

## Template Updates

When dydo releases new template versions, update your local copies:

```bash
dydo template update          # Apply updates, preserve your include tags
dydo template update --diff   # Preview changes without writing
dydo template update --force  # Overwrite even if re-anchoring fails (creates .backup)
```

### How Updates Work

1. **Hash comparison** — Each template's SHA256 hash is compared against the embedded version
2. **Clean files** (hash matches embedded) — Overwritten with the new version directly
3. **User-edited files** — The system extracts user-added `{{include:...}}` tags, writes the new template, then re-anchors the tags by matching surrounding content
4. **Non-include edits are lost** — Only include tags are preserved across updates. Direct text edits to templates will be overwritten.

If re-anchoring can't place a tag, the update warns you. Use `--force` to overwrite anyway (the original is backed up to `.backup`).

### Binary Assets

Binary files like `_assets/dydo-diagram.svg` use byte-level hash comparison and are replaced when a new version is available.

---

## Related

- [Documentation Model](./documentation-model.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
- [Configuration Reference](../reference/configuration.md)
