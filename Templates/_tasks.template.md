---
area: project
type: folder-meta
---

# Tasks

Task tracking for work in progress. Tasks are created by agents when starting work and updated throughout the workflow.

## Task Lifecycle

1. **pending** - Created, work not started
2. **in-progress** - Work underway (set manually if needed)
3. **review-pending** - Ready for code review
4. **closed** - Task approved
5. **review-failed** - Task rejected, needs rework

## File Format

Tasks are created via `dydo task create <name> --area <area>`. Each task has:
- Frontmatter: area, name, status, created, assigned, updated
- Progress checklist
- Files Changed section (critical for debugging)
- Review Summary

## Organization

Tasks stay flat in this folder. Completed tasks can be archived or deleted after their changelog entry is written.

---

## Related

- [Changelog](../changelog/_index.md) - Where completed work is documented
