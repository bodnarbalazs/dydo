---
area: guides
type: guide
---

# Troubleshooting

Common errors, guard blocks, and recovery patterns.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- Guard blocks:
  - "No agent identity assigned" → dydo agent claim auto
  - "Read access denied" → must-reads not completed
  - "Cannot edit path" → wrong role for the file
  - "DYDO_HUMAN not set" → environment variable missing
  - "Already claimed" → another session has the agent
- Stuck states:
  - Agent won't release (pending waits, unprocessed inbox)
  - Dispatch fails (double-dispatch protection)
  - Wait never resolves (stuck waiting state)
- Recovery:
  - dydo whoami (check current state)
  - dydo agent status (check permissions)
  - dydo wait --cancel (clear stuck waits)
  - dydo clean (reset workspace)
- Platform-specific issues:
  - Windows terminal launch errors
  - macOS terminal spawning
- Validation errors:
  - dydo check failures and what they mean
  - dydo fix for auto-repair
-->

---

## Related

- [Guard System](../understand/guard-system.md)
- [Agent Lifecycle](../understand/agent-lifecycle.md)
- [CLI Commands Reference](../reference/dydo-commands.md)
