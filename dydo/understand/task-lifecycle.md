---
area: understand
type: concept
---

# Linear Issue Lifecycle

Actionable work lives in Linear Issues. This file keeps its historical path during the 3.0 migration so
existing durable links continue to resolve; it no longer defines a repository Task lifecycle.

## Shape the Issue

An implementation Issue states the outcome, relevant context, exact scope or owned paths, acceptance
criteria, gate commands, and dependencies. It links the governing Decision and exact plan commit when a
Project plan applies. One atomic Issue may carry the whole reviewed contract; larger coordinated work
uses a linked Project plan and several Issues.

Use a Sub-issue only when the child needs its own status, owner, dependency, or review evidence. A
checklist is enough for mechanical steps that cannot progress independently.

## Execute

Linear owns the Issue's workflow status, priority, assignee, blockers, and updates. The implementation
branch, worktree, agent session, commit, PR, and tests are evidence for that Issue, not additional work
records. The PR or commit references the Issue key, and the Issue links the exact governing commit before
work starts.

## Review and acceptance

Every implementation Issue receives independent agent review before human harmonization. Review
findings stay on the Issue while durable lessons are extracted to repository knowledge. When all Project
Issues are complete, an integrated audit evaluates the combined result against the linked Project plan;
the Project then records an assimilation brief before completion.

Linear status is the only delivery status. dydo does not copy it into frontmatter, infer it from Git, or
poll Linear.

## Related

- [Work Model](./work-model.md)
- [Writing Good Briefs](../guides/writing-good-briefs.md)
- [dydo Glossary](../reference/dydo-glossary.md)
