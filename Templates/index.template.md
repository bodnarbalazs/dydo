---
area: general
type: hub
---

# Documentation Index

The authoritative entry point for understanding and working with this codebase.

---

## For AI Agents

**Gather relevant context before starting work.**

### Workflow Mode

Your prompt may include a flag. Here's what each means:

| Flag | Mode | You Are | Read Next |
|------|------|---------|-----------|
| `--feature X` | Full workflow | Agent X | `.workspace/X/workflow.md` |
| `--task X` | Standard workflow | Agent X | `.workspace/X/workflow.md` |
| `--quick X` | Light workflow | Agent X | `.workspace/X/workflow.md` |
| `--inbox X` | Process inbox | Agent X | `.workspace/X/workflow.md` |
| `--review X` | Review mode | Agent X | `.workspace/X/workflow.md` |
| (none) | Ask human | - | Continue below |

**X is your agent letter:** A=Adele, B=Brian, C=Charlie, D=Dexter, E=Emma, F=Frank, G=Grace, H=Henry, I=Iris, J=Jack, K=Kate, L=Leo, M=Mia, N=Noah, O=Olivia, P=Paul, Q=Quinn, R=Rose, S=Sam, T=Tara, U=Uma, V=Victor, W=Wendy, X=Xavier, Y=Yara, Z=Zack.

**If you have a flag:** Read your workflow file at `.workspace/{YourName}/workflow.md`, then claim your identity:
```bash
dydo agent claim {YourName}
```

**If no flag:** Ask the human which workflow to follow, or proceed with ad-hoc assistance.

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

### Before Writing Code

1. Read [Coding Standards](./project/coding-standards.md)
2. Read [Workflow](./project/workflow.md) if following a workflow mode
3. Check [Active Tasks](./project/tasks/) for ongoing work

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
- [Workflow](./project/workflow.md) — Multi-agent orchestration system
- Architecture decisions (ADRs)
- Known pitfalls
- Session changelog

---

## Quick Links

- [Coding Standards](./project/coding-standards.md)
- [Workflow](./project/workflow.md)
- [Glossary](./glossary.md)
- [Architecture Overview](./understand/architecture.md)
- [Active Tasks](./project/tasks/)
