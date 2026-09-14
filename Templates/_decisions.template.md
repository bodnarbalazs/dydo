---
area: project
type: folder-meta
---

# Decisions

Decision Records documenting choices that required deliberation.

## When to Write a Decision Record

Write a decision record only when all three are true:
- **Hard to reverse**: the cost of changing your mind later is meaningful
- **Surprising without context**: a future reader will wonder "why did they do it this way?"
- **The result of a real trade-off**: there were genuine alternatives and you picked one for specific reasons

If any of the three is missing, skip it. What qualifies: architectural shape; integration patterns
between subsystems; technology choices that carry lock-in; boundary and scope choices; deliberate
deviations from the obvious path; constraints not visible in the code; rejected alternatives when the
rejection is non-obvious.

## File Format

Filename: `NNN-kebab-case-title.md` (e.g., `001-postgres-over-mongo.md`)

Required frontmatter:
- `type: decision`
- `status: proposed | accepted | deprecated | superseded`
- `date: YYYY-MM-DD`
- `area: <category>` (optional, for filtering)

## Status Values

- **proposed** - Under discussion
- **accepted** - Decision made, in effect
- **deprecated** - No longer recommended
- **superseded** - Replaced by another decision (link to it)

---

## Related

- [Pitfalls](../pitfalls/_index.md) - Known issues from past decisions
