---
area: project
type: context
---

# Main-project adoption of dydo 3

This record describes the local adoption boundary for dydo 3.0.0. Live PM belongs in Linear; Git and
dydo retain durable knowledge, reviewed plans, Decisions, and FutureFeatures.

The 3.0 candidate removes the local Notion provider, its watchdog, token and vault code, and the
consumerless external-data sync engine. It does not read, alter, archive, or delete remote Notion
content or local rollback stores.

Projects adopting dydo 3 should update their native artifacts with `dydo template update` and
`dydo sync`, validate with `dydo check`, and follow the [2.x to 3.x migration guide](../../guides/migrating-dydo-2x-to-3x.md).
Release, tag, and publication remain separate human acceptance actions.
