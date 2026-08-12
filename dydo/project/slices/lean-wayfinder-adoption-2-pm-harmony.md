---
title: Harmonize Wayfinder with dydo PM vocabulary
sprint: lean-wayfinder-adoption
seq: 2
status: done
blocked-by: [lean-wayfinder-adoption-1-skills]
area: general
type: context
---

# Slice 2 — Harmonize Wayfinder with dydo PM vocabulary

Make the shipped and local vocabulary-sensitive prompts describe one PM and execution model.

## Spec fragment

Make every vocabulary-sensitive shipped prompt/document and this repository's local work model
describe one underlying PM/execution model, using conditional references rather than context dumps.

Acceptance: Campaign/Sprint/Slice/Task keep their meanings; Waypoint is an orthogonal non-Record
navigation node; delivery maps to Sprint then Slices; current manager/native subagent topology is
explicit; downstream update and fresh-init routes reach the locked glossary.

## Implementation detail

Edit shipped sources:

- `Templates/dydo-glossary.template.md`: add these locked definitions, preserving the existing
  concise glossary style:
  - **Campaign** — one committed goal pursued across one or more Sprints. It may own an optional
    Wayfinding map when the route cannot responsibly be planned all at once.
  - **Wayfinding map** — an optional, low-resolution navigation overlay in an active Campaign. It
    is not an implementation plan or a second PM hierarchy.
  - **Waypoint** — a navigation node in a Wayfinding map, not a Record, Task, Sprint, or Slice. It
    may point to a Decision, evidence artifact, Task, or Sprint. A delivery Waypoint points to one
    Sprint; that Sprint alone decomposes into Slices.
  - **Frontier** — the Waypoints currently actionable because their prerequisites are resolved.
  - **Fog** — relevant Campaign uncertainty not sharp enough to become a Waypoint. It is neither
    backlog nor out of scope.
  - **HITL** / **AFK** — participation modes, not work types. HITL happens with the human in the
    current top-level conversation; AFK may use bounded native subagents and returns evidence to
    the manager.
  - **FutureFeature** — an unscheduled hypothetical, not committed work. Wayfinding begins only
    after human promotion into an active Campaign.
- `Templates/entry-point.template.md`: add exactly: “When work touches dydo records, planning,
  roles, skills, or workflows, consult `dydo/reference/dydo-glossary.md` and treat defined terms as
  locked. For project-domain terms, consult `dydo/glossary.md`.” Do not require unconditional
  reading.
- `Templates/index.template.md`: include Campaign among work records and route relevant work to the
  locked glossary; say Waypoint is not a Record.
- `Templates/how-to-use-docs.template.md`: add the locked dydo glossary to Key References. Copy the
  generated framework-owned content to `dydo/guides/how-to-use-docs.md`.
- `Templates/mode-co-thinker.template.md`: hypothetical idea -> FutureFeature; committed active,
  multi-increment fog -> Wayfinder; one stable increment -> planner. Grilling is a method, not a
  rename for co-thinking.
- `Templates/mode-chief-of-staff.template.md`: preserve human promotion/session authority and route
  committed foggy work to an active Campaign + Wayfinder.
- `Templates/mode-planner.template.md`: repair the compiled glossary link to
  `../../../dydo/reference/dydo-glossary.md`; from a delivery Waypoint plan only the visible Sprint,
  never Fog/the whole Campaign.
- `Templates/reviewer-resource-plan.template.md`: Campaign Fog is not a spec gap unless the current
  Sprint depends on it.
- `Templates/mode-orchestrator.template.md`: return an audited delivery result to the invoking
  manager; never choose the next Waypoint or spawn/co-ordinate top-level sessions.

Update project-owned local copies separately:

- `dydo/reference/dydo-glossary.md` equals the shipped template.
- `dydo/index.md` links both glossaries and briefly reflects the optional Campaign overlay.
- `dydo/understand/work-model.md` describes the map/topology without becoming a downstream source.

Update/add focused tests in:

- `DynaDocs.Tests/Services/FolderScaffolderTests.cs`
- `DynaDocs.Tests/Integration/InitCheckIntegrationTests.cs`
- new `DynaDocs.Tests/Commands/WayfinderHarmonyTests.cs`, which owns compiled planner/reviewer/
  manager semantic assertions and generated Markdown link resolution for this slice.

Prove scaffolded glossary semantics, fresh index links, planner compiled link resolution, delivery
increment boundary, and reviewer Fog behavior. Do not add a global vocabulary-vs-sync-model test
while the explicitly deferred schema contradiction exists.

## Out of scope for this slice

Sync-model statuses, first-class Waypoint storage/sync, README/license/version, generated runtime
artifacts, broad role rewrites, or historical record rewriting.

## Gate

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~FolderScaffolderTests|FullyQualifiedName~InitCheckIntegrationTests|FullyQualifiedName~WayfinderHarmonyTests" --no-restore
dydo check
```

## Result

PASS — the focused test gate passed, the shipped and local glossary files are byte-identical, and
`dydo check` reported 0 errors (13 existing orphan warnings).
