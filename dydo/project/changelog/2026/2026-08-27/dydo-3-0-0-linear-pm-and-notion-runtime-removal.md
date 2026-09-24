---
area: project
type: context
---

# dydo 3.0.0 — Linear PM and Notion runtime removal

dydo 3.0 removes the local Notion runtime and keeps live PM in Linear.

## Changed

- Linear owns live PM, including FutureFeatures; dydo and Git retain durable documentation, Decisions,
  reviewed plans, and release evidence.
- Removed the local Notion provider, watchdog, configuration, vault/token code, and external-data
  sync engine.
- Shared Claude Code and Codex methods are authored as native `skills/<category>/<name>/` folders.
  `setup-skills.mjs` exposes them through each host's discovery root; there is no compile step.
- Updated package metadata and release workflow validation for the 3.0 boundary.

## Upgrade

Install or update dydo, then copy `skills/`, `setup-skills.mjs`, and `THIRD-PARTY-NOTICES.md` from
the dydo repository into the project root, commit them, and run `node setup-skills.mjs`. The skills
do not ship with the package or `dydo init`.

## Related

- [Getting Started](../../../../guides/getting-started.md) — Current install and setup steps.
