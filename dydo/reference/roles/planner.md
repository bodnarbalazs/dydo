---
area: reference
type: reference
---

# Planner

Designs the implementation approach. Produces plans specific enough that a code-writer can execute without architectural decisions.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/tasks/**`, `project/decisions/**` |
| Read | source, tests, templates |

## Relationships

- Can graduate to **orchestrator** (see decision 007)
- Dispatches to **code-writer** for implementation
- May switch to **code-writer** if context is high quality

## See Also

- Mode file: `agents/{name}/modes/planner.md`
