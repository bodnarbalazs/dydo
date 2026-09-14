---
area: project
type: folder-meta
---

# Pitfalls

Known gotchas and issues that catch people repeatedly. Quick reference, not tutorials.

## When to Document a Pitfall

Add a pitfall when:
- The same issue trips up multiple people
- A bug has a non-obvious cause
- Setup or configuration has hidden requirements
- A workaround exists for a framework limitation

## File Format

Filename: `kebab-case-problem-name.md` (e.g., `ef-migration-conflicts.md`)

Name by the problem, not the solution:
- `ef-migration-conflicts.md`
- ~~`how-to-fix-migrations.md`~~

Required sections:
- **Symptoms** - How do you know you hit this?
- **Cause** - Why does this happen?
- **Solution** - How to fix it
- **Prevention** - How to avoid it

---

## Related

- [Decisions](../decisions/_index.md) - Decisions that may have caused pitfalls
