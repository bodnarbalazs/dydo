---
area: project
type: process
---

# Process: Code Review

Detailed checklist for reviewers. Used as a sub-process by all workflows.

---

## Reviewer Mindset

**You are:** A senior engineer with disdain for AI slop.

**Your job:** Catch what the author missed because they were "in the weeds."

**Fresh context is your superpower.** The author has been staring at this code for hours. You see it for the first time. Use that.

### What You're Looking For

- **Drift from intent** — Does it actually do what was asked?
- **Missing pieces** — What did they forget?
- **Over-engineering** — Is this more complex than needed?
- **Under-engineering** — Did they cut corners?
- **AI slop** — Generic, boilerplate-heavy, unnecessarily verbose code

### AI Slop Red Flags

Watch for these signs of low-quality AI-generated code:

- Excessive comments explaining obvious things
- Overly defensive null checks everywhere
- Try-catch blocks that swallow exceptions
- Unnecessary abstractions "for flexibility"
- Copy-paste patterns instead of proper reuse
- Verbose code that could be half the length
- "Enterprise" patterns in simple applications

---

## Pre-Review Setup

Before looking at any code:

### 1. Read the Task Description

What was supposed to be built? What problem does it solve?

### 2. Read the Plan (If Exists)

What was the intended approach? What files should change?

### 3. Form Expectations

What do you EXPECT to see? Then compare against reality.

**Do NOT read the implementation first.** Your fresh perspective is the whole point.

---

## Review Checklist

### Correctness

- [ ] **Logic is correct** — Does it actually do what it claims?
- [ ] **Edge cases handled** — Empty inputs? Nulls? Boundaries? Off-by-one?
- [ ] **Error handling** — Failures are caught and reported appropriately
- [ ] **Concurrency** — Race conditions? Thread safety? Deadlocks?
- [ ] **Resource cleanup** — Disposables disposed? Connections closed?

### Completeness

- [ ] **Plan items completed** — Every step in the plan is done
- [ ] **Tests exist** — And they test meaningful behavior, not just coverage
- [ ] **Tests pass** — Run `dotnet test` yourself, don't trust the author
- [ ] **Documentation updated** — If public API changed

### Standards

- [ ] **Naming conventions** — PascalCase for public, camelCase for private
- [ ] **Code style** — Matches project conventions
- [ ] **No magic numbers** — Constants or config where appropriate
- [ ] **DRY** — But don't over-abstract for one-time code
- [ ] **Single responsibility** — Methods/classes do one thing

### Security

- [ ] **No hardcoded secrets** — API keys, passwords, tokens, connection strings
- [ ] **Input validation** — User input is validated/sanitized
- [ ] **No SQL injection** — Parameterized queries, not string concatenation
- [ ] **Proper auth checks** — Authorization verified before sensitive operations
- [ ] **No sensitive data in logs** — PII, credentials, tokens

### Maintainability

- [ ] **Readable** — Can you understand it without the author explaining?
- [ ] **Not over-engineered** — Solves the problem, nothing more
- [ ] **Comments where needed** — Explain WHY, not WHAT
- [ ] **No dead code** — No commented-out code, unused functions
- [ ] **Appropriate abstractions** — Not too many, not too few

### Performance (If Applicable)

- [ ] **No obvious N+1 queries** — Database calls in loops
- [ ] **Appropriate data structures** — List vs Dictionary vs HashSet
- [ ] **No unnecessary allocations** — In hot paths
- [ ] **Async where appropriate** — I/O-bound operations

---

## Providing Feedback

### Be Specific

**Bad feedback:**
> "Fix the bugs."
> "Tests are incomplete."
> "Code needs cleanup."

**Good feedback:**
> "Line 45: Null check missing. `user` can be null when `GetUser` returns no results, which will throw NullReferenceException."
> "Missing test for empty input case. Add test: `GetUser_EmptyId_ThrowsArgumentException`"
> "Lines 23-45: This 20-line method does 3 things. Extract `ValidateInput()` and `TransformResult()` methods."

### Include Location

Always reference the specific location:
- File name
- Line number(s)
- Method/class name

### Categorize Issues

**Blocking** — Must fix before merge:
- Security vulnerabilities
- Incorrect logic
- Missing tests for new behavior
- Breaking changes to public API

**Non-blocking** — Should fix, but won't block:
- Minor style issues
- Naming suggestions
- Small improvements

**Nitpick** — Take it or leave it:
- Personal preference
- Micro-optimizations
- Formatting minutiae

### Be Constructive

Don't just say what's wrong. Say how to fix it.

**Less helpful:**
> "This is wrong."

**More helpful:**
> "This will fail when X. Consider using Y instead, which handles this case."

---

## Pass/Fail Criteria

### Pass If

- All **blocking** issues resolved
- Tests pass
- Code meets project standards
- Changes match stated intent
- No security vulnerabilities

### Fail If

- Any **blocking** issue remains
- Tests don't pass
- Security vulnerabilities present
- Significant deviation from plan without justification
- Core functionality missing

---

## After Review

### If Pass

```bash
dydo review complete {task-name} --status pass --notes "LGTM. [1-2 specific observations about what's good or notable]"
```

Good review notes:
> "LGTM. Clean error handling. Good test coverage for edge cases."
> "LGTM. Nice use of pattern matching. Consider extracting the validation logic in a future cleanup."

### If Fail

Send specific, actionable feedback:

```bash
dydo dispatch --role code-writer --task {task-name} --brief "Review failed.

Issues:
- [Blocking] src/Auth.cs:45 - Null check missing, will throw if user is null
- [Blocking] Missing test for empty ID case
- [Non-blocking] src/Auth.cs:23 - Consider renaming 'proc' to 'processUserAuth'" --to {original-human}
```

Send back to the **original author** — they have context and can fix faster.

### After 2 Failed Reviews

If the same code fails review twice:

1. **Escalate to human** — Something is wrong with the communication
2. **Consider fresh agent** — Maybe original author is stuck in a rut

```bash
dydo dispatch --role code-writer --task {task-name} --brief "Escalating after 2 failed reviews. Previous issues:
- [List of recurring problems]
Consider fresh perspective." --escalate
```

---

## The Reviewer's Principle

> Your fresh eyes are the last line of defense before this code reaches humans.

The author did their best. They've been staring at this code for hours. They're blind to its flaws. That's not a criticism — it's human nature.

**Your job is to see what they can't see.**

Be thorough. Be specific. Be kind but honest. The goal is better code, not hurt feelings.

**Trust but verify. Question everything. Ship nothing broken.**
