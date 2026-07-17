---
area: project
type: context
name: residue-hunt-handoff
status: open
created: 2026-07-17
created-by: Adele (Fable)
---

# Residue Hunt — handoff brief

You are hunting DR-041 residue: code that compiles and passes tests but belongs to the
deleted agent-roster runtime, plus three restructures balazs ruled on 2026-07-17. Read
[DR-041](../decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)
and [DR-042](../decisions/042-plan-first-implementation.md) first; the
[campaign handback](./simplification-campaign-handback.md) is the deletion history.

**Coordination:** a prompt-engineering pass (Rail B) is running in a parallel session.
DO NOT touch `Templates/*.md`, `Templates/*.js`, or `dydo/` prose docs — they are that
session's surface. Your surface is C# code, tests, and `dydo.json`-adjacent config.
Start only after the working tree is clean (`git status`); if it isn't, stop and ask.

**The ratchet (non-negotiable):** after each coherent slice — `dotnet build` 0 errors,
full suite 0 failed, `python DynaDocs.Tests/coverage/gap_check.py --force-run` all pass.
Kill orphaned `testhost.exe` if builds hit MSB3026 file locks. Commit per slice with a
clear message; balazs reviews by diff.

## Ruled work items (balazs, 2026-07-17)

### 1. Yank DYDO_HUMAN entirely — "it is a corpse"
- Remove: `ConfigService.HumanEnvVar`/`GetHumanName`, `RolesCommand`'s env-var human gate,
  `InitCommand`'s two "export DYDO_HUMAN" suggestions + `--name` plumbing that only feeds
  them, the `HelpCommand` env-var line, every test fixture setting it, and doc mentions
  (`dydo-commands.md` + template — coordinate: if the parallel session owns those files at
  the time, hand them the exact edit instead of making it).
- Human gating, where still wanted, is a **block nudge** (pattern on the command, message
  says "human-only"), not an identity ceremony. If you keep a gate for `roles reset`, that
  is the shape.

### 2. Delete the role.json layer — fold role metadata into template frontmatter (RULED)
Verified 2026-07-17: `dydo sync` compiles from hardcoded `GetBaseRoleDefinitions()` and
NEVER reads `dydo/_system/roles/*.role.json`. The disk files are read only by
`roles list/reset` and `dydo validate` — machinery that exists to manage the files
themselves. Field liveness: `writablePaths` matters only as `.Count == 0` (read-only tool
profile); `denialHint`/`canOrchestrate`/`base` have no living consumer; `roles create`
scaffolds custom roles that sync never compiles (a real defect). balazs ruled: prune.

Target shape:
- **The mode template IS the role.** Its frontmatter carries the metadata sync needs:
  `mode:` (name), `description:`, `emit: agent+skill | skill` (worker vs manager/skill-only
  — replaces the hardcoded WorkerRoles/Tier1ManagerRoles/SkillOnlyRoles lists), and
  `read-only: true` for the tool profile (replaces the WritablePaths.Count check).
  Coordinate frontmatter additions with the parallel prompt session (templates are their
  surface — hand them the exact frontmatter block per template; the C# parsing is yours).
- Sync discovers roles by enumerating `mode-*.template.md` — INCLUDING project-local
  overrides in `dydo/_system/templates/`, which makes custom roles "drop a template in the
  folder" and actually compile (fixes the defect).
- Delete: `RolesCommand` (whole command — list is answerable by sync output; create =
  copy a template; reset = `template update`), `RoleDefinitionService`'s disk layer +
  role.json schema fields with no consumer, `ValidationService`'s role.json checks, the
  eight `dydo/_system/roles/*.role.json` data files, and their tests. Keep model-tier
  binding exactly as is (`dydo.json` `models.roles`).
- Update command docs (`dydo-commands` roles section) — coordinate with the parallel
  session if they own the file at the time.
End state: sources are templates, `template update` manages them, `sync` compiles them —
two commands, one pipeline.

### 3. Restructure `dydo/agents/` → one shared workspace
- Today `dydo/agents/` exists only as the guard's warn-nudge marker directory
  (`AgentRegistry.WorkspacePath` → `ConfigService.GetAgentsPath`).
- Target shape: **`dydo/agents/workspace/`** — a single SHARED scratch folder where agents
  put temporary work products so they don't pollute the repo root. No per-agent
  subfolders.
- Move the guard's warn-nudge markers OUT of agents/ into `dydo/_system/.local/` (already
  scan-excluded and gitignored territory).
- Revisit the four `agents/` special-case exemptions (`Rules/FrontmatterRule.cs`,
  `Rules/HubFilesRule.cs`, `Rules/NamingRule.cs`, `Sync/Notion/DocsTreeSync.cs:201`):
  re-target them to exempt `dydo/agents/**` (scratch content is not documentation and
  never syncs to Notion) — or simplify if a single exclusion mechanism (scanExclude)
  covers it. Prefer one mechanism over four special cases.
- Init scaffolds `dydo/agents/workspace/` (+ keeps the `.gitignore` entry); `AgentRegistry`
  may finally die if the marker-dir accessor was its last consumer — check `GuardCommand`'s
  `registry.Config` use too (nudge loading may move to `ConfigService` directly).

### 4. Retire the dead `must-read` frontmatter key
Confirmed dead: `FrontmatterExtractor` parses `must-read:` into `Frontmatter.MustRead`,
which nothing reads; `types.json` doesn't list it. Remove the property + parsing + any
fixture usage. The `must-read: true` lines in live `dydo/` docs and prose templates belong
to the parallel session — hand them the list instead of editing.

## Smaller flags from prior sweeps (verify, then fix or close)

- `TemplateCommand.FrameworkDocFiles` does not include `dydo-glossary` (scaffolded via
  `FolderScaffolder` but not hash-tracked for `template update`) — wire it.
- `Sync/` model: `notionTitle: "Sprint Tasks"` deliberately still displays the old name —
  DO NOT change; it belongs to the upcoming Notion-engine pass (type is already `Slice`).
- `ProcessUtils` — re-verify every remaining member has a production caller.
- `dydo/_system/templates/` in THIS repo still contains stale override copies (e.g.
  `agent-workflow.template.md`, old mode templates) — stale overrides shadow the new
  embedded templates at compile time. Audit against current `Templates/`; delete stale
  copies (they are data, safe to remove).
- `dydo/_system/notion_sync/project.md` cache carries merge-conflict markers + a stale
  `sprint-tasks` link — machine-regenerated; verify it regenerates or delete the cache.

## Definition of done

- `grep -riE 'DYDO_HUMAN|agents/\{self\}|must-?read' Commands Services Models Rules Sync
  DynaDocs.Tests` returns only deliberate survivors, each justified in your report.
- All gates green; one commit per slice; a short handback note listing judgment calls.
