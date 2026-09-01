---
area: understand
type: concept
---

# Architecture Overview

This page maps the project's structure, component boundaries, and important data flows.

> **Fill this in.** Give agents a brief bird's-eye view of the technologies, boundaries, and data flow
> they need to change this project safely. Link to narrow details instead of copying them here.

---

## Project Structure

<!-- Show the real source, test, and durable-knowledge locations. Live work belongs in Linear. -->

```
project/
├── src/                  # Source code
├── tests/                # Test files
└── dydo/                 # Durable documentation and project knowledge
```

---

## Key Components

<!-- List the major components/modules and their responsibilities. -->

### Component A

*What this component does and which boundaries it owns.*

---

## Data Flow

<!-- Describe how data crosses components and external systems. -->

```
Input → Processing → Durable output
```

---

## Knowledge and Work Boundary

- **Linear** owns Initiatives, Projects, Issues, optional Milestones and Cycles, plus live status,
  priority, assignment, dependencies, updates, and review state.
- **Git/dydo** owns architecture, Decisions, reviewed Project plans, guides, audits, assimilation
  evidence, and changelog. Linear owns FutureFeatures with the rest of the work graph.
- Link between the two; do not mirror volatile Linear state into repository documents.

---

## Where to Find Things

| Looking for... | Location |
|----------------|----------|
| *[Type of code]* | `path/` |
| Live work and current execution state | Linear |
| Durable design rationale | `dydo/project/decisions/` |
| Reviewed coordinated-work contracts | `dydo/project/plans/` |

---

## Key Decisions

*Link to relevant Decision records in `project/decisions/` instead of re-deciding them here.*

---

## Related

- [dydo Glossary](../reference/dydo-glossary.md) — Work and knowledge vocabulary
- [Coding Standards](../guides/coding-standards.md) — Code conventions
