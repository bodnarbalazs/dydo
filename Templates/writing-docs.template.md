---
area: reference
type: reference
---

# Writing Documentation

Reference for documentation conventions, structure, and validation rules.

---

## Frontmatter

Every document requires YAML frontmatter at the top:

```yaml
---
area: guides
type: guide
---
```

### Required Fields

| Field | Values | Description |
|-------|--------|-------------|
| `area` | `understand`, `guides`, `reference`, `general`, `frontend`, `backend`, `microservices`, `platform` | Which section this doc belongs to |
| `type` | `context`, `concept`, `guide`, `reference`, `hub`, `decision`, `pitfall`, `changelog` | What kind of document this is |

### Additional Fields

| Field | When Required | Values |
|-------|---------------|--------|
| `status` | type: decision | `proposed`, `accepted`, `deprecated`, `superseded` |
| `date` | type: decision or changelog | `YYYY-MM-DD` format |

---

## Document Types

| Type | Purpose | Structure |
|------|---------|-----------|
| `context` | Background information | Overview → Details → Related |
| `concept` | Explain a concept | What → Why → How → Related |
| `guide` | Step-by-step instructions | Goal → Prerequisites → Steps → Verification |
| `reference` | Look-up information | Tables, lists, specifications |
| `hub` | Index page for a folder | Title → Description → Links to contents |
| `decision` | Architecture Decision Record | Context → Decision → Consequences |
| `pitfall` | Known gotcha | Problem → Symptom → Solution |
| `changelog` | Change log entry | What changed → Why → Impact |

---

## Naming Conventions

- **Files:** `kebab-case.md` (lowercase, hyphens)
- **Folders:** `kebab-case/` (lowercase, hyphens)
- **Hub files:** `_index.md` in each folder

Examples:
- `api-authentication.md` (correct)
- `API Authentication.md` (incorrect)
- `apiAuthentication.md` (incorrect)

---

## Structure

### Title and Summary

Every doc must start with:

```markdown
# Title

A 1-3 sentence summary of what this document covers.
```

The summary helps agents quickly determine if this doc is relevant.

### Related Section

End every doc with a Related section:

```markdown
## Related

- [Related Doc](./path/to/doc.md) — Brief description
```

This enables navigation through the documentation.

---

## Links

- **Always use relative paths:** `../folder/file.md`
- **Never use absolute paths:** `/docs/folder/file.md`
- **Link text should be descriptive:** `[Authentication Guide](./auth.md)` not `[click here](./auth.md)`

---

## Validation

Run before committing:

```bash
dydo check              # Find issues
dydo fix                # Auto-fix what's possible
```

### Rules Enforced

| Rule | What It Checks |
|------|----------------|
| Frontmatter | Required fields present, values valid |
| Naming | Files and folders are kebab-case |
| Summary | Title exists, summary paragraph follows |
| Links | Relative paths, no broken links |
| Hub Files | Each folder has `_index.md` |
| Orphans | Every doc is linked from somewhere |

---

## Exploring Docs

Use `dydo graph` to see how a document connects to others:

```bash
dydo graph dydo/understand/architecture.md
```

This shows incoming and outgoing links - useful when updating docs to ensure you haven't broken connections.

---

## Related

- [How to Use These Docs](../guides/how-to-use-docs.md) — Navigating the documentation
- [dydo Commands Reference](./dydo-commands.md) — Full command documentation
