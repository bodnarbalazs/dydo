---
area: project
type: process
---

# Process: Feature Implementation

The full workflow for implementing non-trivial features. Use this when complexity warrants careful planning and validation.

---

## Triggers

Use this process when **ANY** of the following are true:

### Explicit Triggers (User Says)

- "plan", "design", "figure out how to"
- "non-trivial", "complex", "careful"
- "think through", "let's discuss"

### Complexity Indicators

- Task touches **3+ files**
- Introduces **new patterns or abstractions**
- Estimated implementation time **> 30 minutes**
- Requires **architectural decisions**
- Affects **public APIs or interfaces**

---

## Flow

```
[Human Request]
       │
       ▼
┌─────────────────┐
│   Interviewer   │  ◄── Optional: Gather requirements if vague
│   (optional)    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Planner      │  ◄── Design approach, identify files, define steps
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Code-Writer    │  ◄── Implement the plan
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Reviewer     │  ◄── Fresh eyes validation (DIFFERENT agent)
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
  [Pass]   [Fail]
    │         │
    ▼         │
[Changelog]   │
    │         │
    ▼         │
[Human   ◄────┘
 Review]
```

---

## Phase 1: Interviewer (Optional)

**When to include:** Requirements are vague, multiple valid interpretations exist, or the human hasn't fully thought through what they want.

**Agent role:** `interviewer`

```bash
dydo agent role interviewer --task {task-name}
```

### Goal

Produce a clear, unambiguous requirements brief that captures the human's intent.

### Work

1. **Ask clarifying questions** — Don't assume, ask
2. **Identify constraints** — What must it do? What must it NOT do?
3. **Capture acceptance criteria** — How will we know it's done?
4. **Document edge cases** — What happens when things go wrong?

### Output

Write a requirements brief to your workspace:

```
dydo/agents/{agent}/brief-{task}.md
```

### Exit Criteria

Human confirms requirements are accurate and complete.

### Complete

When the human approves the brief:

```bash
dydo dispatch --role planner --task {task-name} --brief "Requirements captured. See brief-{task}.md"
```

---

## Phase 2: Planner

**Agent role:** `planner`

```bash
dydo agent role planner --task {task-name}
```

### Goal

Produce an implementation plan that makes code-writing mechanical. The plan should be detailed enough that a different agent can execute it without asking questions.

### Inputs

- Requirements brief (if exists)
- Codebase exploration (architecture, existing patterns)

### Work

1. **Explore the codebase** — Understand existing patterns
2. **Identify files to change** — Be specific
3. **Design the approach** — How will this work?
4. **Break into steps** — Each step should be verifiable
5. **Identify risks** — What could go wrong?

### Output

Write a plan to your workspace:

```
dydo/agents/{agent}/plan-{task}.md
```

**Plan structure:**

```markdown
# Plan: {Task Name}

## Approach

[2-3 sentences: what we're building and why this approach]

## Files to Modify

- `path/file.cs` — [what changes]
- `path/other.cs` — [what changes]

## Files to Create

- `path/new-file.cs` — [purpose]

## Implementation Steps

1. [Step description] — Verify: [how to verify]
2. [Step description] — Verify: [how to verify]
3. [Step description] — Verify: [how to verify]

## Tests to Add

- [ ] Test case 1: [description]
- [ ] Test case 2: [description]

## Risks & Mitigations

- **Risk:** [what could go wrong]
  **Mitigation:** [how to handle]
```

### Exit Criteria

- Plan is complete — no ambiguous steps
- All files identified
- Verification method for each step

### Complete

Create the task and dispatch:

```bash
dydo task create {task-name} --description "..."
dydo dispatch --role code-writer --task {task-name} --brief "Plan complete. See plan-{task}.md"
```

---

## Phase 3: Code-Writer

**Agent role:** `code-writer`

```bash
dydo agent role code-writer --task {task-name}
```

### Goal

Execute the plan. Implementation should be mechanical — the hard thinking was done in planning.

### Rules

- **Follow the plan step-by-step** — Don't skip, don't reorder
- **If plan is wrong, stop** — Dispatch back to planner, don't improvise
- **Don't make architectural decisions** — Those are planner's job
- **Run tests after each logical change** — Catch problems early
- **Keep commits atomic** — One logical change per commit

### Work

1. Read the plan from `dydo/agents/{planner}/plan-{task}.md`
2. Execute each step in order
3. Run `dotnet test` after each step
4. Note any deviations or issues

### Exit Criteria

```bash
dotnet test  # Must pass
```

All plan items completed. Tests pass.

### Complete

```bash
dydo task ready-for-review {task-name} --summary "Implementation complete. All tests pass."
dydo dispatch --role reviewer --task {task-name} --brief "Ready for review. Implementation follows plan."
```

**Important:** The reviewer MUST be a different agent. You cannot review your own code.

---

## Phase 4: Reviewer

**CRITICAL: Must be a DIFFERENT agent than code-writer.**

**Agent role:** `reviewer`

```bash
dydo agent role reviewer --task {task-name}
```

### Goal

Validate implementation with fresh eyes. You haven't been "in the weeds" — use that perspective.

### Mindset

**You are:** A senior engineer with disdain for AI slop.

**Your job:** Catch what the author missed. Question everything.

### Checklist

See [code-review.md](./code-review.md) for the full checklist. Key points:

- [ ] **All plan items completed** — Nothing skipped
- [ ] **Tests pass** — Run `dotnet test` yourself
- [ ] **Code follows standards** — Check coding-standards.md
- [ ] **No obvious bugs** — Logic errors, edge cases
- [ ] **No security issues** — Injection, auth, secrets
- [ ] **Changes match intent** — Does it actually do what was asked?
- [ ] **Not over-engineered** — Solves the problem, nothing more

### If Pass

```bash
dydo review complete {task-name} --status pass --notes "LGTM. [specific observations]"
```

Then create a changelog entry marked for human review.

### If Fail

Send specific feedback back to code-writer:

```bash
dydo dispatch --role code-writer --task {task-name} --brief "Review failed. Issues:
- [Blocking] Line 45: Null check missing, will throw if user is null
- [Blocking] Missing test for empty input case
- [Non-blocking] Consider renaming 'foo' to 'getUserById'" --to {original-human}
```

The original code-writer should fix the issues (they have context).

---

## Completion

After review passes:

### 1. Create Changelog Entry

Create a file in `dydo/project/changelog/`:

```markdown
---
area: general
type: changelog
date: {YYYY-MM-DD}
status: pending-human-review
---

# {Task Name}

## Summary

[1-2 sentences: what was built]

## Changes

- [Change 1]
- [Change 2]

## Files Modified

- `path/file1.cs`
- `path/file2.cs`

## Reviewed By

{Reviewer agent name}

---
Marked for human review.
```

### 2. Release Agent

```bash
dydo agent release
```

### 3. Human Review

Human reviews at their convenience. The changelog entry queues the decision.

**Human is the final gate.** Nothing ships without human sign-off.

---

## The Feature Implementation Principle

> Planning is cheap. Rework is expensive. Fresh eyes are invaluable.

The cost of a 30-minute planning session is nothing compared to the cost of building the wrong thing or missing a critical bug. The cost of having a second agent review is nothing compared to the cost of shipping broken code.

**Plan thoroughly. Implement mechanically. Review ruthlessly.**
