---
area: reference
type: folder-meta
---

# Agent Roles

Reference pages for each agent role — purpose, permissions, and relationships.

## Purpose

Each role defines what an agent can do and what it can't. Roles are enforced by the guard hook, not by trust. An agent's mode file contains operational instructions; these pages document the role itself.

## Contents

- **7 base roles**, split into Tier-1 managers (you claim and talk to) and Tier-2 workers (Claude-managed subagents), per [Decision 024](../../project/decisions/024-dydo-2-native-pivot.md).
- Plus **non-role agents/skills** that workflows use: `planner`, and the read-only QA agents `sprint-auditor` and `inquisitor`.

## Base Roles

| Role | Tier | Purpose |
|------|------|---------|
| **chief-of-staff** | Tier-1 manager | The human's right hand — triages, routes, reports, mediates |
| **co-thinker** | Tier-1 manager | Collaborates on design decisions and architecture |
| **orchestrator** | Tier-1 manager | Coordinates multi-agent workflows |
| **code-writer** | Tier-2 worker | Implements features and fixes bugs in source code |
| **reviewer** | Tier-2 worker | Reviews code changes for quality and correctness (read-only) |
| **test-writer** | Tier-2 worker | Writes and maintains test suites |
| **docs-writer** | Tier-2 worker | Creates and maintains documentation |

## Not Claimable Roles

`planner` is a planning-discipline skill a Tier-1 agent applies in its own thread. `inquisitor` and `sprint-auditor` are read-only agents that workflows spawn (campaign-end QA and whole-sprint audit). The old `inquisitor`/`judge` *roles* were retired in [Decision 024](../../project/decisions/024-dydo-2-native-pivot.md).

## When to Add Docs Here

Add a page when a new role is introduced. Update existing pages when permissions or workflows change.
