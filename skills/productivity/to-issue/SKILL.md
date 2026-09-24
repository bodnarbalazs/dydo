---
name: to-issue
description: Break a plan, spec, or this conversation into pickable Linear Issues, tracer-bullet slices wired with native blocking relations.
disable-model-invocation: true
---

<!-- Adapted from mattpocock/skills to-tickets at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# To Issue

Break a plan, spec, or conversation into a set of **Issues**: tracer-bullet vertical slices, each declaring the Issues that **block** it.

The tracker is Linear. From the repository root, read the Issue labels section of `dydo/reference/linear-workspace-standard.md`: it owns the vocabulary below.

Every Issue carries exactly one Type:

- `Feature`: add, improve, refactor or document; an outcome to build.
- `Bug`: restore intended behaviour.
- `Merge`: one merge operation.
- `Enablement`: access, environment, credentials or material other work needs.
- `Inquisition`: many read-only eyes on the integrated feature; Bugs filed.
- `Prototype`: a design question raised to fidelity the human can react to.
- `Question`: one prepared, discrete question whose answer blocks named work.
- `Research`: a factual answer whose investigation needs its own owner, status or evidence.
- `Grilling`: a tree of intent or specification choices resolved with the human.
- `Walkthrough`: the human inspects what landed.

Every Type a captain holds also carries one Mode: `AFK`, the captain reaches reviewed completion without a live conversation; `HITL`, the human and the captain work the Issue together in a session the human opened.

## Process

### 1. Gather context

Work from whatever is already in the conversation context. If the user passes a reference (a spec path, an Issue key or URL) as an argument, fetch it and read its full body and comments.

### 2. Explore the codebase (optional)

If you have not already explored the codebase, do so to understand the current state of the code. Issue titles and descriptions should use the project's domain glossary vocabulary, and respect Decision Records in the area you're touching.

Look for opportunities to prefactor the code to make the implementation easier. "Make the change easy, then make the easy change."

### 3. Draft vertical slices

Break the work into **tracer bullet** Issues.

<vertical-slice-rules>

- Each slice cuts a narrow but COMPLETE path through every layer (schema, API, UI, tests): vertical, NOT a horizontal slice of one layer
- A completed slice is demoable or verifiable on its own
- Each slice is sized to fit in a single fresh context window
- Any prefactoring should be done first

</vertical-slice-rules>

Give each Issue its Type, its Mode where a captain holds the Type, and its **blocking edges**: the other Issues that must complete before it can start. An Issue with no blockers can start immediately.

**Wide refactors are the exception to vertical slicing.** A **wide refactor** is one mechanical change (rename a column, retype a shared symbol) whose **blast radius** fans across the whole codebase, so a single edit breaks thousands of call sites at once and no vertical slice can land green. Don't force it into a tracer bullet; sequence it as **expand–contract**. First expand: add the new form beside the old so nothing breaks. Then migrate the call sites over in batches sized by blast radius (per package, per directory), each batch its own Issue blocked by the expand, keeping CI green batch to batch because the old form still exists. Finally contract: delete the old form once no caller remains, in an Issue blocked by every migrate batch. When even the batches can't stay green alone, keep the sequence but let them share an integration branch that all block a final integrate-and-verify Issue; green is promised only there.

### 4. Quiz the user

Present the proposed breakdown as a numbered list. For each Issue, show:

- **Title**: short descriptive name
- **Type and Mode**
- **Blocked by**: which other Issues (if any) must complete first
- **What it delivers**: the end-to-end behaviour this Issue makes work

Ask the user:

- Does the granularity feel right? (too coarse / too fine)
- Are the blocking edges correct: does each Issue only depend on Issues that genuinely gate it?
- Should any Issues be merged or split further?

Iterate until the user approves the breakdown.

### 5. Publish the Issues to Linear

Publish one Issue per approved slice in dependency order (blockers first), so each Issue's blocking edges can reference real keys, and wire every edge as Linear's native blocked-by relation. Create each Issue in `Todo`, unassigned, with its Type label, its Mode label where a captain holds the Type, and settled owned paths: pickable by construction.

Work the **frontier**: any Issue whose blockers are all done. For a purely linear chain that means top to bottom.

Do NOT close or modify any parent Issue.

The `issue-captain` that claims an Issue sharpens its contract, and asks a Question, or for a Grilling when the work is complex.

<issue-template>

## Parent

A reference to the parent Issue (if the source was an existing Issue, otherwise omit this section).

## Outcome

The end-to-end behaviour this Issue makes work, from the user's perspective, not layer-by-layer implementation.

- [ ] Acceptance criterion 1
- [ ] Acceptance criterion 2

## Owned paths

The paths this Issue may change, once settled.

## Blockers

- A reference to each blocking Issue, or "None (can start immediately)".

## Exact gates

The command for each test, suite and static gate that proves it.

## Base branch

The branch it starts from: the Project's feature branch, or `main` for an atomic Issue.

</issue-template>

These are the standard's `Feature` sections; a Type with its own template there takes that one.

Name paths only as settled owned paths, and avoid code snippets: they go stale fast. Exception: if a prototype produced a snippet that encodes a decision more precisely than prose can (state machine, reducer, schema, type shape), inline it and note briefly that it came from a prototype. Trim to the decision-rich parts, not a working demo, just the important bits.
