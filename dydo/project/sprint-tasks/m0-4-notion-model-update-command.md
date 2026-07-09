---
title: m0-4 dydo notion model-update Command
blocked-by:
due:
needs-human: false
priority: High
sprint: m0-spine-types-completion
status: ready
work-type: feature
area: backend
type: context
---

# m0-4 `dydo notion model-update` Command

Sanctioned regen path for the live sync model — resolves issue 0252 (today only a human hand-copy
bridges `Templates/sync-model.template.json` → `dydo/_system/sync-model.json`, so template changes
never reach a provisioned board). Design fixed in DR 040 §6: **diff + confirm, never a blind
overwrite** — `SyncModelLoader`'s doc-comment philosophy ("the on-disk file is the single source
of truth a project edits") stands, so the command shows what would change and asks.

## Behavior

`dydo notion model-update [--yes]`
1. Resolve the template exactly as the auto-seed path does (project override under
   `dydo/_system/templates/` wins over the embedded resource — mirror `SyncModelLoader`'s seeding
   precedence; read that code, don't reinvent).
2. If the live file is missing → seed it (existing auto-seed behavior), report, done.
3. Otherwise print a unified diff (live → template render). No diff → "already current", exit 0.
4. With a diff: confirm interactively unless `--yes`. On confirm, overwrite the live file.
5. Always end with the provisioning note when a diff was applied: added/changed types need
   `dydo notion sync` (additive provision) or `dydo notion reset` (destructive re-mint); removed
   types are not auto-archived.
- The CLI writes `_system/` itself (like the auto-seed) — no guard conflict; the guard blocks
  agent tool-call edits, not dydo's own file I/O.

## Files

- `Commands/NotionCommand.cs` — `CreateModelUpdateCommand()`, registered alongside
  connect/reveal-token/sync/reset (see lines 25–29 for the pattern).
- `Sync/Model/SyncModelLoader.cs` — expose the template-render/seed internals the command needs
  (small refactor; keep the loader's public surface minimal).
- `Services/CompletionProvider.cs` — the `["notion"]` entry currently lists only `["sync"]`
  (stale); add `model-update` and, while there, the other existing subcommands.
- New tests: command behavior (seed-when-missing, no-diff, diff+confirm, `--yes`), template
  override precedence. Neighbor pattern: `DynaDocs.Tests/Sync/Notion/NotionResetTests.cs`.
- Doc surfaces per the 6-surface command rule (`CommandDocConsistencyTests` enforces — let it
  drive the list; includes `reference/dydo-commands.md` and help text). Mention the command in
  issue 0252's Resolution section and mark it resolved on land.

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py` — green, `CommandDocConsistencyTests` included.
- `gap_check --force-run` (Commands/ and Sync/ touched).

## Sequencing & ripple

- **Post-C1 by construction** (balazs 2026-07-09: C1 implements first and gates v2.0.7) — this
  slice's `NotionCommand.cs`/`CompletionProvider.cs` edits start only after C1 lands.
- If `CommandDocConsistencyTests` drags in README-family / `about-dynadocs` template-sourced
  surfaces (`Templates/**` clone-sync), report the ripple to Adele before landing — do not
  absorb it silently.

## Success criteria

Template edits reach the live model via one confirmed command; no hand-copy; issue 0252 resolved;
suite green.
