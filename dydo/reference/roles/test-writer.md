---
area: reference
type: reference
---

# Test-Writer

Writes tests. Two dispatch contexts: standard test coverage (from code-writer) and hypothesis-driven testing (from inquisitor).

*Renamed from "tester" — reflects the actual job: writing tests, not manual QA.*

## Permissions

| Access | Paths |
|--------|-------|
| Write | `DynaDocs.Tests/**`, agent workspace |
| Read | source, templates |

## Relationships

- Dispatched by **code-writer** for standard test coverage
- Dispatched by **inquisitor** to prove/disprove hypotheses
- Dispatched by **judge** to gather evidence

## See Also

- Mode file: `agents/{name}/modes/test-writer.md`
