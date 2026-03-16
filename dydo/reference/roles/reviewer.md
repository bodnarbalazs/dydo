---
area: reference
type: reference
---

# Reviewer

Reviews code for correctness, standards compliance, security, and unnecessary complexity. The quality gate — nothing ships without passing review.

## Category

Core role. Quality assurance — dispatched by a code-writer (or orchestrator) after implementation is complete. The reviewer never writes source code; it reads, evaluates, and either passes or fails the work.

## Permissions

| Access | Paths |
|--------|-------|
| Write | agent workspace only |
| Read | all (`**`) |

The reviewer's write access is intentionally minimal — only its own workspace (`dydo/agents/{self}/**`). It cannot edit source code, tests, or documentation. This enforces the separation between reviewing and implementing. The denial hint (N8) reads: "Reviewer role can only edit own workspace."

**Note:** If the reviewer discovers a pitfall, it should dispatch to a docs-writer or mention it in the review notes.

## Constraints

| # | Constraint | Description |
|---|-----------|-------------|
| H10 | No self-review | An agent that was `code-writer` on a task cannot become `reviewer` on the same task. Enforced via `role-transition` constraint in `.role.json`. This ensures fresh eyes on every review. |

The constraint is checked against `TaskRoleHistory` — even if the agent releases and reclaims, the history persists per task.

## Workflow

1. Read must-reads (about, architecture, coding-standards)
2. Read the brief — understand what was implemented and why
3. Review the changes against coding standards (general and stack-specific)
4. Run tests — verify they pass
5. Document findings clearly and specifically
6. Verdict: pass or fail (no middle ground)

### Review Checklist

- Code follows coding standards
- Logic is correct and handles edge cases
- Tests exist and are meaningful
- No security vulnerabilities introduced
- No unnecessary complexity
- Changes match the task requirements

### Strict Pass/Fail

There is no "pass with notes." If there are issues, it's a fail. "Pass" means the code is ready to ship as-is. This avoids the trap of accumulating "minor" issues that never get fixed.

## Dispatch Pattern

Uses the fresh agent model ([decision 005](../../project/decisions/005-fresh-agent-over-wait-for-feedback.md)).

**On pass:**
```bash
dydo review complete <task> --status pass --notes "..."
dydo inbox clear --all
dydo agent release
```

**On fail:** Dispatches to a *new* code-writer with specific fix instructions. The review feedback must be actionable — exact line numbers, specific issues, clear descriptions of what's wrong.

```bash
dydo dispatch --wait --auto-close --role code-writer --task <task> --brief "Review failed. Issues: [specific list]"
```

After 2 failed reviews, the task may be escalated to a fresh agent or to the human.

## Out-of-Scope Issues

If the reviewer discovers a bug or problem outside the current task during review, it proposes filing an issue to the human before acting. Issues are created with `--found-by review`.

## Design Notes

- The "fresh eyes" principle is central to the reviewer role. Decision 005 chose the fresh agent model partly because new agents approach fixes without the assumptions that caused the original bug — the same logic applies to why the reviewer must not be the same agent that wrote the code.
- The reviewer's workspace-only write access means it can take notes and draft feedback but cannot "just quickly fix" a one-line issue. This is deliberate: even small fixes bypass review if the reviewer makes them.
- The mode file describes the mindset as "Gandalf" — the reviewer's job is to say "you shall not pass" to bugs, security issues, dead code, and AI slop.

## Related

- [Code-Writer](./code-writer.md) — implements the code that gets reviewed
- [Decision 005](../../project/decisions/005-fresh-agent-over-wait-for-feedback.md) — fresh agent model for review feedback
- [Guardrails Reference](../guardrails.md) — H10 (no self-review), H1 (write permissions)
