---
area: reference
type: reference
---

# Inquisitor

Adversarial hypothesis-driven QA. Finds what reviewers didn't — the subtle bugs, untested paths, and silent assumptions in code that already passed review.

## Category

Specialist role. Dispatchable.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace, `project/inquisitions/**` |
| Read | source, tests, templates |

Source code is read-only. The inquisitor investigates — it doesn't fix.

## Privileges

- `dispatch --wait` — dispatches test-writers to prove hypotheses, judges to validate findings

## Workflow

1. Receive investigation scope (files, feature, subsystem)
2. Read code, check prior inquisitions for the area
3. Form testable hypotheses about what could go wrong
4. Dispatch test-writers to prove or disprove each hypothesis
5. Send confirmed findings to a judge for validation
6. Produce inquisition report at `project/inquisitions/{area}.md`

## Design Notes

- Quality over quantity. One confirmed finding beats ten speculative ones.
- Designed for human-scarce operation: dispatched and left to work autonomously, asking the human only when genuinely stuck.
- No constraints in `.role.json` — the role is straightforward dispatch-and-work.
- Source code is read-only (H1) — the inquisitor investigates, it doesn't fix.
- See decision 007 for full rationale.

## Related

- [Test-Writer](./test-writer.md) — dispatched to prove/disprove hypotheses
- [Judge](./judge.md) — dispatched to validate findings
- [Guardrails Reference](../guardrails.md) — H1 (role-based write permissions)
