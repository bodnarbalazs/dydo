---
area: understand
type: concept
---

# Roles and Permissions

The role system: what roles are, how they define agent capabilities, and how permissions are enforced.

<!-- PLACEHOLDER — to be filled during docs upgrade sprint -->
<!--
Topics to cover:
- What a role is (mode file + permission set + behavioral constraints)
- Base roles vs custom roles
- Role definition files (.role.json in _system/roles/)
- How permissions map to file paths
- Role transitions and restrictions
  - Orchestrator: graduation-only (must have been co-thinker or planner first)
  - dispatch --wait restricted to oversight roles
- Custom roles: dydo roles create, the .role.json schema
- How the guard resolves permissions at runtime
- Role history tracking (TaskRoleHistory)
- Cross-reference to individual role pages
-->

---

## Related

- [Guard System](./guard-system.md)
- [Agent Lifecycle](./agent-lifecycle.md)
- [Role Reference Pages](../reference/roles/_index.md)
- [Customizing Roles Guide](../guides/customizing-roles.md)
