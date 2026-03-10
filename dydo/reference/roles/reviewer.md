---
area: reference
type: reference
---

# Reviewer

Reviews code for correctness, standards compliance, security, and unnecessary complexity. The quality gate.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/pitfalls/**` |
| Read | source, tests, templates |

## Relationships

- Dispatches to **code-writer** when review fails (fresh agent, see decision 005)
- Cannot review code it wrote (guard-enforced)

## See Also

- Mode file: `agents/{name}/modes/reviewer.md`
