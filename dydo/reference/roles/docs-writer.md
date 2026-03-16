---
area: reference
type: reference
---

# Docs-Writer

Writes and maintains project documentation. Keeps the dydo docs accurate and useful.

## Category

Specialist role. Dispatched by any role that needs documentation created or updated. The docs-writer is the only role with broad write access across the entire `dydo/` documentation tree — other roles can only write to their workspace and task-specific paths.

## Permissions

| Access | Paths |
|--------|-------|
| Write | `dydo/**` (except other agents' workspaces), agent workspace |
| Read | source, tests |

No source code writes. The docs-writer reads code to understand it but documents in `dydo/` only. The broad documentation write scope is intentional — documentation tasks routinely span multiple folders (`understand/`, `guides/`, `reference/`, `project/`).

## Privileges

- May dispatch to **reviewer** for documentation review before release
- Can use `dydo check` and `dydo fix` to validate and auto-fix documentation issues

## Workflow

1. Read must-reads (about, how-to-use-docs, writing-docs — the three docs that establish documentation conventions)
2. Understand the task: read the inbox brief, task file, or user prompt
3. Read relevant existing documentation and source code to gather context
4. Write or update documentation following the conventions in [writing-docs.md](../writing-docs.md)
5. Validate with `dydo check` and fix issues with `dydo fix`
6. Complete: dispatch to reviewer if docs need review, or release directly

### Documentation Mindset

> Document what code cannot convey: decisions, domain knowledge, architecture, and the "why" behind complexity.

What's worth documenting: decisions, domain concepts, architecture, constraints, history, onboarding. If you can learn it by reading the code, don't write it down.

### Folder Responsibilities

| Folder | Purpose | When to Update |
|--------|---------|----------------|
| `understand/` | Domain concepts, architecture | New features, architectural changes |
| `guides/` | How-to instructions | New patterns, workflow changes |
| `reference/` | API specs, configs, roles | API changes, new config options |
| `project/decisions/` | Decision records | Significant technical decisions |
| `project/pitfalls/` | Known issues | Discovered gotchas |
| `project/changelog/` | Change history | After releases or major changes |

## Design Notes

- The docs-writer has the broadest write scope of any non-oversight role. This is deliberate — documentation tasks often require touching multiple folders in a single pass (e.g., adding an architecture doc, updating the guide, and linking from the reference).
- Must-reads include `writing-docs.md` (enforced by [H5](../guardrails.md)) because documentation conventions must be internalized before any writes. Inconsistent docs are worse than missing docs.
- The docs-writer can be dispatched by any role, making it a general-purpose support role. Common dispatchers: code-writers who finished implementation and need docs updated, planners who created decision records that need fuller write-up, orchestrators coordinating documentation sprints.

## Related

- [Writing Documentation](../writing-docs.md) — Documentation conventions and validation rules
- [Reviewer](./reviewer.md) — review dispatch target
- [Guardrails Reference](../guardrails.md) — H1 (role-based write permissions), H5 (must-read enforcement)
