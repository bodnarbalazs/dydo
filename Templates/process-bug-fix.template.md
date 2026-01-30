---
area: project
type: process
---

# Process: Bug Fix

Simpler workflow for fixing bugs. May skip planning phase for trivial fixes.

---

## Flow

```
[Bug Report]
       │
       ▼
┌─────────────────┐
│  Investigation  │  ◄── Understand the bug (co-thinker mode)
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
  Simple    Complex
    │         │
    │    ┌────┴────┐
    │    │ Planner │
    │    └────┬────┘
    │         │
    └────┬────┘
         │
         ▼
┌─────────────────┐
│  Code-Writer    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Reviewer     │  ◄── DIFFERENT agent
└────────┬────────┘
         │
       [Done]
```

---

## Phase 1: Investigation

**Agent role:** `co-thinker`

```bash
dydo agent role co-thinker --task {bug-name}
```

### Goal

Understand the root cause and determine fix complexity.

### Questions to Answer

1. **What is the bug?** — Reproduction steps, expected vs actual
2. **What is the root cause?** — Not symptoms, the actual cause
3. **What files are affected?** — Scope of changes needed
4. **Are there related bugs?** — Same root cause elsewhere?
5. **What's the complexity?** — Simple or needs planning?

### Complexity Check

**Simple bug (skip to Code-Writer):**
- Root cause is clear
- Single file fix
- < 15 minutes estimated
- No risk of side effects

**Complex bug (include Planner phase):**
- Multiple files affected
- Root cause unclear or deep
- Fix might cause regressions
- Involves state management or data flow
- Performance implications

### Output

Document findings in your workspace:

```
dydo/agents/{agent}/investigation-{bug}.md
```

### Complete

**If Simple:**
```bash
dydo dispatch --role code-writer --task {bug-name} --brief "Simple fix. Root cause: [X]. Fix: [Y]."
```

**If Complex:**
```bash
dydo dispatch --role planner --task {bug-name} --brief "Complex bug. See investigation-{bug}.md"
```

---

## Phase 2: Planner (If Non-Trivial)

**Skip if:** Bug is simple (single file, clear fix, < 15 min, no side effects).

**Include if:**
- Multiple files affected
- Fix might cause regressions
- Root cause is deep or unclear
- Involves critical paths

**Agent role:** `planner`

```bash
dydo agent role planner --task {bug-name}
```

### Goal

Create a safe fix plan that won't introduce new bugs.

### Output

```markdown
# Fix Plan: {Bug Name}

## Root Cause

[1-2 sentences explaining the actual cause]

## Fix Approach

[How we'll fix it]

## Files to Modify

- `path/file.cs` — [what changes]

## Regression Risks

- [What could break]
- [How we'll prevent it]

## Test Plan

- [ ] Add regression test: [test that would have caught this]
- [ ] Verify existing tests still pass
- [ ] Manual verification: [steps]
```

### Complete

```bash
dydo task create {bug-name}
dydo dispatch --role code-writer --task {bug-name} --brief "Fix plan ready."
```

---

## Phase 3: Code-Writer

**Agent role:** `code-writer`

```bash
dydo agent role code-writer --task {bug-name}
```

### Goal

Fix the bug and add regression test.

### Requirements

1. **Fix the bug** — Address root cause, not symptoms
2. **Add regression test** — Test that would have caught this bug
3. **Don't refactor** — Fix the bug, nothing more
4. **Verify** — Run `dotnet test`

### Output

- Working fix
- Regression test
- Brief notes on what was changed

### Exit Criteria

```bash
dotnet test  # Must pass
```

Bug is fixed. Regression test exists and passes. No unrelated changes.

### Complete

```bash
dydo task ready-for-review {bug-name} --summary "Bug fixed. Regression test added."
dydo dispatch --role reviewer --task {bug-name} --brief "Ready for review."
```

---

## Phase 4: Reviewer

**CRITICAL: Must be a DIFFERENT agent than code-writer.**

**Agent role:** `reviewer`

```bash
dydo agent role reviewer --task {bug-name}
```

### Bug-Specific Checklist

- [ ] **Bug is actually fixed** — Test the reproduction steps
- [ ] **Regression test exists** — And it tests the right thing
- [ ] **Fix doesn't introduce new bugs** — Check edge cases
- [ ] **Fix is minimal** — No unrelated changes snuck in
- [ ] **Tests pass** — Run `dotnet test`
- [ ] **No "while I was there" changes** — Bug fix only

### Red Flags

- Fix is much larger than expected
- Multiple unrelated files changed
- Tests were modified (not just added)
- "Refactored" code around the bug

### If Pass

```bash
dydo review complete {bug-name} --status pass --notes "Fix verified. Regression test adequate."
```

### If Fail

```bash
dydo dispatch --role code-writer --task {bug-name} --brief "Review failed:
- [Issue 1]
- [Issue 2]" --to {original-human}
```

---

## Completion

After review passes:

1. **Create changelog entry** noting the bug fix
2. **Mark for human review** if the bug was customer-facing or critical
3. **Release agent**

---

## The Bug Fix Principle

> Fix the bug. Add a test. Nothing more.

Bug fixes should be surgical. The temptation to "clean up while you're there" leads to scope creep and new bugs. Save refactoring for a separate task.

**Minimal change. Maximum confidence.**
