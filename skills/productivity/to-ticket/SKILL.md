---
name: to-ticket
description: Break a plan, spec, or this conversation into pickable Linear tickets, tracer-bullet slices wired with native blocking relations. Load only when the user asks.
---

<!-- Adapted from mattpocock/skills to-tickets at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# To Ticket

Break a plan, spec, or conversation into a set of **tickets**: tracer-bullet vertical slices, each declaring the tickets that **block** it.

The tracker is Linear. From the repository root, read the Ticket labels section of `dydo/reference/linear-workspace-standard.md`: it owns the vocabulary below.

Every ticket carries exactly one Type:

- `Feature`: add, improve, refactor or document; an outcome to build.
- `Bug`: restore intended behaviour.
- `Merge`: one merge operation.
- `Enablement`: access, environment, credentials or material other work needs.
- `Inquisition`: one round of audit of the integrated feature, area by area through Read, Break and Drive; Bugs filed.
- `Prototype`: a design question raised to fidelity the user can react to.
- `Question`: one prepared, discrete question whose answer blocks named work.
- `Research`: a factual answer whose investigation needs its own owner, status or evidence.
- `Grilling`: a tree of intent or specification choices resolved with the user.
- `Walkthrough`: the user walks the product as it stands, at a Milestone or the landing.

Every Type a captain holds also carries one Mode: `AFK`, the captain reaches reviewed completion without a live conversation; `HITL`, the user and the captain work the ticket together in its own top-level thread.

## Process

### 1. Gather context

Read the supplied intent and relevant references; fetch comments only for a named missing fact.
For shared multi-ticket behavior, read `dydo/reference/feature-spec-convention.md` from the repository root
and link the applicable spec sections.

### 2. Explore the codebase (optional)

If you have not already explored the codebase, do so to understand the current state of the code. Ticket titles and descriptions should use the project's domain glossary vocabulary, and respect Decision Records in the area you're touching.

Look for opportunities to prefactor the code to make the implementation easier. "Make the change easy, then make the easy change."

### 3. Draft vertical slices

Break the work into **tracer bullet** tickets.

<vertical-slice-rules>

- Each slice cuts a narrow but COMPLETE path through every layer (schema, API, UI, tests): vertical, NOT a horizontal slice of one layer
- A completed slice is demoable or verifiable on its own
- Each slice is sized to fit in a single fresh context window
- Any prefactoring should be done first

</vertical-slice-rules>

In a Project, group the slices into its Milestones: few and meaningful, the walking skeleton first, the landing last.

Give each ticket its Type, its Mode where a captain holds the Type, and its **blocking edges**: the other tickets that must complete before it can start. A ticket with no blockers can start immediately.

**Wide refactors are the exception to vertical slicing.** A **wide refactor** is one mechanical change (rename a column, retype a shared symbol) whose **blast radius** fans across the whole codebase, so a single edit breaks thousands of call sites at once and no vertical slice can land green. Don't force it into a tracer bullet; sequence it as **expand–contract**. First expand: add the new form beside the old so nothing breaks. Then migrate the call sites over in batches sized by blast radius (per package, per directory), each batch its own ticket blocked by the expand, keeping CI green batch to batch because the old form still exists. Finally contract: delete the old form once no caller remains, in a ticket blocked by every migrate batch. Regroup any batch that cannot pass alone until each batch does.

### 4. Quiz the user

Present the proposed breakdown as a numbered list. For each ticket, show:

- **Title**: short descriptive name
- **Type and Mode**
- **Milestone**: the one it belongs to, in a Project
- **Blocked by**: which other tickets (if any) must complete first
- **What it delivers**: the end-to-end behaviour this ticket makes work

Ask the user:

- Does the granularity feel right? (too coarse / too fine)
- Are the blocking edges correct: does each ticket only depend on tickets that genuinely gate it?
- Should any tickets be merged or split further?

Iterate until the user approves the breakdown.

### 5. Publish the tickets to Linear

Publish one ticket per approved slice in dependency order (blockers first), so each ticket's blocking edges can reference real keys, and wire every edge as Linear's native blocked-by relation. Create each ticket in `Todo` unless its Type's status rule says otherwise (an unconfirmed Inquisition starts in `Backlog`), unassigned, with its Type label, its Mode label where a captain holds the Type, its Milestone in a Project, and settled owned paths: pickable by construction once in `Todo`.

Set priority, per the standard's Priority section in `dydo/reference/linear-workspace-standard.md`, on a `Question` or HITL ticket it creates; leave AFK tickets without one.

Work the **frontier**: the open, unblocked, unassigned map tickets, as `dydo/reference/dydo-glossary.md` defines it. For a purely linear chain that means top to bottom.

Do NOT close or modify any parent ticket.

The `ticket-captain` that claims a ticket sharpens its contract, and asks a Question, or for a Grilling when the work is complex.

<ticket-template>

## Parent

A reference to the parent ticket (if the source was an existing ticket, otherwise omit this section).

## Outcome

The end-to-end behaviour this ticket makes work, from the user's perspective, not layer-by-layer implementation.

- [ ] Acceptance criterion 1
- [ ] Acceptance criterion 2

## Spec

Current feature spec and relevant sections, when applicable; otherwise omit.

## Owned paths

The paths this ticket may change, once settled.

## Blockers

- A reference to each blocking ticket, or "None (can start immediately)".

## Exact gates

The command for each test, suite and static gate that proves it.

## Base branch

The branch it starts from: the Project's feature branch, or `main` for an atomic ticket.

</ticket-template>

These are the standard's `Feature` sections; a Type with its own template there takes that one.

Name paths only as settled owned paths, and avoid code snippets: they go stale fast. Exception: if a prototype produced a snippet that encodes a decision more precisely than prose can (state machine, reducer, schema, type shape), inline it and note briefly that it came from a prototype. Trim to the decision-rich parts, not a working demo, just the important bits.
