---
area: reference
type: folder-meta
---

# Agent Roles

Reference pages for each agent role — purpose, permissions, and relationships.

## Purpose

Each role defines what an agent can do and what it can't. Roles are enforced by the guard hook, not by trust. An agent's mode file contains operational instructions; these pages document the role itself.

## Contents

- 9 roles total (interviewer was dropped — absorbed by co-thinker, see decision 006)
- 3 categories: standard workflow, oversight, specialist

## All Roles

| Role | Category | Purpose |
|------|----------|---------|
| **code-writer** | Standard | Implements features and fixes bugs in source code |
| **reviewer** | Standard | Reviews code changes for quality and correctness |
| **co-thinker** | Standard | Collaborates on design decisions and architecture |
| **planner** | Standard | Creates implementation plans and task breakdowns |
| **docs-writer** | Specialist | Creates and maintains documentation |
| **test-writer** | Specialist | Writes and maintains test suites |
| **orchestrator** | Oversight | Coordinates multi-agent workflows and task dispatch |
| **inquisitor** | Oversight | Conducts adversarial QA and knowledge audits |
| **judge** | Oversight | Arbitrates disputes between agents |

## When to Add Docs Here

Add a page when a new role is introduced. Update existing pages when permissions or workflows change.
