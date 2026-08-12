---
title: Ship the three lean skills
sprint: lean-wayfinder-adoption
seq: 1
status: done
area: general
type: context
---

# Slice 1 — Ship the three lean skills

Add the three prompt-level skills and pin their framework distribution and runtime compilation.

## Spec fragment

Add `wayfinder`, `grilling`, and `bro` as concise skill-only templates. Prove fresh init, template
update, and Claude/Codex sync distribute them with no native agent definitions. No compiler change.

Acceptance: Wayfinder carries the optional active-Campaign map and execution invariants; grilling
elicits human decisions while agents find facts; bro re-pitches only the previous response in plain
technical English without dumbing it down. All three compile identically for Claude and Codex.

## Implementation detail

Create:

- `Templates/mode-wayfinder.template.md`
- `Templates/mode-grilling.template.md`
- `Templates/mode-bro.template.md`

Use frontmatter `mode: <name>`, a precise trigger `description`, and `emit: skill`. Wayfinder and
bro descriptions say they are for explicit human requests. Grilling says it is deliberately invoked
by a manager when intent/decisions need elicitation. Bodies must be concise and imperative; do not
place required behavior under `Must-Reads`, `Verify`, or other headings stripped by
`SyncCommand.ExtractMethodology`.

Wayfinder must state:

- active Campaign only; skip it for FutureFeatures and clear one-Sprint work;
- map is optional navigation, not implementation plan/PM hierarchy;
- Waypoint is not a Record/Slice and may point to evidence/Decision/Task/Sprint;
- delivery points to one Sprint, which alone decomposes into Slices;
- derive/select frontier, work one non-research Waypoint, record outcome once, redraw Fog/frontier;
- HITL remains in the current conversation; AFK may use bounded native discovery subagents;
- no top-level spawning, claims, runtime coordination, or implementation outside planner/workflow.

Grilling must separate facts (agent finds) from choices (human decides), ask one decision frontier at
a time with a recommendation and trade-off, avoid re-asking settled branches, and return a compact
resolved intent to its caller. Bro must expand unfamiliar abbreviations/local terms, preserve exact
technical content, avoid beginner analogies unless requested, and stop after a concise re-pitch.

Update focused tests:

- `DynaDocs.Tests/Services/TemplateGeneratorTests.cs`
- `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs`
- `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`
- `DynaDocs.Tests/Integration/InitCommandTests.cs`
- `DynaDocs.Tests/Integration/TemplateCommandTests.cs`
- `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs`
- `DynaDocs.Tests/Commands/SyncCommandTests.cs`

Pin 13 shipped `mode-*.template.md` files after the addition and 18 total results from
`TemplateGenerator.GetAllTemplateNames()` (13 mode templates plus 5 existing reviewer resources),
`EmitAgent == false`, old-project creation/hash tracking, both skill outputs, absent agent
definitions, LF/identical content, and the semantic invariants above. Do not change the generic
compiler description suffix in this slice.

## Out of scope for this slice

PM/glossary cross-references, public README/license/version, generated repository artifacts,
invocation metadata/compiler changes, and standalone supporting skills.

## Gate

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~TemplateGeneratorTests|FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~TemplateOverrideTests|FullyQualifiedName~InitCommandTests|FullyQualifiedName~TemplateCommandTests|FullyQualifiedName~CodexSyncArtifactsE2ETests|FullyQualifiedName~SyncCommandTests" --no-restore
```
