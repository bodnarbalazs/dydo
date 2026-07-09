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
- **One shared resolver:** a single display-resolution point (natural host:
  `Services/ArtifactProvenance.cs`, which both message and issue provenance already flow
  through) consumed by every provenance surface — issues, messages, agent list, whoami. Rule:
  show display-model when known; vendor ONLY as fallback when model is unknown.
- **whoami:** print Host and Model lines from the claimed session (`Commands/WhoamiCommand.cs`,
  Execute 22-102 — it already has registry access; `AgentSession.Host/Model` are populated).

## Files

- `Models/HookInput.cs` — model field (if the payload carries one).
- `Commands/GuardCommand.cs` — `InferModel` fallback chain (file follows c1-2's edits).
- `Models/ModelsConfig.cs` + `Services/ConfigFactory.cs` — display map (file follows c1-3's
  edits).
- `Services/ArtifactProvenance.cs` — shared resolver.
- `Services/MessageService.cs` (:56-59), `Commands/IssueCreateHandler.cs` (:134-139),
  `Commands/WhoamiCommand.cs`, agent list rendering (follow `ArtifactProvenance` consumers) —
  render through the resolver.
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
