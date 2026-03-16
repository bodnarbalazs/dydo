---
area: reference
type: reference
---

# Test-Writer

Writes and maintains automated tests. Dispatched in two contexts: standard test coverage (from code-writer) and hypothesis-driven testing (from inquisitor).

## Category

Specialist role. Always dispatched — never self-selected. Renamed from "tester" to reflect the actual job: writing tests, not manual QA.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, tests, `project/pitfalls/**` |
| Read | source |

Write access to pitfalls lets the test-writer document gotchas alongside the tests that exposed them, without routing through another agent.

## Privileges

- Reports findings back to the dispatching agent via `dydo msg`
- Can be dispatched by any role that needs test evidence (code-writer, inquisitor, judge)

## Workflow

1. Read dispatch brief — understand what to test and why
2. Read relevant source code and existing tests
3. Write tests targeting the specified scope
4. Run tests and document results
5. Report findings to the dispatching agent (pass/fail, findings, test files written)
6. Clear inbox and release

## Dispatch Contexts

The test-writer serves two distinct workflows:

- **Code-writer dispatch** — Standard test coverage. The code-writer implements a feature and dispatches a test-writer to cover it. Focus: happy paths, edge cases, regressions.
- **Inquisitor dispatch** — Hypothesis-driven testing. The inquisitor forms a hypothesis about a potential bug and dispatches a test-writer to prove or disprove it. Focus: targeted tests that confirm or refute specific claims.

The test-writer doesn't decide what happens with findings — it reports back. The dispatching agent (or a judge) decides next steps.

## Design Notes

- No constraints in `.role.json` — the role is straightforward and doesn't need transition guards.
- Write access to `project/pitfalls/**` is intentional: test-writers often discover gotchas worth documenting, and routing them through another agent just to write a pitfall file adds overhead.
- The role is read-only on source (H1) — it writes tests, not production code.

## Related

- [Code-Writer](./code-writer.md) — primary dispatcher for standard coverage
- [Inquisitor](./inquisitor.md) — dispatcher for hypothesis-driven testing
- [Guardrails Reference](../guardrails.md) — H1 (role-based write permissions)
