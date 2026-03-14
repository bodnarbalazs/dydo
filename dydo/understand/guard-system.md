---
area: understand
type: concept
---

# Guard System

How dydo enforces agent behavior through the PreToolUse hook. Every file operation passes through `dydo guard` before execution.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- How the hook intercepts tool calls (PreToolUse)
- Staged onboarding enforcement (claim → role → must-reads → work)
- Role-based permission checking
- Off-limits file enforcement (files-off-limits.md)
- Bash command analysis
- The three-tier guardrail system (→ reference/guardrails.md for the full catalog)
- Guard exit codes and error messages
- How other AI tools can integrate (stdin JSON, CLI args)
-->

---

## Related

- [Guardrails Reference](../reference/guardrails.md) — Full catalog of nudges, soft-blocks, and hard rules
- [Agent Lifecycle](./agent-lifecycle.md)
- [Roles and Permissions](./roles-and-permissions.md)
