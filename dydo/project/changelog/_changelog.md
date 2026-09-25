---
area: project
type: folder-meta
---

# Changelog

Chronological record of completed work. Essential for debugging and understanding what changed when.

## When to Write an Entry

Create a changelog entry when:
- A task is approved
- Significant changes are deployed
- Bugs are fixed

## Folder Structure

Organize by year and date:
```
changelog/
├── 2025/
│   ├── 2025-01-15/
│   │   ├── auth-refactor.md
│   │   └── token-migration.md
│   └── 2025-01-20/
│       └── api-versioning.md
└── 2026/
    └── ...
```

> **Note:** This structure is a suggestion. Flat organization or other schemes work fine—dydo doesn't enforce changelog folder structure.

## File Format

Filename: `topic-name.md` (kebab-case)

Required sections:
- **Summary** - What was done and why
- **Files Changed** - Every file touched (critical for debugging)

## Releases

- [dydo 3.1.0 — Linear project map](./2026/2026-09-25/dydo-3-1-0-linear-project-map.md)
- [dydo 3.0.0 — Linear PM and Notion runtime removal](./2026/2026-08-27/dydo-3-0-0-linear-pm-and-notion-runtime-removal.md)

---

## Related

- [Decisions](../decisions/_decisions.md) - Why choices were made
