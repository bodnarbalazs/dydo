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

---

## Related

- [Tasks](../tasks/_index.md) - Work in progress
- [Decisions](../decisions/_index.md) - Why choices were made
