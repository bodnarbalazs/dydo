---
area: project
type: process
---

# Process: Refactoring

Careful incremental changes. Emphasizes discussion before action and preserving behavior.

---

## Flow

```
[Refactoring Request]
       │
       ▼
┌─────────────────┐
│   Co-Thinker    │  ◄── Discuss scope with human
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Planner      │  ◄── Define incremental steps
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Code-Writer    │  ◄── Execute step-by-step
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Reviewer     │  ◄── Verify no behavior change
└────────┬────────┘
         │
       [Done]
```

---

## Phase 1: Co-Thinker

**Agent role:** `co-thinker`

```bash
dydo agent role co-thinker --task {refactor-name}
```

### Goal

Agree on scope and approach with human before any code changes.

### Discussion Points

1. **What are we refactoring?** — Be specific about boundaries
2. **Why are we refactoring?** — What problem does this solve?
3. **What should NOT change?** — Behavior, interfaces, performance
4. **What's the desired end state?** — What does "done" look like?
5. **How do we break this into safe increments?** — Each step shippable

### Invariants

Define what MUST remain unchanged:

- Public API behavior
- Test outcomes (same assertions, same results)
- Performance characteristics (if critical)
- External contracts (database schemas, wire formats)

### Output

Document the decision in `dydo/project/decisions/`:

```markdown
---
area: general
type: decision
status: accepted
date: {YYYY-MM-DD}
---

# Refactoring: {Name}

## Context

[Why we're doing this refactoring]

## Decision

[What we're changing and how]

## Invariants (Must Not Change)

- [Invariant 1]
- [Invariant 2]

## Consequences

- [Positive consequence]
- [Risk and mitigation]
```

### Complete

```bash
dydo dispatch --role planner --task {refactor-name} --brief "Scope agreed. See decision doc."
```

---

## Phase 2: Planner

**Agent role:** `planner`

```bash
dydo agent role planner --task {refactor-name}
```

### Goal

Define incremental, safe refactoring steps. Each step must be independently shippable.

### Key Constraint

**Each step must:**
- Be small enough to review easily (< 200 lines changed)
- Pass all existing tests
- Be independently deployable
- Not break anything if we stop here

### Output

```markdown
# Refactoring Plan: {Name}

## Goal

[What we're achieving]

## Invariants (Must Not Change)

- Public API behavior
- Test outcomes
- [Other constraints from decision doc]

## Steps

### Step 1: [Description]

**Changes:**
- [File]: [Change]

**Verification:**
- `dotnet test` passes
- [Specific check]

### Step 2: [Description]

**Changes:**
- [File]: [Change]

**Verification:**
- `dotnet test` passes
- [Specific check]

[... more steps ...]

## Rollback Plan

If issues arise at any step:
1. [How to safely revert]
2. [What to check]
```

### Complete

```bash
dydo task create {refactor-name}
dydo dispatch --role code-writer --task {refactor-name} --brief "Plan ready. Execute steps in order."
```

---

## Phase 3: Code-Writer

**Agent role:** `code-writer`

```bash
dydo agent role code-writer --task {refactor-name}
```

### Goal

Execute the refactoring plan step by step, verifying after each step.

### Rules

- **Execute ONE step at a time** — Don't combine steps
- **Run tests after EVERY step** — Not just at the end
- **If tests fail, STOP** — You changed behavior, that's a bug
- **Commit after each step** — Atomic, reviewable commits
- **No feature changes** — Refactoring only

### Key Principle

**Refactoring should not change behavior.**

If tests fail, you did one of two things:
1. Changed behavior (bad — fix it or rollback)
2. Found a bug in existing tests (document, discuss, don't just fix)

### Work

For each step in the plan:

1. Make the changes described
2. Run `dotnet test`
3. If pass: commit with message "Refactor: [step description]"
4. If fail: stop, investigate, don't proceed

### Exit Criteria

- All steps complete
- All tests pass
- Each step has its own commit

### Complete

```bash
dydo task ready-for-review {refactor-name} --summary "Refactoring complete. All steps executed, tests pass."
dydo dispatch --role reviewer --task {refactor-name} --brief "Ready for review. [N] commits, one per step."
```

---

## Phase 4: Reviewer

**CRITICAL: Must be a DIFFERENT agent than code-writer.**

**Agent role:** `reviewer`

```bash
dydo agent role reviewer --task {refactor-name}
```

### Goal

Verify the refactoring preserved behavior and improved the code.

### Refactoring-Specific Checklist

- [ ] **Behavior unchanged** — Tests still pass with same assertions
- [ ] **Code is actually cleaner** — Not just different
- [ ] **Each commit is atomic** — One logical change per commit
- [ ] **No feature changes snuck in** — Refactoring only
- [ ] **Invariants preserved** — Check the decision doc
- [ ] **Performance not degraded** — If applicable

### Red Flags

- Tests were modified (not just moved)
- Method signatures changed unexpectedly
- New dependencies added
- "While I was there..." changes
- Single giant commit instead of incremental steps

### If Pass

```bash
dydo review complete {refactor-name} --status pass --notes "Refactoring clean. Behavior preserved."
```

### If Fail

```bash
dydo dispatch --role code-writer --task {refactor-name} --brief "Review failed:
- [Issue 1]
- [Issue 2]" --to {original-human}
```

---

## Completion

After review passes:

1. **Update decision doc** with outcome
2. **Create changelog entry** noting the refactoring
3. **Release agent**

---

## The Refactoring Principle

> Change structure without changing behavior. Prove it with tests.

Refactoring is NOT the time to add features, fix bugs, or "improve" things. Those are separate tasks. Refactoring has one job: make the code better without changing what it does.

**Small steps. Constant verification. No surprises.**
