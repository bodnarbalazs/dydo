---
area: general
type: hub
---

# Documentation Index

The authoritative entry point for understanding and working with this codebase.

---

## For AI Agents

**Gather relevant context before starting work.**

### Traversal

1. **Start here** — Identify which section(s) below relate to your task
2. **Read linked docs** — Follow links until you have sufficient context
3. **Check the glossary** — For unfamiliar terms, see [Glossary](./glossary.md)
4. **Then explore code** — Docs give you the *why*; code gives you the *how*

**Stop reading** when you have enough context to proceed confidently, or when content becomes irrelevant to your task.

### When to Stop and Clarify

**Do not silently resolve ambiguity.** Stop and ask the user when:

- Two docs contradict each other
- A doc contradicts the user's explicit instructions
- Requirements are unclear or incomplete
- Multiple valid approaches exist and the choice matters
- The task scope differs from what was requested

User instructions take precedence over docs. But if docs contradict for good reason, surface the conflict rather than assuming.

---

## Documentation Sections

### [Understand](./understand/_index.md) — Domain & Architecture

Why things exist and how they fit together.

- Platform overview and purpose
- Core domain concepts
- System architecture and data flow

### [Guides](./guides/_index.md) — Implementation Patterns

How to build things correctly.

- [Backend patterns](./guides/backend/_index.md) — C#, .NET, APIs
- [Frontend patterns](./guides/frontend/_index.md) — React, TypeScript, styling
- [Microservices patterns](./guides/microservices/_index.md) — Python, FastAPI

### [Reference](./reference/_index.md) — Specifications

Technical details for quick lookup.

- API endpoint specifications
- Configuration options
- Internal tools documentation

### [Project](./project/_index.md) — Meta

How we work on this project.

- [Coding Standards](./project/coding-standards.md) — **Read before writing any code**
- Architecture decisions (ADRs)
- Known pitfalls
- Session changelog

---

## Quick Links

- [Coding Standards](./project/coding-standards.md)
- [Glossary](./glossary.md)
- [Architecture Overview](./understand/architecture.md)
