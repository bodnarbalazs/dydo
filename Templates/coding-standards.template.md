---
area: general
type: reference
---

# Coding Standards

Rules and conventions for writing code in this project.

---

## Core Doctrine

> **Perfection is attained, not when no more can be added, but when no more can be removed.**

> **Simplicity is the ultimate form of sophistication.**

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

### When Abstraction Is Right

Good abstractions emerge from observed patterns, not anticipated ones.

**The Rule of Three:** Consider extracting when a pattern appears three times. When it appears once, don't.

Signs an abstraction is justified:

- The same logic exists in 3+ places
- Changes to one instance always require changes to others
- The abstraction makes code *shorter*, not longer
- The abstraction has a clear, single responsibility

Signs an abstraction is premature:

- It exists for "flexibility" with no concrete use case
- It adds indirection without reducing complexity
- It's harder to understand than the original code
- You're building for requirements that don't exist yet

---

## Security

Security is not an afterthought. These practices are non-negotiable.

### Validate at Boundaries

Validate and sanitize at system boundaries:

- All user input
- All external API responses
- All file system operations
- All database inputs (use parameterized queries)

Once data passes a boundary check, trust it internally. Do not re-validate the same data in every function it passes through.

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

Files marked `// Locked, do not edit.` at the top require explicit user confirmation before modification.

### Generated Code

Never modify files in `generated/` directories. They are overwritten by tooling.

### No Backwards-Compatibility Hacks

When changing code, change it cleanly. Do not:

- Rename unused variables to `_var`
- Re-export removed types "for compatibility"
- Add `// removed` comments for deleted code
- Keep dead code paths "just in case"

If something is unused, delete it completely.

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
| Constants | `SCREAMING_SNAKE_CASE` or `PascalCase` (follow language convention) |
| Interfaces | Prefix with `I` (e.g., `IUserService`) — all languages, not just C# |

### Error Handling

**Do not add silent fallbacks for impossible states.**

If your type system and boundary validation guarantee something, trust that guarantee. Redundant defensive checks add noise and — worse — mask bugs by silently handling corrupted state.

```
✗ if (user == null) return;           // Silently masks a bug
✗ if (user == null) throw ...;        // Redundant if type guarantees non-null

✓ Use non-nullable types and let violations fail fast
✓ If you must assert an invariant, fail loudly — never silently return
```

The enemy of resiliency is not "missing null checks." It's **silent failure** — code that handles impossible cases by doing nothing, hiding bugs until they cause real damage downstream.

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

- [Documentation System](./docs-system.md) — Doc structure, naming, linking conventions
