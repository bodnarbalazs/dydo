---
area: general
type: reference
---

# Documentation System

How this documentation is structured and maintained.

---

## Purpose

This documentation is designed for **dynamic traversal** by both humans and AI agents. Instead of loading everything upfront, readers start at the index and follow links to gather relevant context for their current task.

This approach is called **JITI** — Just In Time Information.

### Design Principles

1. **Hierarchical navigation** — Index → Hubs → Details (top-down)
2. **Graph connectivity** — Related docs link to each other (lateral)
3. **Predictable structure** — Consistent naming and organization
4. **Self-describing** — Frontmatter and summaries enable skimming

---

## Structure

```
project/
├── dydo.json                    # Configuration (agents, assignments)
├── CLAUDE.md                    # Entry point → dydo/index.md
│
└── dydo/                        # Documentation root
    ├── index.md                 # Main entry point
    ├── glossary.md              # Term definitions
    │
    ├── workflows/               # Agent workflow files
    │   ├── adele.md
    │   ├── brian.md
    │   └── ...
    │
    ├── understand/              # What things ARE
    │   ├── _index.md            # Hub
    │   ├── architecture.md
    │   └── {concept}.md
    │
    ├── guides/                  # How to DO things
    │   ├── _index.md
    │   ├── coding-standards.md
    │   ├── how-to-use-docs.md
    │   └── {task}.md
    │
    ├── reference/               # Specs and lookups
    │   ├── _index.md
    │   └── {topic}.md
    │
    ├── project/                 # Meta: how we work
    │   ├── _index.md
    │   ├── tasks/               # Cross-human task dispatch
    │   ├── decisions/           # Architecture Decision Records
    │   ├── pitfalls/            # Known gotchas
    │   └── changelog/           # Session notes
    │       └── {YYYY}/
    │           └── {YYYY-MM-DD}/
    │               └── topic.md
    │
    └── agents/                  # Agent workspaces (GITIGNORED)
        └── AgentName/
            ├── state.md
            ├── .session
            └── inbox/
```

### Folder Purposes

| Folder | Question it answers | Content type |
|--------|---------------------|--------------|
| `workflows/` | "Who am I and what do I do?" | Agent workflow files |
| `understand/` | "What IS this?" | Domain concepts, architecture |
| `guides/` | "How do I DO this?" | Task-oriented instructions |
| `reference/` | "What are the specs?" | APIs, configs, tool docs |
| `project/` | "Why/how do we work?" | Decisions, pitfalls, tasks, meta |
| `agents/` | "What's my state?" | Agent workspaces (local, gitignored) |

---

## Document Types

| Type | Purpose | Location | Naming |
|------|---------|----------|--------|
| `entry` | Main entry point | `index.md` | Always `index.md` |
| `hub` | Entry point for a folder | `_index.md` in any folder | Always `_index.md` |
| `workflow` | Agent-specific instructions | `workflows/` | `{agentname}.md` |
| `concept` | Explains what something IS | `understand/` | Named by concept |
| `guide` | How to accomplish a task | `guides/` | Named by task |
| `reference` | Specs, APIs, configs | `reference/` | Named by subject |
| `decision` | ADR — why we decided something | `project/decisions/` | `NNN-topic.md` |
| `pitfall` | Common mistake to avoid | `project/pitfalls/` | Named by problem |
| `changelog` | Session notes, what changed | `project/changelog/{YYYY}/{YYYY-MM-DD}/` | `topic.md` |

---

## Frontmatter

Every document starts with YAML frontmatter:

```markdown
---
area: frontend | backend | microservices | platform | general
type: hub | concept | guide | reference | decision | pitfall | changelog | workflow
---

# Title

Brief summary paragraph (1-3 sentences).

---

[Content]
```

### Required Fields

- `area` — Which part of the system this relates to
- `type` — What kind of document this is

### Conditional Fields

- `status` — Required for decisions: `proposed`, `accepted`, `deprecated`, `superseded`
- `date` — Required for decisions and changelog: `YYYY-MM-DD`
- `agent` — Required for workflow files: agent name

