---
area: understand
type: concept
---

# Documentation Model

dydo uses JITI (Just-In-Time Information) so agents load only the durable knowledge relevant to the
current Linear Issue or conversation. Documentation is curated memory, not a mirror of live workflow
state.

## The documentation funnel

```text
entry file → dydo/index.md → folder hubs → issue-relevant durable docs
```

1. The host loads `CLAUDE.md` or `AGENTS.md`.
2. `dydo/index.md` establishes the knowledge and work boundary.
3. Folder hubs narrow navigation to `understand/`, `guides/`, `reference/`, or `project/`.
4. The agent reads only the Decisions, plan, architecture, guide, or reference material needed now.
5. An invoked skill adds the methodology for the current kind of work.

Linear supplies current Issue/Project state. Repository documents supply durable context and reviewed
contracts. Copying volatile Linear state into Markdown creates drift and is not part of this model.

## Folder purposes

| Folder | Contains | Read when |
|---|---|---|
| `understand/` | Domain concepts, architecture, system overview | Building a mental model |
| `guides/` | Procedures and coding conventions | Performing a known activity |
| `reference/` | Exact commands, schemas, and rules | Looking up precise behavior |
| `project/` | Decisions, reviewed plans, audits, changelog, pitfalls, ideas | Needing durable intent or proof |

## Project knowledge

The `project/` tree contains information that remains valuable after current execution state changes:
accepted Decisions, reviewed coordinated-work plans, audits, assimilation briefs, migration evidence,
release history, pitfalls, and FutureFeatures.

A FutureFeature is an unscheduled idea, not actionable work. Only the human may promote it to exactly
one Linear Initiative, Project, or Issue. Its terminal promotion reference is provenance; delivery
status remains in Linear.

## File conventions

Every document begins with YAML frontmatter and an H1. An opening summary is optional; a section
or list may follow the title directly.
Folder `_index.md` files provide navigation; direct child folders use `_<folder>.md` metadata.
Relative Markdown links form the durable documentation graph.

```yaml
---
area: understand
type: concept
---
```

Use `dydo graph` to explore relationships and `dydo graph stats` to find central documents.

## Validation

```bash
dydo check
dydo fix
```

`dydo check` validates frontmatter, titles, naming, links, hubs, folder metadata, and project rules.
`dydo fix` applies supported repairs; review its diff because generated hubs and framework-owned files
may change.

## Related

- [Writing Documentation](../reference/writing-docs.md)
- [Templates and Customization](./templates-and-customization.md)
