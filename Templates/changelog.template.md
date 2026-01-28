---
area: general
type: changelog
date: YYYY-MM-DD
---

# {Brief Title}

One-line summary of what was done.

---

## Summary

- What was accomplished
- Why it was done (context)

## Decisions

- [ADR-NNN: Title](../../decisions/NNN-topic.md) — if any decisions were made

## Pitfalls

- [Pitfall Name](../../pitfalls/pitfall-name.md) — if any were encountered/created

## Files Changed

```
path/to/file.cs — Brief description of change
path/to/another.ts — Brief description
path/to/deleted.md — Deleted (reason)
```

<!--
Changelog guidelines:

Location: project/changelog/{YYYY}/{YYYY-MM-DD}/topic-name.md

Example path: project/changelog/2025/2025-01-15/auth-refactor.md

Structure:
  changelog/
  ├── 2025/
  │   ├── 2025-01-15/
  │   │   ├── auth-refactor.md
  │   │   └── token-migration.md
  │   └── 2025-01-20/
  │       └── api-versioning.md
  └── 2026/
      └── ...

The "Files Changed" section is critical for future debugging.
List every file touched, with a brief note on what changed.

Keep summaries concise — this is a reference, not a narrative.
-->
