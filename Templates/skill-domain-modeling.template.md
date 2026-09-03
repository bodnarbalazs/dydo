---
name: domain-modeling
description: Build and sharpen a project's domain model. Use when discussing codebase terminology, writing or editing the glossary, or recording or editing a Decision Record.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills domain-modeling at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Domain Modeling

Actively build and sharpen the project's domain model as you design. This is the *active*
discipline: challenging terms, inventing edge-case scenarios, and writing the glossary and decisions
down the moment they crystallise. (Merely *reading* [`glossary.md`](../../../glossary.md) for
vocabulary is not this skill: that's a one-line habit any skill can do. This skill is for when
you're changing the model, not just consuming it.)

## Where the model lives

Both homes are scaffolded in every project, so nothing is created lazily:

- [`glossary.md`](../../../glossary.md): one glossary per project. Its header states the format and
  the ceiling.
- `dydo/project/decisions/`: one Decision Record per choice, governed by
  [`_decisions.md`](../../../project/decisions/_decisions.md) and shaped like the records already
  there.

## During the session

### Challenge against the glossary

When the human uses a term that conflicts with the existing language in the glossary, call it out
immediately. "Your glossary defines 'cancellation' as X, but you seem to mean Y. Which is it?"

### Sharpen fuzzy language

When the human uses vague or overloaded terms, propose a precise canonical term. "You're saying
'account': do you mean the Customer or the User? Those are different things."

### Discuss concrete scenarios

When domain relationships are being discussed, stress-test them with specific scenarios. Invent
scenarios that probe edge cases and force the human to be precise about the boundaries between
concepts.

### Cross-reference with code

When the human states how something works, check whether the code agrees. If you find a
contradiction, surface it: "Your code cancels entire Orders, but you just said partial cancellation
is possible. Which is right?"

### Update the glossary inline

When a term is resolved, update the glossary right there. Don't batch these up: capture them as they
happen. Use the format its header states.

The glossary should be totally devoid of implementation details. Do not treat it as a spec, a
scratch pad, or a repository for implementation decisions. It is a glossary and nothing else.

### Offer Decision Records sparingly

Only offer to create a Decision Record when all three are true:

1. **Hard to reverse**: the cost of changing your mind later is meaningful
2. **Surprising without context**: a future reader will wonder "why did they do it this way?"
3. **The result of a real trade-off**: there were genuine alternatives and you picked one for specific reasons

If any of the three is missing, skip it. Follow `_decisions.md`, number the file after the highest
existing record, and run `dydo fix` so the hub lists it.
