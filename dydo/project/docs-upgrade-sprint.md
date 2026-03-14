---
area: project
type: context
date: 2026-03-14
status: in-progress
---

# Documentation Upgrade Sprint

Bring dydo's own documentation up to date after the v1.2/v1.3 feature sprint. These docs serve as anchors for agents to understand what exists, what's half-baked, and what needs fixing.

---

## New Pages — Placeholders to Fill

### understand/

- [ ] `agent-lifecycle.md` — Claim, role, work, dispatch/release lifecycle
- [ ] `guard-system.md` — Hook enforcement, staged onboarding, permission model
- [ ] `dispatch-and-messaging.md` — Dispatch, inbox, messaging, wait — how agents talk
- [ ] `roles-and-permissions.md` — Role system conceptually, enforcement, custom roles
- [ ] `task-lifecycle.md` — Task create → in-progress → review-pending → approved/rejected
- [ ] `documentation-model.md` — JITI philosophy, folder conventions, hub/meta, frontmatter, doc graph
- [ ] `templates-and-customization.md` — Template system, include tags, template-additions
- [ ] `multi-agent-workflows.md` — Parallel agents, worktrees, orchestrator patterns

### guides/

- [ ] `getting-started.md` — First-time setup walkthrough
- [ ] `customizing-roles.md` — Creating custom roles, modifying permissions
- [ ] `writing-good-briefs.md` — How to write dispatch briefs agents can act on
- [ ] `troubleshooting.md` — Common errors, guard blocks, recovery patterns

### reference/

- [x] `guardrails.md` — Three-tier guardrail system (nudge / soft-block / hard rule) with all instances
- [ ] `configuration.md` — dydo.json schema, all config options
- [ ] `audit-system.md` — Audit trail format, compaction, replay visualization

---

## Existing Pages — Need Updating

### Stubs (need full content)

- [ ] `understand/about.md` — Currently near-empty, needs real project context
- [ ] `reference/roles/co-thinker.md` — Stub, needs full doc (like inquisitor/judge/orchestrator)
- [ ] `reference/roles/code-writer.md` — Stub
- [ ] `reference/roles/docs-writer.md` — Stub
- [ ] `reference/roles/planner.md` — Stub
- [ ] `reference/roles/reviewer.md` — Stub
- [ ] `reference/roles/test-writer.md` — Stub

### Review for accuracy (have content, may be outdated)

- [ ] `reference/roles/inquisitor.md` — Written during v1.3 sprint, review for accuracy
- [ ] `reference/roles/judge.md` — Written during v1.3 sprint, review for accuracy
- [ ] `reference/roles/orchestrator.md` — Written during v1.3 sprint, review for accuracy

### Outdated (need review + update)

- [ ] `understand/architecture.md` — Decent but missing new features (dispatch, messaging, roles system, custom roles)
- [ ] `reference/about-dynadocs.md` — References dropped interviewer role, role table outdated, missing new features
- [ ] `reference/dydo-commands.md` — Review for completeness against current CLI
- [ ] `glossary.md` — Template only, needs real terms (nudge, dispatch, guard, etc.)
- [ ] `welcome.md` — Still boilerplate
- [ ] `project/v1.3-release.md` — Needs status update, some slices shipped

---

## Related

- [v1.3 Release Plan](./v1.3-release.md)
- [Writing Documentation](../reference/writing-docs.md)
- [How to Use These Docs](../guides/how-to-use-docs.md)
