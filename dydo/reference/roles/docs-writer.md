---
area: reference
type: reference
---

# Docs-Writer

Writes and maintains project documentation. Keeps the dydo docs accurate and useful.

## Permissions

| Access | Paths |
|--------|-------|
| Write | `dydo/**` (except `agents/` of other agents), agent workspace |
| Read | source, tests |

## Relationships

- Dispatched by any role that needs documentation changes
- May dispatch to **reviewer** for doc review

## See Also

- Mode file: `agents/{name}/modes/docs-writer.md`
