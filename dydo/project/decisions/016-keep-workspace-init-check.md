---
type: decision
status: accepted
date: 2026-03-30
area: cli
---

# 016 — Keep `workspace init` and `workspace check`

## Context

`dydo workspace init` (bulk-creates agent workspace directories) and `dydo workspace check` (validates task/inbox state before session end) have zero usage across 250+ audit sessions. Their functionality is effectively built into the agent claim and release flows.

## Decision

Keep both commands. No changes.

## Rationale

- They're low-risk — no maintenance burden, no confusion in help output
- Possible edge use-cases for manual workspace setup or validation outside the normal agent flow
- Removing working, tested code risks breaking something for no meaningful gain
