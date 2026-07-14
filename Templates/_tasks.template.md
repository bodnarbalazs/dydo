---
area: project
type: folder-meta
---

# Tasks

Task tracking for work in progress. Tasks are created by agents when starting work and updated throughout the workflow.

## Task Lifecycle

1. **backlog** - Created, work not started
2. **in-progress** - Work underway
3. **in-review** - Ready for code review
4. **done** - Verified work, awaiting archival

## File Format

Tasks are created via `dydo task create <name> --area <area>`. Each task has:
- Frontmatter: area, name, status, created, assigned, updated
- Progress checklist
- Files Changed section (critical for debugging)
- Review Summary

## Organization

Tasks stay flat in this folder. Done tasks stay here until the human archives them.

---

## Related

- [Changelog](../changelog/_index.md) - Where completed work is documented
