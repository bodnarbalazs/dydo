---
area: guides
type: guide
---

# Writing Good Briefs

The self-containment bar for a Linear Issue, Project-plan lane, or prompt handed to a fresh agent. A
brief is good when another agent can execute and review it without reconstructing the author's private
conversation.

## Choose the right contract

One atomic, autonomous-ready Issue can be the reviewed contract. Coordinated, cross-cutting, or
architecture-sensitive work first receives a reviewed repository Project plan, then Issues that link its
exact governing commit. A mechanical checklist belongs inside an Issue; create Sub-issues only when the
children need independent ownership, status, dependencies, or evidence.

## Brief anatomy

1. **Outcome and context** — what must become true and why it matters.
2. **Scope and ownership** — exact files, systems, or responsibilities in bounds, plus explicit
   exclusions.
3. **Dependencies and references** — blockers, governing Decision, reviewed plan, and exact commit.
4. **Acceptance evidence** — observable behavior, test commands, review expectations, and artifacts to
   link back to the Issue.

The receiving agent starts with no memory of the shaping conversation. Avoid “as discussed,” implicit
file ownership, vague success such as “make it work,” or acceptance that exists only in someone's head.

## Keep runtime choices out of prose

Do not hard-code a model choice in a durable brief. Runtime configuration and the host platform own
model availability, permissions, and agent spawning. State the capability, independence, and evidence
the work requires; escalate a runtime limitation instead of preserving a temporary workaround in the
Issue.

## Link work without mirroring it

A Linear Issue links the relevant durable repository knowledge and its governing commit. Commits and PRs
reference the Issue key. New reusable knowledge flows back into a Decision, guide, plan, audit, or
assimilation brief. Do not create a Markdown copy of the Issue or copy Linear workflow fields into
frontmatter.

## Review check

Before execution, ask: could a fresh agent deliver this without making a product decision, and could an
independent reviewer determine pass or fail from the same text? If either answer is no, the brief is not
ready.

## Related

- [Work Model](../understand/work-model.md)
- [Linear Issue Lifecycle](../understand/task-lifecycle.md)
- [Coding Standards](./coding-standards.md)
- [dydo Glossary](../reference/dydo-glossary.md)
