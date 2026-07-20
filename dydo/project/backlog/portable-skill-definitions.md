---
area: general
type: context
status: open
created: 2026-07-08
created-by: Adele
origin: balazs — "what if in another project I wanted to add a skill/agent... would I have a way to define it in dydo then compile it to the target platform (claude code/codex)? We should have this feature so we don't have to manually edit and add the skills there."
related-decisions: [024, 037]
---

# Portable skill definitions — extend dydo sync beyond roles

Define skills once, vendor-neutral, under dydo and let `dydo sync` compile them to every
configured platform — the role pipeline already does this; non-role skills need the same.

## What exists today (verified 2026-07-08)

For **role-shaped** things the feature is already live: `dydo roles create my-role` scaffolds a
`.role.json` (+ mode template), and `dydo sync` dual-compiles every role into BOTH platforms —
Claude Code (`.claude/agents/<role>.md`, `.claude/skills/<role>/SKILL.md`) and Codex
(`.codex/agents/<role>.toml`, `.agents/skills/<role>/SKILL.md`) per DR 024. Model bindings come
from the DR 028 tier config per vendor. Define once, compile everywhere — for roles.

## The gap

Anything that is **not a role** has no dydo-side definition: task/workflow skills (run-sprint,
inquisition, deep-research-style harnesses), slash-command-like utilities, and the QA agents +
planner skill are either hand-authored per platform or hardcoded in dydo's emit set. A new
project wanting a custom skill today edits `.claude/skills/` by hand and has nothing for the
Codex side — exactly the manual per-platform editing balazs wants gone.

## Feature sketch (design round to refine)

- A vendor-neutral skill definition under dydo (e.g. `dydo/_system/skills/<name>.skill.md` or
  json+markdown pair): metadata (name, description, when-to-use, tool needs) + body.
- `dydo sync` compiles it to each configured platform's native shape alongside roles, honoring
  the DR 037 invariant (definitions vendor-free; vendor specifics resolved at compile time).
- Template-override mechanism reused (`dydo/_system/templates/`) so projects can customize.
- Open questions: workflows with platform-specific features (Claude Workflow scripts have no
  Codex twin — degrade or gate?); skill-vs-role boundary; whether the hardcoded emit set
  (QA agents, planner skill) migrates onto the same mechanism (dogfood).

## Interactions

- Mia's planner-role round (2026-07-08): if planner becomes a role, it rides the EXISTING
  pipeline — no dependency on this feature. But a plan-review *rubric skill* would be a natural
  first consumer.
- DR 036 R3 (dispatcher acceptance checklist into CoS/orchestrator skills) would also benefit —
  today that means editing per-platform skill artifacts.
