---
area: reference
type: reference
---

# Code-Writer

Implements features, fixes bugs, and writes tests. The hands-on builder that turns plans into working code.

## Category

Core role. Implementation — the agent picks this role when the task requires writing or modifying source code. Typically dispatched by a planner or orchestrator, but can also be chosen directly by the human.

## Permissions

| Access | Paths |
|--------|-------|
| Write | source (`Commands/**`, `Services/**`, `Models/**`, `Rules/**`, `Utils/**`, `Serialization/**`, `Program.cs`), `Templates/**`, `DynaDocs.Tests/**`, agent workspace |
| Read | all |

Full write access to source code, tests, and templates. Read access to everything including documentation. Cannot write to `dydo/**` (except its own workspace) — if docs need updating, dispatch to a docs-writer.

## Constraints

- No role-specific constraints in `.role.json` (empty `constraints` array)
- Standard guardrails apply: must-read enforcement (H5), role-based write permissions (H1), release blocking (H13–H16, H25)
- Cannot become reviewer on the same task after being code-writer (H10 — enforced on the reviewer side via `role-transition` constraint)

## Workflow

1. Read must-reads (about, architecture, coding-standards)
2. Read the plan — if dispatched for a task, the plan or brief is in the inbox
3. Understand relevant code before changing it
4. Implement the minimal code that solves the problem
5. Add or update tests for the changes
6. Run tests and verify they pass
7. Dispatch to reviewer with a brief summarizing the implementation

## Dispatch Pattern

Uses the fresh agent model ([decision 005](../../project/decisions/005-fresh-agent-over-wait-for-feedback.md)). After dispatching to a reviewer, the code-writer releases — it does not wait for review feedback. If the review fails, the reviewer dispatches to a *new* code-writer session with specific fix instructions.

```
code-writer → dispatch --no-wait --auto-close → reviewer → (if fail) → dispatch → new code-writer
```

The `--no-wait --auto-close` flags mean the code-writer releases immediately after dispatching (no wait registration). The reviewer inherits any `reply_required` obligation through the dispatch chain (baton-passing — see [decision 010](../../project/decisions/010-baton-passing-and-review-enforcement.md)). When the code-writer is dispatched (has an origin), H25 enforces that it must dispatch a reviewer before releasing.

## Out-of-Scope Issues

If the code-writer encounters a bug or problem outside its current task, it proposes filing an issue to the human before acting. Issues are created with `--found-by manual`.

## Design Notes

- The code-writer has the broadest write permissions of any role — source, tests, and templates — but zero documentation write access. This enforces separation of concerns: code-writers build, docs-writers document.
- No constraints on role transitions *from* code-writer. The constraint is on transitioning *to* reviewer after being code-writer on the same task (H10), which is defined in the reviewer's `.role.json`, not the code-writer's.
- The denial hint (N8) reads: "Code-writer role can only edit configured source/test paths and own workspace."

## Related

- [Reviewer](./reviewer.md) — dispatched after implementation
- [Planner](./planner.md) — often provides the plan the code-writer follows
- [Test-Writer](./test-writer.md) — alternative for test-only tasks
- [Decision 005](../../project/decisions/005-fresh-agent-over-wait-for-feedback.md) — fresh agent model for review feedback
- [Guardrails Reference](../guardrails.md) — H1 (write permissions), H10 (no self-review), H25 (review enforcement)
- [Decision 010](../../project/decisions/010-baton-passing-and-review-enforcement.md) — Baton-passing and review enforcement
