---
area: general
type: backlog
status: resolved
created: 2026-07-08
created-by: Adele
resolved: 2026-07-09
resolved-by: c1-6-model-provenance
origin: balazs — "I would prefer to use the exact model at found-by-vendor on issues and other surfaces where it's listed so I don't want to see claude or chatgpt/openai, I want to see Opus 4.8 / Fable 5 / Gpt 5.5 / Gpt-5.6 Sol"
related-decisions: [037]
---

## Resolution (absorbed by c1-6, 2026-07-09)

Delivered by sprint slice `c1-6-model-provenance`:

- **Capture.** The guard captures a concrete runtime model in `GuardCommand.InferModel`: an
  explicit `model`/`dydo_model` field on the hook payload (what Codex already delivers) wins;
  otherwise, for a Claude session, the model id is read from the most recent assistant entry of
  the session transcript (`transcript_path`). Neither present ⇒ `unknown`, never guessed from a
  role default. The captured value flows through `ParseInput` into the claim's session-storage
  write, so a Tier-1 Claude claim now persists a concrete model.
  - The subagent `agent_type → frontmatter` leg from the original sketch was **dropped as
    unreachable**: Tier-2 worker calls are anonymous and never persist session state (they route
    through `HandleWorkerCall` before `ctx.Model` is used), so a frontmatter fallback would be
    dead wiring. Tier-2 provenance is out of scope here — workers do not create issues/messages
    under their own identity.
- **Display map.** `models.display-names` (id → human name) in the DR 028 models config, shipped
  defaults in `ConfigFactory.DefaultDisplayNames`. An absent or empty map resolves to the shipped
  defaults (same absent-section-defaults contract as `dispatch.codex`), so the mapping is live
  even for a `dydo.json` written before the key existed. Unknown ids pass through verbatim.
- **One resolver, resolved at the source.** `Services/ModelDisplay` is the single resolver;
  `Services/ArtifactProvenance.FromSession` applies it so issues, messages, reviews, and task
  records all render display names with zero consumer edits. `whoami` and `agent list` render via
  the same resolver. Rendering rule: display model when known; vendor only as a fallback when the
  model is unknown.
- **whoami** prints Host and Model lines from the claimed session.

Docs: `dydo/reference/configuration.md` (Model Display Names section) and the `dydo whoami`
output description in `dydo/reference/dydo-commands.md`.

# Exact-model provenance on display surfaces (not vendor names)

Provenance surfaces should show the exact model (Fable 5, Opus 4.8, Gpt 5.5) instead of a bare
vendor name; needs runtime model capture on the Claude side plus an id→display-name map.

## Current state (verified 2026-07-08)

The provenance fields already exist and are written — `found-by-agent/vendor/model` on issues
(IssueCreateHandler.cs:139), `from_vendor`/`from_model` on messages (MessageService.cs:55),
review/session provenance from today's slice B. Two gaps make them useless as balazs wants them:

1. **The model value is `unknown` for every Claude agent.** `HookInput` carries no model field
   (session_id / agent_id / agent_type only), and `AgentSession.Model` defaults to `"unknown"`.
   Codex DOES deliver it (Dexter's messages arrive `from_model: gpt-5-codex`) — asymmetry.
2. **No display mapping.** Raw ids (claude-fable-5, gpt-5-codex) are stored verbatim; balazs
   wants human names: `Fable 5`, `Opus 4.8`, `Gpt 5.5`, `Gpt-5.6 Sol` — and never a bare vendor
   (`claude`/`openai`) where a model can be shown.

## Work sketch

- **Capture (the real fix):** determine what the CURRENT Claude Code hook payload carries —
  if a model field exists in newer payloads, parse it into HookInput → session context. If not:
  fallback chain per caller type — Tier-2 subagents resolve via `agent_type` → compiled agent
  frontmatter `model:` (the actual binding); Tier-1 sessions via env/transcript if available;
  else keep `unknown`. Predecessor scout's rule stands: **model must be concrete runtime data,
  not guessed from role defaults** — the frontmatter fallback is acceptable only because for
  subagents the frontmatter IS the runtime binding (modulo `dydo model cap`, which rewrites it —
  conveniently keeping the fallback truthful under caps).
- **Display:** a model-id → display-name map (natural home: the DR 028 tier config, where model
  ids already live; unknown ids pass through verbatim). Rendering rule on ALL provenance
  surfaces (issues, messages, reviews, agent list): show display-model when known; vendor ONLY
  as fallback when model is unknown.
- **Consistency:** one shared resolver so issue/message/review/list surfaces can't drift.

## Notes

- Good early Codex worker-task candidate post-smoke: small, well-specified, test-shaped.
- Touches the doc-consistency surfaces only if flags change (should not).
