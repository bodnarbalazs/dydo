---
area: general
type: reference
must-read: true
---

# Coding Standards

Rules and conventions for writing code in this project.

---

## Core Doctrines

> **Perfection is attained, not when no more can be added, but when no more can be removed.**

Every line of code, every abstraction, every file must justify its existence.

> **Whatever you do, do it right.**

We don't do "quick fixes" which generate technical debt. We go the extra mile, it will pay off with interest.

---

## The Anti-Slop Mandate

AI-generated code tends toward verbosity, over-abstraction, and "just works" solutions that become instant legacy. This is unacceptable.

**Reject code that:**

- Adds abstractions for hypothetical future requirements
- Creates helpers/utilities for one-time operations
- Wraps simple operations in unnecessary layers
- Adds error handling for impossible scenarios
- Includes comments that restate the obvious
- Uses verbose patterns when simple ones suffice

**Demand code that:**

- Solves the immediate problem directly
- Can be understood without documentation
- Has obvious data flow
- Uses the simplest construct that works
- Deletes more than it adds when refactoring

**The test:** If you remove something and nothing breaks, it shouldn't have existed.

---

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

---

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked
- No abstractions for single-use code
- No "flexibility" or "configurability" that wasn't requested
- No error handling for impossible scenarios
- If you write 200 lines and it could be 50, rewrite it

**The test:** Would a senior engineer say this is overcomplicated? If yes, simplify.

### When Abstraction Is Right

Abstractions emerge from observed patterns, not anticipated ones.

**Rule of Three:** Consider extracting when a pattern appears three times. Not before.

Signs an abstraction is justified:
- The same logic exists in 3+ places
- Changes to one instance always require changes to others
- The abstraction makes code *shorter*, not longer
- It has a clear, single responsibility

Signs an abstraction is premature:
- It exists for "flexibility" with no concrete use case
- It adds indirection without reducing complexity
- It's harder to understand than the original code

---

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting
- Don't refactor things that aren't broken
- Match existing style, even if you'd do it differently
- If you notice unrelated dead code, mention it — don't delete it

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused
- Don't remove pre-existing dead code unless asked

**The test:** Every changed line should trace directly to the user's request.

---

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

## 5. Workflow Discipline

**You are not done when the code works.**

Code-writing happens inside a workflow (`run-sprint` and kin). You are a Tier-2 worker on one slice; the workflow — not you — orchestrates the review loop and the merge (Decision 024, Decision 026). Your discipline:

1. **Work your slice only** — implement against the brief in the worktree/scratch space the workflow assigned you. Do not reach into other slices.
2. **Prove it green** — run the worktree-isolated test runner (`python DynaDocs.Tests/coverage/run_tests.py`) and satisfy the coverage gate before returning.
3. **Return structured output** — hand the workflow a structured result (what changed, test outcome, files touched). The workflow spawns the reviewer; you do **not** self-dispatch one.
4. **Address review feedback** — when the loop sends the slice back, the same worker context fixes the flagged issues and re-returns.
5. **Raise your hand, don't guess** — if the spec is ambiguous, contradicts the codebase, or you are thrashing on one root cause, set the raise-hand signal to escalate early instead of burning review rounds.

**Do not:**
- Merge your own slice or review your own code — the workflow's reviewer and merge step own that.
- Edit files outside your slice's scope. If you notice a problem elsewhere, flag it in your result; don't fix it.
- Mark work complete without the tests and coverage gate passing.
- Reintroduce worker-tier `dydo dispatch`/`claim`/`release` — that 1.0 machinery is gone (Decision 024).

---

## 6. Security

Security is not an afterthought. These practices are non-negotiable.

### Validate at Boundaries

Trust internal code. Validate at system boundaries:

- All user input
- All external API responses
- All file system operations
- All database inputs (use parameterized queries)

Once data passes a boundary check, don't re-validate in every function.

### Secrets

- Never commit secrets to version control
- Never log secrets, tokens, or credentials
- Use environment variables or secret management services
- Rotate compromised secrets immediately

### Common Vulnerabilities

Be vigilant against:

- **Injection** — SQL, command, template injection
- **XSS** — Escape output, use framework protections
- **CSRF** — Use tokens for state-changing operations
- **Broken auth** — Validate sessions, use secure cookies
- **Sensitive data exposure** — Encrypt at rest and in transit

When uncertain about security implications, stop and research or ask.

---

## Rules

Violating these causes real problems.

### One Type Per File

Each class, interface, or enum lives in its own file. Filename matches type name exactly.

```
✓ User.cs contains class User
✓ IUserService.cs contains interface IUserService

✗ Models.cs contains multiple classes
```

**Exception:** Frontend props interfaces may be co-located with their component.

### Generated Code

Never modify files in `generated/` directories. They are overwritten by tooling.

### Test Parallelism

xUnit assembly-wide parallelism is disabled in `DynaDocs.Tests`. Several code paths under test mutate process-global state — `Console.Out`/`Console.Error` capture, `ProcessUtils.IsProcessRunningOverride`, the working directory — so per-class isolation is not sufficient; running classes in parallel produces flakes that even an honest coverage gate cannot see. The cost is roughly 50s of sequential overhead on a ~3:13 baseline, which we accept. If a single class genuinely benefits from internal parallelism, mark it with `[Collection(...)]` and opt that collection back in explicitly — but first verify it touches none of the shared statics above.

```csharp
// DynaDocs.Tests/AssemblyInfo.cs
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
```

---

## Conventions

Strong preferences. Deviate only with explicit justification.

### Naming

As a general rule, the technology specific naming convention should apply.
PascalCase for C#, snake_case for python etc.

The specifics may be found under the platform specific coding-standards. 

### Error Handling

**Do not add silent fallbacks for impossible states.**

If your type system and boundary validation guarantee something, trust it. Redundant checks add noise and mask bugs by silently handling corrupted state.

```
✗ if (user == null) return;           // Silently masks a bug
✗ if (user == null) throw ...;        // Redundant if type guarantees non-null

✓ Use non-nullable types and let violations fail fast
```

### Comments

Write comments for **why**, never for **what**. If code needs a comment explaining what it does, rewrite the code.

```
✗ // Loop through users and check if active
✓ // GDPR compliance — inactive users must not appear in exports
```

---

### Delegation of code-writing

Sub-agents and workflows are **where code gets written** (Decision 026). Worker-tier `dydo dispatch` no longer exists — it was 1.0-era machinery removed by [Decision 024](../project/decisions/024-dydo-2-native-pivot.md).

- **Tier-1 named agents are managers, not implementers.** They run workflows (`run-sprint` and kin) and coordinate; they do **not** write code beyond the trivial-edit exception (typo fixes, single-line config toggles, doc-link repairs). Rule of thumb: *if it needs a reviewer, it needs a workflow.*
- **Tier-2 workers write the code.** Code-writers, reviewers, and test-writers are native sub-agents spawned by workflows, each with a scoped permission profile. The workflow — not the worker — orchestrates the code → review loop and the merge.

See [Work Model](../understand/work-model.md) for the full hierarchy and [Decision 026](../project/decisions/026-tier1-managers-doctrine.md) for the manager doctrine.

## Related

- [How to Use Docs](./how-to-use-docs.md) — Navigating the documentation
- [Architecture](../understand/architecture.md) — Project structure

<!--
Add stack-specific standards as your project grows:
- guides/backend/_index.md — Backend patterns
- guides/frontend/_index.md — Frontend patterns
-->
