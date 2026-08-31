---
mode: domain-modeling
description: The active discipline of naming things. Use when the codebase's terminology is under discussion, when a term needs writing or sharpening in the project glossary, or when a choice looks durable enough to become a Decision Record.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills domain-modeling at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Domain Modeling

Actively build and sharpen the project's domain model as you design. This is the **active**
discipline: challenging terms, inventing edge-case scenarios, and writing the glossary entry down
the moment it crystallises. Merely *reading* [`dydo/glossary.md`](../../../dydo/glossary.md) for
vocabulary is not this skill — that is a one-line habit any skill can do. This one is for when you
are changing the model, not just consuming it. Think stage: the co-thinker takes this as a step
mid-conversation and takes back what the session settled — the terms written, the record offered,
the choice still open.

## Where the model lives

Both homes are scaffolded in every project, so nothing is created lazily and there is no layout to
infer:

- [`dydo/glossary.md`](../../../dydo/glossary.md) — one glossary per project, the language and
  nothing else. Not a spec, not a scratch pad, and totally devoid of implementation detail.
- `dydo/project/decisions/` — one Decision Record per choice that passes the three-part test below.
  The word *decision* is reserved for these records; everything smaller stays a choice or a term.

## During the session

### Challenge against the glossary

When the human uses a term that conflicts with the existing language in the glossary, call it out
immediately. "Your glossary defines 'cancellation' as X, but you seem to mean Y. Which is it?"

### Sharpen fuzzy language

When the human uses vague or overloaded terms, propose a precise canonical term. "You're saying
'account': do you mean the Customer or the Subscriber? Those are different things."

### Discuss concrete scenarios

When domain relationships are being discussed, stress-test them with specific scenarios. Invent
scenarios that probe edge cases and force the human to be precise about the boundaries between
concepts.

### Cross-reference with code

When the human states how something works, check whether the code agrees. If you find a
contradiction, surface it: "Your code cancels entire Orders, but you just said partial cancellation
is possible. Which is right?"

### Write the entry inline

When a term is resolved, write it into the glossary right there. Don't batch these up: capture them
as they happen.

- **Be opinionated.** When multiple words exist for the same concept, pick the best one and list the
  others on an `_Avoid_:` line under the entry — `_Avoid_: Purchase, transaction` beneath **Order**.
- **Keep definitions tight.** One or two sentences max. Define what it IS, not what it does.
- **Only terms specific to this project.** General programming concepts (timeouts, error types,
  utility patterns) don't belong even if the project uses them extensively. Before adding a term,
  ask: is this concept unique to this project, or a general programming concept? Only the former
  belongs.
- **Respect the ceiling.** The glossary holds fifteen to twenty terms; the rest of the documentation
  is discoverable by search. Adding one past the ceiling means arguing that another term is no
  longer among the most important — do that argument out loud, or don't add it.

## Offer a Decision Record sparingly

Only offer to record one when all three are true:

1. **Hard to reverse**: the cost of changing your mind later is meaningful
2. **Surprising without context**: a future reader will look at the code and wonder "why on earth
   did they do it this way?"
3. **The result of a real trade-off**: there were genuine alternatives and you picked one for
   specific reasons

If any of the three is missing, skip it. Easy to reverse and you will just reverse it; unsurprising
and nobody will wonder why; no real alternative and there is nothing to record beyond "we did the
obvious thing." What fails the test is not lost — if it is language, it is a glossary entry, and
anything else stays with the thinking that raised it.

### What qualifies

- **Architectural shape.** "We're using a monorepo." "The write model is event-sourced, the read
  model is projected into Postgres."
- **Integration patterns between subsystems.** "Ordering and Billing communicate via domain events,
  not synchronous HTTP."
- **Technology choices that carry lock-in.** Database, message bus, auth provider, deployment
  target. Not every library: just the ones that would take a quarter to swap out.
- **Boundary and scope choices.** "Customer data is owned by the Customer module; everything else
  references it by ID only." The explicit no-s are as valuable as the yes-s.
- **Deliberate deviations from the obvious path.** "We're using manual SQL instead of an ORM because
  X." Anything where a reasonable reader would assume the opposite. These stop the next engineer
  from "fixing" something that was deliberate.
- **Constraints not visible in the code.** "We can't use AWS because of compliance requirements."
  "Response times must be under 200ms because of the partner API contract."
- **Rejected alternatives when the rejection is non-obvious.** If you considered GraphQL and picked
  REST for subtle reasons, record it; otherwise someone will suggest GraphQL again in six months.

### Recording one

Scan `dydo/project/decisions/` for the highest existing number and increment it, then copy the shape
of the records already there: `NNN-slug.md`, `status` and `date` in the frontmatter, a body carrying
the context, the choice, its consequences and what it affects, and `dydo fix` afterwards so the
folder index picks it up. Keep it short — the value is in recording *that* the choice was made and
*why*, not in filling out sections. The alternatives you weighed earn their own section only when
the rejected ones are worth remembering.
