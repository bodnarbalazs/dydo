---
area: project
type: context
---

# dydo 3.0.0 — Linear PM and Notion runtime removal

dydo 3.0 removes the local Notion runtime and keeps live PM in Linear.

## Changed

- Linear is the live PM owner; dydo and Git retain durable documentation, Decisions, reviewed plans,
  release evidence, and FutureFeatures.
- Removed the local Notion provider, watchdog, configuration, vault/token code, and external-data
  sync engine.
- `dydo sync` remains the native compiler for shared Claude Code and Codex methods.
- Updated package metadata and release workflow validation for the 3.0 boundary.

## Upgrade

Follow [Migrate from dydo 2.x to 3.x](../../../../guides/migrating-dydo-2x-to-3x.md). This entry records
the candidate change; version tagging and publication require separate human acceptance.
