---
area: guides
type: guide
---

# How to Use These Docs

This documentation is designed to be navigated by AI agents. It follows JITI (Just-In-Time Information) - you don't need to read everything upfront.
Navigate to what you need, when you need it. Read what's seems to be relevant and leave out what's not.

---

## Documentation Structure

| Folder | Contains | When to Read |
|--------|----------|--------------|
| `understand/` | Project overview, architecture, domain context | Starting a new task |
| `guides/` | How-to guides, coding standards | When doing specific work |
| `reference/` | Command reference, API specs, config | When you need exact details |
| `project/` | Decisions, changelog, tasks, pitfalls | When you need history/context |

**Subfolders:** Each subfolder has a `_foldername.md` meta file that describes its purpose. Check this first to understand what a folder contains.

---

## Document Types

The frontmatter at the top of each doc tells you what kind it is:

| Type | Purpose |
|------|---------|
| `context` | Background information, overviews |
| `guide` | Step-by-step instructions |
| `reference` | Look-up information |
| `hub` | Index pages linking to other docs |

---

## Navigation

### Index Files

Every folder has an `_index.md` — the table of contents for that area:

| Index | Contents |
|-------|----------|
| `understand/_index.md` | Context and architecture docs |
| `guides/_index.md` | How-to guides |
| `reference/_index.md` | Reference docs |
| `project/_index.md` | Decisions, changelog, pitfalls |

**Folder Meta Files:** Subfolders also have `_foldername.md` files (e.g., `guides/api/_api.md`) that describe the folder's purpose. The first sentence appears as a summary in the parent `_index.md`.

### Related Links

Docs link to each other via **Related** sections at the bottom. Follow links only when you need more detail.

---

## Exploring Connections

Use `dydo graph` to see how documents connect:

```bash
dydo graph dydo/understand/architecture.md
```

This shows what links to and from a document - useful for finding related context.

---

## Key Reference Documents

| Document | Purpose |
|----------|---------|
| `glossary.md` | Domain-specific terms |
| `project/decisions/` | Why architectural choices were made |
| `project/changelog/` | What changed and when |
| `project/pitfalls/` | Known gotchas to avoid |

---

## Related

- [Architecture](../understand/architecture.md) — Project structure
- [Coding Standards](./coding-standards.md) — Code conventions
- [dydo Commands Reference](../reference/dydo-commands.md) — Full command documentation
