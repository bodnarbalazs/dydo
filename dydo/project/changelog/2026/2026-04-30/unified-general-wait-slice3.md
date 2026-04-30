---
area: general
type: changelog
date: 2026-04-30
---

# Task: unified-general-wait-slice3

Slice 3 of unified-general-wait initiative. Review the full surface that landed under this task: templates, tests, dydo.json frameworkHashes bump, and the published guide. Dispatcher: Adele (code-writer).

CONTEXT
- Decision doc: dydo/project/decisions/021-unified-general-wait.md
- Plan: dydo/agents/Olivia/plan-unified-general-wait.md, § 'Slice 3 — Template and workflow rewrites'
- The guide (dydo/guides/agent-general-wait.md) already has its own reviewer pass in flight via Dexter — Olivia confirmed. Defer guide review to that pass and focus on templates + tests + hash coherence.

WHAT TO REVIEW
1. Templates/mode-*.template.md (all 9 mode templates) — 'Register General Wait' step inserted right after the role step, with uniform wording per the plan's WORDING block. Verify wording matches verbatim and ordering is consistent across modes.
2. Templates/mode-orchestrator.template.md — § Dispatch dropped per-task wait registration and reframed --wait as a callee release-block; § Monitor dropped 'Keep a general wait open' subsection and reframes around dydo agent list + general wait surfacing replies; § Complete teardown wording adjusted.
3. Templates/mode-{code-writer,reviewer,docs-writer}.template.md — § Complete carries the verbatim --wait release-block one-liner ('Note: if your dispatcher used --wait, you cannot release until you have messaged them on this task. The release error names the expected subject.').
4. Templates/agent-workflow.template.md — Quick Reference synced: general 'dydo wait' surfaced first; --task form preserved for special cases; --cancel separated.
5. DynaDocs.Tests/Services/TemplateGeneratorTests.cs — new tests:
   - GenerateModeFile_AllModes_RegisterGeneralWaitStep (Theory, all 9 modes)
   - GenerateModeFile_AllModes_GeneralWaitStepFollowsRoleStep (ordering check between role/general-wait/verify)
   - GenerateModeFile_Orchestrator_DropsPerTaskWaitFromDispatch
   - GenerateModeFile_Orchestrator_DropsKeepGeneralWaitOpenSubsection
   - GenerateModeFile_Orchestrator_ReframesWaitAsReleaseBlockOnCallee
   - GenerateModeFile_DispatchedRoles_HaveDispatchWaitReleaseNote (Theory, code-writer/reviewer/docs-writer)