---

## File Naming

All documentation files use `kebab-case`:

```
✓ coding-standards.md
✓ api-patterns.md
✓ _index.md

✗ Coding Standards.md
✗ coding_standards.md
✗ CodingStandards.md
```

**Exceptions:**
- `CLAUDE.md` — All caps is convention for these meta files

---

## Linking

### Format

Use relative paths with `.md` extension:

```markdown
✓ [Standards](./coding-standards.md)
✓ [Term](./glossary.md#term-name)
✓ [Parent](../other-folder/doc.md)

✗ [[coding-standards]]
✗ [Standards](coding-standards)
✗ [Standards](/absolute/path.md)
```

### First Mention Rule

Link the first meaningful occurrence of a term, not every occurrence:

```markdown
The [Tease](../glossary.md#tease) editor allows creators to add
[ChoiceActions](./choice-actions.md). Users spend tokens on premium
ChoiceActions.  ← No link, already introduced
```

### Related Section

Add a "Related" section at document end for connections not mentioned in the body:

```markdown
## Related

- [Token Economy](../commerce/tokens.md) — Full monetization details
- [ADR-006: Refund Window](../../project/decisions/006-refund-window.md)
```

### Bidirectional Linking

When a decision or pitfall affects other docs, link both directions:

**In the concept doc:**
```markdown
Users can refund within 6 minutes. See [ADR-006](../decisions/006-refund-window.md).
```

**In the decision doc:**
```markdown
## Affects
- [Refunds](../../understand/commerce/refunds.md)
```

---

## Hub Files

Every folder with multiple docs needs an `_index.md` that:

1. Provides a brief overview
2. Lists and describes each child doc
3. Helps readers decide which child to read

```markdown
---
area: backend
type: hub
---

# Backend Guides

Guides for working with the C# backend.

---

## Contents

- [API Patterns](./api-patterns.md) — Minimal API conventions
- [Database](./database.md) — EF Core, migrations, queries
- [Background Jobs](./background-jobs.md) — TickerQ, consumers
```

### Clustering Rule

When a folder exceeds ~7-10 items, create subfolders with their own `_index.md`.

---

## Validation

The `dydo` tool validates these rules:

| Rule | Auto-fixable |
|------|--------------|
| Kebab-case naming | Yes |
| Relative links only | Yes (wikilinks converted) |
| Frontmatter required | No (adds template) |
| Summary required | No |
| No broken links | No |
| Hub files in folders | Yes (creates skeleton) |
| No orphan docs | No |

Run `dydo check` to validate, `dydo fix` to auto-fix what's possible.

---

## Graph Queries

The documentation forms a knowledge graph. Use `dydo graph` to explore connections:

```bash
# Find docs that link TO a file (backlinks)
dydo graph tokens.md --incoming

# Find docs within N link-hops of a file
dydo graph tokens.md --degree 2

# Combine both
dydo graph tokens.md --incoming --degree 2
```

**Use cases:**

- **Impact analysis** — Before modifying a doc, check `--incoming` to see what depends on it
- **Context gathering** — Use `--degree 2` to find indirectly related docs
- **Debugging** — Trace connections when something references outdated information

---

## Changelog Structure

Changelogs live in date-based folders:

```
project/changelog/
├── 2025/
│   ├── 2025-01-15/
│   │   ├── auth-refactor.md
│   │   └── token-migration.md
│   └── 2025-01-20/
│       └── api-versioning.md
└── 2026/
    └── ...
```

Each changelog documents:

1. **Summary** — What was done and why
2. **Decisions** — Links to any ADRs created
3. **Pitfalls** — Links to any pitfalls encountered/documented
4. **Files Changed** — Every file touched, with brief descriptions

The "Files Changed" section enables future debugging by tracing changes back to their source.

---

## Related

- [Coding Standards](./coding-standards.md) — Code conventions
- [Workflow](./workflow.md) — Multi-agent workflow system
- [Glossary](./glossary.md) — Term definitions
