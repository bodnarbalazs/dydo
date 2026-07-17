---
area: project
type: context
name: residue-hunt-handback
status: open
created: 2026-07-18
created-by: Claude (Fable)
---

# Residue Hunt — handback

Executes [residue-hunt-handoff](./residue-hunt-handoff.md) (DR-041 residue + the four
rulings of 2026-07-17). Five slices, one commit each, every gate green after each
(build 0 errors, full suite 0 failed, gap_check 100%):

| Slice | Commit | What died |
|---|---|---|
| 1 — DYDO_HUMAN | `f1c2248c` | env-var identity: ConfigService accessor, roles-reset gate, init `--name` + name prompt + export suggestions, help line, all test fixtures, README setup steps |
| 2 — role.json layer | `1ceeb1c6` | RolesCommand, the seven `*.role.json` files, RoleDefinitionService disk layer + permission map, role-file validation, init role-file generation; sync now discovers roles from `mode-*.template.md` (frontmatter: `description`, `emit`, `read-only`) — custom roles compile for the first time |
| 3 — agents/ workspace | `cc4ea266` | AgentRegistry + GetAgentsPath; `dydo/agents/workspace/` is the one shared scratch; guard warn-markers → `dydo/_system/.local/`; six agents/ special cases collapsed into the `agents/` scanExclude invariant (folder enumeration is now exclude-aware) |
| 4 — must-read key | `2a3c8e74` | `Frontmatter.MustRead` + parsing + fixtures + the ReadMustReadsAsync test shim |
| 5 — residue sweep | `80008c8d` | FileLock / ProcessUtils / FileReadRetry (zero production callers), the 33-file stale `_system/notion_sync/` shadow tree, dead `sprint-auditor`/`judge` model bindings; `reference/dydo-glossary.md` is now hash-tracked by `template update` |

## Definition of done — grep results

`DYDO_HUMAN` and `agents/{self}`: **zero hits** in Commands/Services/Models/Rules/Sync/Utils/tests.
`must-?read` survivors, each deliberate:

- `SyncCommand.ExtractMustReads` + friends — the LIVE mechanism compiling each role's
  context list from its mode template's `## Must-Reads` section. Not the dead key.
- Comments in GuardCommand / guard tests saying must-read *gating no longer applies* — history.
- `MarkdownParserTests.ExtractFrontmatter_IgnoresUnknownKeys` — pins that the retired key
  still parses harmlessly.

## Judgment calls

1. **Transitional metadata seed (slice 2).** `RoleDefinitionService.BaseRoleSeed` holds the
   base roles' description/emit/read-only until the frontmatter blocks land (below).
   Frontmatter always wins; once the prompt pass applies the blocks, **delete the seed** —
   it is marked as transitional in-code.
2. **Sync now reads project template overrides** (`ExtractMethodology`/`ExtractMustReads` go
   through `dydo/_system/templates/`), which the old sync silently ignored. Because of that,
   this repo's stale materialized copies had to go in slice 2 (they would have shadowed the
   wave-2 templates and resurrected sprint-auditor).
3. **`template update` only deletes stale templates it owns** (hash-tracked). An untracked
   `mode-*.template.md` is a user's custom role now — deleting it would kill the new feature.
4. **Sprint-auditor compiled artifacts** (`.claude/agents+skills`, `.codex`, `.agents`)
   were orphans of the wave-2 template deletion — removed. `inquisitor` is live
   (hand-authored agent, spawned by inquisition.js) — untouched, its model binding kept.
5. **Stale `dydo notguard` codex hook entry** removed (campaign disarm residue); sync's
   hook writer had already added the correct `dydo guard` entry beside it.
6. **Whole notion_sync shadow tree deleted**, not just the flagged project.md: all 33 files
   were unresolved pre-DR-041 hub conflicts whose external halves are machine-generated
   page listings (verified — no human content). DR-035 §4: the reconcile re-derives any
   real conflict deterministically.
7. **`agents/` exemption mechanism**: went with scanExclude (the handoff's "prefer one
   mechanism") and made `GetAllFolders` exclude-aware so folder rules ride the same list.
   `DocsTreeSync.ExcludedDirs` keeps its `agents` entry — that set defines the Notion
   mirror surface, not rule machinery.
8. **`dydo check` baseline**: 63 errors / 51 warnings before AND after the restructure —
   the debt is pre-existing prose-side drift, untouched here.

## Hand-over to the prompt-template session (their surface, exact edits)

1. **Frontmatter blocks — one per mode template** (then tell me / delete the seed per call 1).
   Replace each template's current `---\nmode: <name>\n---` block:

   | Template | block |
   |---|---|
   | mode-code-writer | `mode: code-writer` · `description: Implements features and fixes bugs in source code.` · `emit: agent` |
   | mode-reviewer | `mode: reviewer` · `description: Reviews code changes for quality and correctness.` · `emit: agent` · `read-only: true` |
   | mode-test-writer | `mode: test-writer` · `description: Writes and maintains test suites.` · `emit: agent` |
   | mode-docs-writer | `mode: docs-writer` · `description: Creates and maintains documentation.` · `emit: agent` |
   | mode-planner | `mode: planner` · `description: Creates implementation plans and task breakdowns.` · `emit: skill` |
   | mode-orchestrator | `mode: orchestrator` · `description: Coordinates multi-agent workflows and task dispatch.` · `emit: skill` |
   | mode-co-thinker | `mode: co-thinker` · `description: Collaborates on design decisions and architecture.` · `emit: skill` |
   | mode-chief-of-staff | `mode: chief-of-staff` · `description: The human's right hand — triages the backlog and idea funnel, routes work to domain orchestrators, reports status, and mediates between agents.` · `emit: skill` |

2. **DYDO_HUMAN mentions to delete** (sections/rows): `dydo/reference/about-dynadocs.md`
   (~86–95), `dydo/reference/configuration.md` (~66–75), `dydo/reference/dydo-commands.md`
   (~501–512) + `Templates/dydo-commands.template.md` (same lines),
   `dydo/guides/getting-started.md` (step 2, ~41–50), `dydo/guides/troubleshooting.md`
   (~59–74), `dydo/understand/agent-lifecycle.md` (line 18 — the whole doc looks
   pre-DR-041), `Templates/about-dynadocs.template.md` (~86–95).
3. **`must-read: true` lines to delete**: `dydo/guides/coding-standards.md:4`,
   `dydo/understand/about.md:4`, `dydo/understand/architecture.md:4`,
   `dydo/understand/documentation-model.md:69`, `Templates/about.template.md:4`,
   `Templates/architecture.template.md:4`, `Templates/coding-standards.template.md:4`.
4. **`dydo roles` command is gone** — remove its rows/sections from
   `dydo/reference/dydo-commands.md` + `Templates/dydo-commands.template.md`; document the
   new custom-role flow instead (drop `mode-<name>.template.md` into
   `dydo/_system/templates/`, run `dydo sync`).
5. **Divergences noticed** (yours to reconcile): `Templates/dydo-glossary.template.md`
   still carries the "Claim / release / dispatch…" entry the live
   `dydo/reference/dydo-glossary.md` dropped — `template update` would resurrect it.
   `reference/dydo-commands.md` is hash-mismatched (edited without re-hashing), so
   `template update` warns and skips it.