6. DynaDocs.Tests/Integration/TemplateOverrideTests.cs — new test Init_FrameworkHashes_MatchEmbeddedTemplateContent guards against false-positive override detection by asserting init-time hashes equal ComputeHash(ReadBuiltInTemplate(name)) for every framework template.
7. dydo.json frameworkHashes — 10 entries bumped to SHA256s of the new normalized embedded source. Hashes computed via the same NormalizeForHash + SHA256 that TemplateCommand.ComputeHash uses (BOM strip + CRLF→LF). On-disk dydo/_system/templates/* is still pre-change content (this matches the plan: post-ship dydo template update will sync on-disk to embedded; agent workspace regen is also explicitly out of scope per plan, deferred to dydo fix later). Confirm this divergence is acceptable per plan intent.

TEST STATUS
- Full suite: 3972/3972 passing (worktree-isolated runner, 3m6s).
- gap_check: FAIL on 3 modules — Services/TerminalLauncher.cs, Services/WindowsTerminalLauncher.cs, Services/LinuxTerminalLauncher.cs. These files were already modified in the working tree before I started (Rose's auto-resume work). I have not touched any Services/ file. Reporting per workflow guidance — please confirm this is unrelated to slice 3 and not something for the slice 3 reviewer to pursue.

OUT OF SCOPE (per plan)
- Source/service code (Slice 2)
- Regenerating dydo/agents/*/workflow.md or modes/*.md
- Regenerating dydo/_system/templates/* on-disk override files (deferred to post-ship dydo template update)
- Role JSON changes

The plan calls Slice 3's deliverable: every mode template gets the Register General Wait step, the orchestrator template's Dispatch/Monitor are rewritten, the three dispatched-role templates carry the release-block one-liner, the agent-workflow template is synced, the guide doc exists, the framework hashes are bumped, and the new tests cover the contract.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Slice 3 of unified-general-wait initiative. Review the full surface that landed under this task: templates, tests, dydo.json frameworkHashes bump, and the published guide. Dispatcher: Adele (code-writer).

CONTEXT
- Decision doc: dydo/project/decisions/021-unified-general-wait.md
- Plan: dydo/agents/Olivia/plan-unified-general-wait.md, § 'Slice 3 — Template and workflow rewrites'
- The guide (dydo/guides/agent-general-wait.md) already has its own reviewer pass in flight via Dexter — Olivia confirmed. Defer guide review to that pass and focus on templates + tests + hash coherence.

WHAT TO REVIEW
1. Templates/mode-*.template.md (all 9 mode templates) — 'Register General Wait' step inserted right after the role step, with uniform wording per the plan's WORDING block. Verify wording matches verbatim and ordering is consistent across modes.
2. Templates/mode-orchestrator.template.md — § Dispatch dropped per-task wait registration and reframed --wait as a callee release-block; § Monitor dropped 'Keep a general wait open' subsection and reframes around dydo agent list + general wait surfacing replies; § Complete teardown wording adjusted.
3. Templates/mode-{code-writer,reviewer,docs-writer}.template.md — § Complete carries the verbatim --wait release-block one-liner ('Note: if your dispatcher used --wait, you cannot release until you have messaged them on this task. The release error names the expected subject.').
4. Templates/agent-workflow.template.md — Quick Reference synced: general 'dydo wait' surfaced first; --task form preserved for special cases; --cancel separated.
5. DynaDocs.Tests/Services/TemplateGeneratorTests.cs — new tests:
   - GenerateModeFile_AllModes_RegisterGeneralWaitStep (Theory, all 9 modes)
   - GenerateModeFile_AllModes_GeneralWaitStepFollowsRoleStep (ordering check between role/general-wait/verify)
   - GenerateModeFile_Orchestrator_DropsPerTaskWaitFromDispatch
   - GenerateModeFile_Orchestrator_DropsKeepGeneralWaitOpenSubsection
   - GenerateModeFile_Orchestrator_ReframesWaitAsReleaseBlockOnCallee
   - GenerateModeFile_DispatchedRoles_HaveDispatchWaitReleaseNote (Theory, code-writer/reviewer/docs-writer)
6. DynaDocs.Tests/Integration/TemplateOverrideTests.cs — new test Init_FrameworkHashes_MatchEmbeddedTemplateContent guards against false-positive override detection by asserting init-time hashes equal ComputeHash(ReadBuiltInTemplate(name)) for every framework template.
7. dydo.json frameworkHashes — 10 entries bumped to SHA256s of the new normalized embedded source. Hashes computed via the same NormalizeForHash + SHA256 that TemplateCommand.ComputeHash uses (BOM strip + CRLF→LF). On-disk dydo/_system/templates/* is still pre-change content (this matches the plan: post-ship dydo template update will sync on-disk to embedded; agent workspace regen is also explicitly out of scope per plan, deferred to dydo fix later). Confirm this divergence is acceptable per plan intent.

TEST STATUS
- Full suite: 3972/3972 passing (worktree-isolated runner, 3m6s).
- gap_check: FAIL on 3 modules — Services/TerminalLauncher.cs, Services/WindowsTerminalLauncher.cs, Services/LinuxTerminalLauncher.cs. These files were already modified in the working tree before I started (Rose's auto-resume work). I have not touched any Services/ file. Reporting per workflow guidance — please confirm this is unrelated to slice 3 and not something for the slice 3 reviewer to pursue.

OUT OF SCOPE (per plan)
- Source/service code (Slice 2)
- Regenerating dydo/agents/*/workflow.md or modes/*.md
- Regenerating dydo/_system/templates/* on-disk override files (deferred to post-ship dydo template update)
- Role JSON changes

The plan calls Slice 3's deliverable: every mode template gets the Register General Wait step, the orchestrator template's Dispatch/Monitor are rewritten, the three dispatched-role templates carry the release-block one-liner, the agent-workflow template is synced, the guide doc exists, the framework hashes are bumped, and the new tests cover the contract.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-29 22:08
- Result: PASSED
- Notes: Slice 3 surface verified clean end-to-end:
- All 9 mode templates carry verbatim 'Register General Wait' step in Set Role -> Register General Wait -> Verify order.
- mode-orchestrator: per-task wait dropped, --wait reframed as callee release-block, 'Keep a general wait open' subsection removed, teardown wording updated.
- code-writer/reviewer/docs-writer Complete sections carry the verbatim release-block one-liner.
- agent-workflow.template.md Quick Reference synced.
- TemplateGeneratorTests: all 6 new tests present, assertions correct, ordering guard tight.
- TemplateOverrideTests: Init_FrameworkHashes_MatchEmbeddedTemplateContent asserts the right invariant.
- dydo.json frameworkHashes: 10 hashes independently re-computed (NormalizeForHash + SHA256 lower hex), all match byte-for-byte.
- Full suite 3972/3972 passing on the worktree-isolated runner.

gap_check failures (3 launcher coverage shortfalls + 1 concurrency test fail in AgentRegistryTests) confirmed unrelated to slice 3 (Rose's auto-resume work, fix-auto-resume-crashed-agents). User (balazs) authorized PASS given unrelated nature. Surfaced separately for Rose's track.

Decision-doc drift (021 says 'after claim', plan/templates/tests say 'after role') resolved: user confirmed role-set placement is canonical; dispatched docs-writer Adele on fix-decision-021-wait-ordering to correct the decision doc.

Awaiting human approval.

## Approval

- Approved: 2026-04-30 12:51
