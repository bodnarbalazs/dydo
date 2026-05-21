---
area: general
type: changelog
date: 2026-05-21
---

# Task: backlog-folder-introduction-impl

Review the backlog/ and future-features/ port. Plan at dydo/agents/Charlie/plan-backlog-introduction.md. Phase 1 (code-writer) + Phase 2 (docs-writer) complete on master (uncommitted). Verify: (1) FolderScaffolder.cs adds project/backlog + project/future-features folders and meta files; (2) TemplateGenerator.cs gains GenerateBacklogMetaMd + GenerateFutureFeaturesMetaMd; (3) RoleDefinitionService.cs adds dydo/project/backlog/** to code-writer, co-thinker, orchestrator, judge (no future-features grants); (4) WorktreeCommand.JunctionSubpaths adds both new folders; (5) Templates/_backlog.template.md ports LC content with Decision 023 reference; Templates/_future-features.template.md ports verbatim; (6) Templates/_project.template.md Contents list adds backlog/ and future-features/ between tasks/ and decisions/; (7) Four mode templates each gain exactly one locked one-liner per plan §Workflow-mode-file-wording; (8) Five test files updated per plan; all 4205 tests pass and gap_check is 100%; (9) dydo/_system/roles/{code-writer,co-thinker,orchestrator,judge}.role.json gain dydo/project/backlog/** in writablePaths; (10) dydo/project/decisions/023-backlog-doc-category.md covers both folders (scope expanded from LC's 035); (11) dydo/project/backlog/_backlog.md and dydo/project/future-features/_future-features.md present; (12) dydo/project/_project.md Contents list updated; (13) dydo check passes for my changes (pre-existing wikilink/orphan errors are out of scope). Notes: future-features/ folder already existed in this repo with 3 pre-existing items using type:concept — I left those untouched (per land-empty rule, applies to new seed items). backlog/_index.md created manually since dydo fix skips empty folders (only meta file).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review the backlog/ and future-features/ port. Plan at dydo/agents/Charlie/plan-backlog-introduction.md. Phase 1 (code-writer) + Phase 2 (docs-writer) complete on master (uncommitted). Verify: (1) FolderScaffolder.cs adds project/backlog + project/future-features folders and meta files; (2) TemplateGenerator.cs gains GenerateBacklogMetaMd + GenerateFutureFeaturesMetaMd; (3) RoleDefinitionService.cs adds dydo/project/backlog/** to code-writer, co-thinker, orchestrator, judge (no future-features grants); (4) WorktreeCommand.JunctionSubpaths adds both new folders; (5) Templates/_backlog.template.md ports LC content with Decision 023 reference; Templates/_future-features.template.md ports verbatim; (6) Templates/_project.template.md Contents list adds backlog/ and future-features/ between tasks/ and decisions/; (7) Four mode templates each gain exactly one locked one-liner per plan §Workflow-mode-file-wording; (8) Five test files updated per plan; all 4205 tests pass and gap_check is 100%; (9) dydo/_system/roles/{code-writer,co-thinker,orchestrator,judge}.role.json gain dydo/project/backlog/** in writablePaths; (10) dydo/project/decisions/023-backlog-doc-category.md covers both folders (scope expanded from LC's 035); (11) dydo/project/backlog/_backlog.md and dydo/project/future-features/_future-features.md present; (12) dydo/project/_project.md Contents list updated; (13) dydo check passes for my changes (pre-existing wikilink/orphan errors are out of scope). Notes: future-features/ folder already existed in this repo with 3 pre-existing items using type:concept — I left those untouched (per land-empty rule, applies to new seed items). backlog/_index.md created manually since dydo fix skips empty folders (only meta file).

## Code Review

- Reviewed by: Frank
- Date: 2026-05-19 12:11
- Result: PASSED
- Notes: All 13 brief items verified. FolderScaffolder adds both folders + meta files; TemplateGenerator has both Generate*MetaMd; RoleDefinitionService grants backlog/** to code-writer/co-thinker/orchestrator/judge only (no future-features grants); WorktreeCommand.JunctionSubpaths adds both folders; templates match Decision 023; mode templates each gain exactly one locked one-liner; 4 role JSON files updated; Decision 023 covers both folders; _project.md Contents list updated; backlog/_backlog.md + _index.md + future-features/_future-features.md present. Tests: 4205 pass, gap_check 100%. dydo check on dydo/project/backlog is clean; remaining full-tree errors are pre-existing (issue 0186 anchor-only-link bug, unrelated inquisition wikilink) per the brief. Minor non-blocking nit: Templates/_project.template.md Contents list still omits issues/ while the project copy now lists it — pre-existing template gap, not introduced here.

Awaiting human approval.

## Approval

- Approved: 2026-05-21 19:06
