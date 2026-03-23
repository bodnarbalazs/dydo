---
type: decision
status: accepted
date: 2026-03-23
area: project
---

# 013 — Conditional Must-Reads (Hardcoded)

Hardcode conditional must-read enforcement for merge workflows now; defer soft-coding to role JSON if more cases emerge.

## Problem

The must-read system currently enforces a static set of docs derived from mode file links with `must-read: true` frontmatter. Two new cases need docs that are only relevant conditionally:

1. **Merge code-writers** (`.merge-source` in workspace) need merge workflow guidance before they can write.
2. **Merge reviewers** (task name ends with `-merge`) need merge review guidance before they can complete review.

A static must-read list would force all code-writers and reviewers to read merge docs even when they're not doing merge work.

## Decision

### Hardcode conditional checks in MustReadTracker

Two checks in `ComputeMustReads()`:

- If role is `code-writer` and `.merge-source` exists in the agent's workspace → add `dydo/guides/how-to-merge-worktrees.md`
- If role is `reviewer` and task name ends with `-merge` → add `dydo/guides/how-to-review-worktree-merges.md`

### Why not soft-code in role JSON?

Decision 012 established the principle: "If a custom role would genuinely need to express the capability, it belongs in role JSON." A `conditionalMustReads` array in role JSON (e.g., `[{"marker": ".merge-source", "path": "..."}]`) would be cleaner and extensible. But we have exactly two cases, and the generic mechanism adds schema complexity, evaluation logic, and test surface for a pattern we haven't validated. Hardcoding is appropriate at this scale.

### Path to soft-coding

If a third conditional must-read case emerges, move all three to role JSON via a `conditionalMustReads` field on `RoleDefinition`. The hardcoded checks should have code comments noting this future path.

## Also in this batch

- **New docs:** `how-to-merge-worktrees.md` and `how-to-review-worktree-merges.md` as framework-owned guide templates (scaffolded on init, regenerated on update, with tests).
- **Task file brief injection:** `DispatchService` injects `--brief` content into the task file description at dispatch time, making task files useful for reviewers and improving changelogs by proxy.
- **Template updates:** Code-writer merge section moves to the new doc. Reviewer gets merge review guidance via conditional must-read. Orchestrator gets active intervention guidance.
- **Reviewer task file must-read:** All reviewers must read `dydo/project/tasks/<task>.md` before completing review, now that briefs are injected into task files.

## Implications

- `MustReadTracker.cs` gains two conditional checks with comments noting the soft-coding path.
- Two new template files in `Templates/`, generator methods in `TemplateGenerator.cs`, entries in `FolderScaffolder.DocFiles`.
- `DispatchService.cs` gains brief-to-task-file injection logic.
- Code-writer, reviewer, and orchestrator mode templates updated.
- Tests for: conditional must-read enforcement, doc scaffolding, brief injection, template regeneration.
