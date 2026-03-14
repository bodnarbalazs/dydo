---
area: reference
type: reference
---

# Audit System

The audit trail: how dydo records agent activity for replay and accountability.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- What gets audited (agent claims, role changes, dispatches, task transitions, file operations)
- Audit file format (JSON snapshots in dydo/_system/audit/)
- File naming convention (date + UUID)
- Audit commands:
  - dydo audit (replay visualization)
  - dydo audit --list (list sessions)
  - dydo audit --session <id> (session details)
- Compaction:
  - dydo audit compact
  - Baseline + delta compression
  - Year-based organization
- Use cases:
  - Debugging what an agent did
  - Reviewing agent behavior
  - Finding when a change was introduced
-->

---

## Related

- [Agent Lifecycle](../understand/agent-lifecycle.md)
- [CLI Commands Reference](./dydo-commands.md)
