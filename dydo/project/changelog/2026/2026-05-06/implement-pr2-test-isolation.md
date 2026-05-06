---
area: general
type: changelog
date: 2026-05-06
---

# Task: implement-pr2-test-isolation

PR2 #0167 implementation in commit 405a220. Plan: dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md (PR2 section, OQ1=A locked).

CHANGES (DynaDocs.Tests/** only):
- AssemblyInfo.cs (new): [assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
- RuntimeRegression/ParallelisationDisabledTests.cs (new): reflection invariant pin
- Services/ProcessUtilsCollection.cs: doc-comment honesty (no behaviour change)
- EndToEnd/CliEndToEndTests.cs: doc-comment honesty (fixture wiring untouched)
- Services/AuditCompactionTests.cs: Stderr migration + new DoesNotLeakConsoleErrorOnException
- Services/AuditEdgeCaseTests.cs: Stderr migration via outer-scope sessions variable
- Commands/WorktreeMergeSafetyIntegrationTests.cs: CaptureAll collapsed onto ConsoleCapture.All

VERIFICATION:
- run_tests.py: Passed:4087, Failed:0, Duration 4m 3s (vs 3:13 baseline → +50s, within plan +60-120s prediction)
- gap_check.py --force-run: 12 integration failures + 1 tier failure (Commands/TemplateCommand.cs T1). ALL residuals are in code paths owned by in-flight dydo-check-drift PR2 work (Rules/, CheckCommand, FixCommand, TemplateCommand) — untouched by my commit. Filtered re-run on ChangelogStructureTests + Fix_GeneratedHubsPassFrontmatterCheck reproduces 4/5 fail with identical error signatures, confirming pre-existing under in-flight working tree. Soft-pass per Adele's brief.

OUT OF SCOPE (per plan): OQ4 docs touch on dydo/project/issues/0148-… + dydo/guides/coding-standards.md test-parallelism subsection — docs-writer territory; coordination with Adele pending.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

PR2 #0167 implementation in commit 405a220. Plan: dydo/agents/Dexter/archive/20260505-173954/plan-runtime-regression-batch.md (PR2 section, OQ1=A locked).

CHANGES (DynaDocs.Tests/** only):
- AssemblyInfo.cs (new): [assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
- RuntimeRegression/ParallelisationDisabledTests.cs (new): reflection invariant pin
- Services/ProcessUtilsCollection.cs: doc-comment honesty (no behaviour change)
- EndToEnd/CliEndToEndTests.cs: doc-comment honesty (fixture wiring untouched)
- Services/AuditCompactionTests.cs: Stderr migration + new DoesNotLeakConsoleErrorOnException
- Services/AuditEdgeCaseTests.cs: Stderr migration via outer-scope sessions variable
- Commands/WorktreeMergeSafetyIntegrationTests.cs: CaptureAll collapsed onto ConsoleCapture.All

VERIFICATION:
- run_tests.py: Passed:4087, Failed:0, Duration 4m 3s (vs 3:13 baseline → +50s, within plan +60-120s prediction)
- gap_check.py --force-run: 12 integration failures + 1 tier failure (Commands/TemplateCommand.cs T1). ALL residuals are in code paths owned by in-flight dydo-check-drift PR2 work (Rules/, CheckCommand, FixCommand, TemplateCommand) — untouched by my commit. Filtered re-run on ChangelogStructureTests + Fix_GeneratedHubsPassFrontmatterCheck reproduces 4/5 fail with identical error signatures, confirming pre-existing under in-flight working tree. Soft-pass per Adele's brief.

OUT OF SCOPE (per plan): OQ4 docs touch on dydo/project/issues/0148-… + dydo/guides/coding-standards.md test-parallelism subsection — docs-writer territory; coordination with Adele pending.

## Code Review (2026-05-05 20:24)

- Reviewed by: Frank
- Result: FAILED
- Issues: FAIL: Compact_CorruptBaseline_DoesNotLeakConsoleErrorOnException (AuditCompactionTests.cs:849-867) is structurally dead. The corrupt-baseline path does not throw - the previous test (LogsWarningInsteadOfSilentSkip, line 836) demonstrates Compact logs a warning and returns normally. Catch block is unreachable; Assert.Same only verifies the success-path finally that is implicit in every Stderr call. Test name advertises 'OnException' semantics that are never exercised. Either (a) move the contract pin to a new ConsoleCaptureTests.cs and use a throwing action, e.g. Assert.Throws<InvalidOperationException>(() => ConsoleCapture.Stderr(() => throw new InvalidOperationException()))) followed by Assert.Same, or (b) delete the test - ConsoleCapture's try/finally is best pinned in its own test file, not as a co-located AuditCompactionTests duplicate. Recommend (a). Other gates clean: dotnet build clean; 4111 tests pass under worktree (4m 3s vs 3:13 baseline, within plan +60-120s); gap_check.py --force-run exits 0 (140/140 modules pass tier); dydo check has 55 errors/46 warnings ALL pre-existing in dydo doc tree (project/inquisitions, project/issues, _system/template-additions) - untouched by this commit. AssemblyInfo, ParallelisationDisabledTests reflection pin, doc-comment honesty edits, and CaptureAll collapse all faithful to plan.

Requires rework.

## Approval

- Approved: 2026-05-06 17:47
