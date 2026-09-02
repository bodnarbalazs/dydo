---
area: guides
type: guide
---

# Migrate from dydo 2.x to 3.x

dydo 3 keeps durable project knowledge in Git and uses Linear for live project management. It no
longer contains a local Notion provider, watchdog, token store, or external-data sync engine.

## Update a project

1. Upgrade dydo to 3.0.0 when the accepted release is available.
2. Keep active work, status, priority, assignment, and dependencies in Linear. Keep Decisions,
   plans, guides, release evidence, and FutureFeatures in the repository.
3. Run `dydo template update`, then `dydo sync`, to install the current native skills, agents, and
   workflows. `dydo sync` is the local template compiler; it does not synchronize an external PM
   system.
4. Run `dydo check` and resolve documentation findings before resuming normal work.

## Remove old configuration deliberately

Existing 2.x `dydo.json` files may still contain an unknown `notion` object. dydo 3 ignores it, so
the upgrade is safe without reading token or rollback data. After confirming no local rollback is
needed, remove that object manually. Do not delete remote Notion content or secret-bearing local
rollback stores as part of this upgrade.

Rename `models.roles` to `models.agents` in `dydo.json`; dydo 3 does not read the old key.

## What remains unchanged

FutureFeatures remain repository-native ideas. A human promotes one to a Linear Initiative, Project,
or Issue when it becomes live work. Claude Code and Codex continue to own runtime identity,
permissions, isolation, and native coordination.
