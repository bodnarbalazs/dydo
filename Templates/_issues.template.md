---
area: project
type: folder-meta
---

# Issues

Actionable work items with lifecycle tracking. Issues capture out-of-scope problems discovered during development or inquisition.

## Issue Lifecycle

1. **Open** — Created, needs attention
2. **Resolved** — Fixed, moved to `resolved/` subfolder

## How Issues Are Created

### Inquisition Pipeline (judge files directly)
inquisitor → test-writer → inquisitor → judge → **judge files the issue**

### Normal Development (human approval required)
Any agent encounters out-of-scope problem → proposes to human → human approves → agent files the issue

## Commands

- `dydo issue create --title "..." --area <a> --severity <s> [--found-by <f>]`
- `dydo issue list [--area <a>] [--status <s>] [--all]`
- `dydo issue resolve <id> --summary "..."`

## Severity Levels

- **Low** — Minor, no immediate impact
- **Medium** — Notable, should be addressed
- **High** — Significant, needs prompt attention
- **Critical** — Severe, blocking or dangerous

## Issues vs Pitfalls

| | Issues | Pitfalls |
|---|--------|----------|
| **Nature** | Actionable work items | Recurring knowledge |
| **Lifecycle** | Open → Resolved | Persistent |
| **Purpose** | "Fix X" | "Watch out for X" |

## Organization

Issues are numbered (0001, 0002...). Resolved issues move to the `resolved/` subfolder.

---

## Related

- [Pitfalls](../pitfalls/_index.md) — Known gotchas (different from issues)
- [Tasks](../tasks/_index.md) — Work tracking
