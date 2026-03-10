---
area: reference
type: reference
---

# Code-Writer

Implements features, fixes bugs, writes tests. The hands-on builder.

## Permissions

| Access | Paths |
|--------|-------|
| Write | source (`Commands/**`, `Services/**`, `Models/**`, `Rules/**`, `Utils/**`, `Serialization/**`, `Program.cs`), `Templates/**`, `DynaDocs.Tests/**`, agent workspace |
| Read | all |

## Relationships

- Dispatches to **reviewer** when implementation is done
- Cannot review its own code (guard-enforced, see `CanTakeRole`)

## See Also

- Mode file: `agents/{name}/modes/code-writer.md`
