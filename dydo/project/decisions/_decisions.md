---
area: project
type: folder-meta
---

# Decisions

Decision Records documenting choices that required deliberation.

## When to Write a Decision Record

Write a decision record when:
- Choosing between viable alternatives (technical, product, or business)
- Making trade-offs with lasting consequences
- Reaching conclusions others might revisit or question later
- Changing an established approach

Skip if the answer is obvious or easily reversible.

## File Format

Filename: `NNN-kebab-case-title.md` (e.g., `001-postgres-over-mongo.md`)

Required frontmatter:
- `type: decision`
- `status: proposed | accepted | deprecated | superseded`
- `date: YYYY-MM-DD`
- `area: <category>` (optional, for filtering)
- `ratification: pending` (only on a `proposed` record a tribunal decided while the human was away;
  removed when he ratifies or overturns it)

## Status Values

- **proposed** - Under discussion
- **accepted** - Decision made, in effect
- **deprecated** - No longer recommended
- **superseded** - Replaced by another decision (link to it)

---

## Related

- [Pitfalls](../pitfalls/_pitfalls.md) - Known issues from past decisions
