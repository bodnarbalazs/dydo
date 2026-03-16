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
- [x] `configuration.md` — Done (Frank, 2026-03-16, batch D)
- [x] `audit-system.md` — Done (Frank, 2026-03-16, batch D)

---

## Existing Pages — Need Updating

### Stubs (need full content)

- [x] `understand/about.md` — Done (Frank, 2026-03-16, batch D)
- [x] `reference/roles/co-thinker.md` — Done (Adele, 2026-03-14)
- [x] `reference/roles/code-writer.md` — Done (Charlie, 2026-03-14) — needs your review
- [x] `reference/roles/docs-writer.md` — Done (Dexter, 2026-03-14) — needs your review
- [x] `reference/roles/planner.md` — Done (Dexter, 2026-03-14) — needs your review
- [x] `reference/roles/reviewer.md` — Done (Charlie, 2026-03-14) — needs your review
- [x] `reference/roles/test-writer.md` — Done (Emma, 2026-03-14) — needs your review

### Review for accuracy (have content, may be outdated)

- [x] `reference/roles/inquisitor.md` — Reviewed by Emma, consistency edits applied — needs your review
- [x] `reference/roles/judge.md` — Reviewed by Emma, consistency edits applied — needs your review
- [x] `reference/roles/orchestrator.md` — Reviewed by Emma, consistency edits applied — needs your review

### Outdated (need review + update)

- [x] `understand/architecture.md` — Added guard, roles, dispatch, audit sections (Frank, 2026-03-16, batch D)
- [x] `reference/about-dynadocs.md` — Fixed role table, removed interviewer, added new commands (Frank, 2026-03-16, batch D)
- [x] `reference/dydo-commands.md` — Added inquisition coverage command (Frank, 2026-03-16, batch D)
- [x] `glossary.md` — Added 16 real project terms (Frank, 2026-03-16, batch D)
- [x] `welcome.md` — Replaced boilerplate with DyDo-specific content (Frank, 2026-03-16, batch D)
- [x] `project/v1.3-release.md` — Updated all slice statuses (Frank, 2026-03-16, batch D)

---

## Related

- [v1.3 Release Plan](./v1.3-release.md)
- [Writing Documentation](../reference/writing-docs.md)
- [How to Use These Docs](../guides/how-to-use-docs.md)
