---
title: c1-6 Exact-Model Provenance + whoami Host/Model
blocked-by: c1-2-durable-wait, c1-3-codex-posture
due:
needs-human: false
priority: Medium
sprint: c1-codex-adoption
status: ready
work-type: feature
area: backend
type: context
---

# c1-6 Exact-Model Provenance + whoami Host/Model

`backlog/exact-model-provenance-display.md` + 0254 item (5). The codex side is PROVEN (smoke:
Iris's message arrived with a concrete `from_model`); the remaining work is Claude-side capture,
an id→display-name map, and one shared resolver so no surface drifts. Plus: `dydo whoami` prints
no host/model.

## Behavior

- **Capture (the real fix):** determine what the CURRENT Claude Code hook payload carries — if a
  model field exists, parse it into `HookInput` → session context. If not, the backlog's
  fallback chain: Tier-2 subagents via `agent_type` → compiled agent frontmatter `model:` (the
  actual runtime binding, truthful under `dydo model cap`); Tier-1 via env/transcript if
  available; else keep unknown. **Model must be concrete runtime data, never guessed from role
  defaults.** Seam: `GuardCommand.ParseInput`/`InferModel` (GuardCommand.cs:135-227).
- **Display map:** model-id → display-name, home in the DR 028 models config
  (`Models/ModelsConfig.cs`; shipped defaults in `Services/ConfigFactory.cs`). Display names and
  id pairs come from the backlog record + the ids already in the tier config — not enumerated
  here (no model names in plan text, DR 039). Unknown ids pass through verbatim.
- **One shared resolver, resolved AT THE SOURCE:** display-name resolution happens inside
  `Services/ArtifactProvenance.cs` itself (`FromSession`), so every consumer that already flows
  through it — issues, messages, reviews, task records — gets display names with ZERO consumer
  edits. (Plan-review fix, 2026-07-09: `ReviewCommand.cs:174` and `TaskCreateHandler.cs:21` are
  provenance consumers owned by M1-S2a — map-at-render would have collided with a live parallel
  sprint.) Rule: display-model when known; unknown ids pass through verbatim; vendor ONLY as
  fallback when model is unknown.
- The two surfaces that do NOT flow through `ArtifactProvenance` render via the same resolver:
  `Commands/AgentListHandler.cs` and `Commands/WhoamiCommand.cs`.
- **whoami:** print Host and Model lines from the claimed session (`Commands/WhoamiCommand.cs`,
  Execute 22-102 — it already has registry access; `AgentSession.Host/Model` are populated).

## Files

- `Models/HookInput.cs` — model field (if the payload carries one).
- `Commands/GuardCommand.cs` — `InferModel` fallback chain (file follows c1-2's edits).
- `Models/ModelsConfig.cs` + `Services/ConfigFactory.cs` — display map (file follows c1-3's
  edits).
- `Services/ArtifactProvenance.cs` — resolve display names at the source (`FromSession`).
- `Commands/AgentListHandler.cs`, `Commands/WhoamiCommand.cs` — the two non-ArtifactProvenance
  surfaces; render via the same resolver.
- NOT touched (zero consumer edits by design): `Services/MessageService.cs`,
  `Commands/IssueCreateHandler.cs`, `Commands/ReviewCommand.cs`, `Commands/TaskCreateHandler.cs`
  — the latter two are M1-S2a's files.
- Tests: capture fallback chain (payload model, agent_type→frontmatter, unknown); resolver
  (known id → display, unknown id verbatim, vendor-only fallback); whoami output; provenance
  surfaces render display names.
- Docs: `dydo/reference/configuration.md` display-map key (after c1-3's posture keys). No new
  command; whoami output format isn't doc-consistency-enforced — grep reference docs for stale
  whoami output examples anyway.

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing

**After c1-2** (GuardCommand.cs chain) **and c1-3** (ConfigFactory.cs + configuration.md chain).

## Success criteria

New issues/messages from a Claude session carry a concrete model, rendered as a display name;
codex provenance renders display names too; `dydo whoami` shows host + model; one resolver, no
per-surface drift; backlog record `exact-model-provenance-display.md` closed. Suite green.
