---
area: general
type: reference
---

# Coding Standards

Rules and conventions for writing code in this project.

---

## Core Doctrine

> **Perfection is attained, not when no more can be added, but when no more can be removed.**

Every line of code, every abstraction, every file must justify its existence.

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

If following a workflow mode (`--feature`, `--task`, etc.):

1. **Task exists** — Create or claim a task before starting
2. **Progress tracked** — Update task as you work
3. **Ready for review** — Mark task `review-pending` with summary
4. **Review passed** — Different agent reviews (you don't review your own code)
5. **Changelog created** — Document what changed and why
6. **Docs dispatched** — If docs needed, dispatch to docs-writer (you don't write docs for your own code)
7. **Human approved** — Task needs human review before closing

**Do not:**
- Skip the review step
- Write docs for code you wrote
- Mark tasks complete without human approval
- Edit files outside your role's permissions

**Run before finishing:**
```bash
dydo workflow check
```

This verifies compliance. Fix any issues before releasing.

See [Workflow](./workflow.md) for full details.

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

### Protected Files

Files marked `// Locked, do not edit.` require explicit user confirmation before modification.

### Generated Code

Never modify files in `generated/` directories. They are overwritten by tooling.

---

## Conventions

Strong preferences. Deviate only with explicit justification.

### Naming

| Element | Convention |
|---------|------------|
| Files/folders (docs) | `kebab-case` |
| Classes, interfaces, enums | `PascalCase` |
| Functions/methods | `camelCase` (JS/TS) or `PascalCase` (C#) |
| Variables | `camelCase` |
| Constants | `SCREAMING_SNAKE_CASE` or `PascalCase` |
| Interfaces | Prefix with `I` (e.g., `IUserService`) |

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

## Stack-Specific Standards

- [Backend Standards](./guides/backend/_index.md) — C#, .NET, API patterns
- [Frontend Standards](./guides/frontend/_index.md) — React, TypeScript, styling
- [Microservices Standards](./guides/microservices/_index.md) — Python, FastAPI, uv

---

## Related

- [Workflow](./workflow.md) — Multi-agent orchestration system
- [Documentation System](./docs-system.md) — Doc structure, naming, linking conventions
