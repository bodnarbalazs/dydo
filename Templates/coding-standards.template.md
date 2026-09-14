---
area: general
type: reference
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

## 5. Security

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

## 6. Testing

**Red before green, outside in.** Three claims prove a change, and each has one owner:

- A **scenario** claims behaviour at the product's boundary, in glossary words, with example tables
  where values vary. It is contract: the specifier writes it in Gherkin, the acceptance runner runs
  it, and implementation wires it without editing it.
- A **test** claims one seam inside, in the code's words. It is the implementer's, and comes and goes
  with refactors. Every non-trivial module has a test file; generated code and logic-free data types
  are the only exceptions.
- A **gate** is a command whose exit code proves what neither can state: the coverage bar, the
  mutation run, the docs check.

An acceptance criterion is a scenario when it can be one, else a gate.

Every module carries a **tier**. T1 is the default. T2 and T3 are declared by a comment in the first
ten lines of the module's test file, and the Issue's plan says which tier the work must meet.

| Metric | T1 | T2 | T3 |
|---|---|---|---|
| Line coverage | ≥ 80% | 100% | 100% |
| Branch coverage | ≥ 60% | ≥ 80% | 100% |
| CRAP score, per method | ≤ 30 | ≤ 15 | ≤ 5 |
| Edge cases | key ones | systematic | exhaustive and adversarial |

**CRAP** = CC² × (1 − coverage)³ + CC, on the method's cyclomatic complexity. At full coverage it is
pure complexity, so a red T3 forces decomposition rather than more tests. No `coverage:ignore`: if a
line is unreachable, delete it.

**Mutation testing** runs on the changed files only (Stryker's `--since`), and no mutant survives. A
survivor is a missing assertion or dead code; the fix is a sharper test or a deletion, never a lower
threshold. Acceptance mutation changes one example value at a time; a scenario still green marks a
step that asserts nothing.

The project's test runner, acceptance runner, coverage gate and mutation command are named in its
testing guide and in each Issue's gates.

---

## 7. Smells

Twelve shapes that make code worse than it needs to be (Fowler, _Refactoring_, ch. 3). Each reads what
it is → how to fix; the hardener works them, the reviewer judges by them.

- **Mysterious Name** — hides what it does or holds. → rename; no honest name means a murky design.
- **Duplicated Code** — one logic shape in two hunks or files. → extract it, call it from both.
- **Feature Envy** — a method using another object's data more than its own. → move it there.
- **Data Clumps** — the same fields always travelling together. → bundle them into one type.
- **Primitive Obsession** — a primitive standing in for a domain concept. → give it its own type.
- **Repeated Switches** — the same cascade on one type, twice. → polymorphism or a shared map.
- **Shotgun Surgery** — one change forcing scattered edits. → gather what changes together.
- **Divergent Change** — one file edited for unrelated reasons. → split it by reason.
- **Speculative Generality** — abstraction for needs the contract does not have. → delete it.
- **Message Chains** — long `a.b().c().d()` walks the caller depends on. → hide the walk.
- **Middle Man** — a unit that mostly delegates onward. → cut it; call the target direct.
- **Refused Bequest** — a subclass ignoring most of what it inherits. → compose instead.

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

## Related

- [Architecture](../understand/architecture.md) — Project structure

<!--
Add stack-specific standards as your project grows:
- guides/backend/_index.md — Backend patterns
- guides/frontend/_index.md — Frontend patterns
- guides/testing-strategy.md — test runner, coverage gate, mutation command, tier assignments
-->
