---
area: general
type: changelog
date: 2026-05-21
---

# Task: link-validator-fix

Review the link-validator fix slice (#0185 #0186 #0187 #0188). Commit: 5783867. Plan: dydo/agents/Brian/archive/20260519-120857/plan-link-validator-fix.md.

What landed:
- #0185: CheckCommand.ResolvePath split into ResolveBasePath (always docs root) + ResolveReportScope (user path); CheckDocValidator.Validate(basePath, reportScope?) filters docsToValidate + foldersToValidate via IsUnderScope (internal helper, separator/case/trailing-slash-aware) while allDocs stays full-tree. Path-not-found and path-outside-docs-tree now return clear errors; supplying a path at-or-above the docs root degrades to a whole-tree check.
- #0186: BrokenLinksRule line-32 condition gated on link.Target.Length > 0 so anchor-only links flow to the resolver. LinkResolver.ResolveLink short-circuits empty target via ValidateAnchor against sourceDoc.Anchors.
- #0187: ILinkResolver.ResolveToRelativeKey added; LinkResolver implements via PathUtils.ResolvePath + Path.GetRelativePath + NormalizeForKey. DocGraph takes ILinkResolver in its (now single) constructor and routes through it. GraphCommand and SnapshotService wired to pass new LinkResolver(). Services/DocLinkResolver.cs deleted.
- #0188: 5 new BrokenLinksRuleTests (cross-folder + anchor-only happy/sad/anti-regression), 5 new LinkResolverTests (cross-folder + 2-level parent + anchor-only) plus parametrized cross-resolver-agreement theory (Resolve_HandlesEveryRelativeShape_UsedByBothCallers + 2 companions covering anchored cross-folder and intentional anchor-only divergence), 2 new PathUtilsDiscoveryTests (mixed separators + parent-collapse positive correctness), 1 new CliEndToEndTests (Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder using TestData/link-validator/ fixture), new CheckDocValidatorTests covering IsUnderScope (mixed separators, case, trailing slash, partial-prefix rejection).

Verification:
- python DynaDocs.Tests/coverage/run_tests.py: 4235/4235 pass.
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140/140 modules pass tier requirements.
- dydo check dydo/project/decisions: 0 broken-link errors (was 5).
- dydo check dydo/guides: 0 (was 29).
- dydo check dydo/reference: 0 (was 27).
- dydo graph dydo/index.md: 'no outgoing links' (unchanged).

Plan deviations:
- Plan-named BrokenLinksRuleTests test 'Validate_AcceptsCrossFolderLink_WhenTargetNotInAllDocs_ButExistsOnDisk' is shape-(b)-oriented; under shape (a) the rule itself does not change, so I renamed to 'Validate_AcceptsCrossFolderLink_WhenAllDocsContainsTarget'. The integration-level guard for #0185 is CliEndToEndTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder, which exercises the validator+rule pipeline.
- ResolveToRelativeKey uses Path.GetFullPath + Path.GetRelativePath (per plan). Old DocLinkResolver silently absorbed over-traversal (../../../foo.md from a shallow source mapped to 'foo.md'); the new method returns an out-of-tree key ('../foo.md' etc.) that fails the allDocs membership test, so over-traversing links drop the edge instead of mis-mapping it. Verified no such links exist outside _system/templates/ (which BrokenLinksRule already skips), and the named smoke 'dydo graph dydo/index.md' is byte-identical (no outgoing links pre or post).
- Added 2 CheckCommand E2E tests (Check_NonExistentPath_ReturnsToolError, Check_PathOutsideDocsTree_ReturnsToolError) and refactored Execute into ValidateConfig/ValidateDocs/ValidateAgents/WriteSummary helpers to bring CRAP under the T1 threshold (was 39.7, gate is 30).
- TestData fixture is staged-and-now-committed (run_tests.py copies dirty files into the worktree, but untracked directories surface only as a single ?? entry in porcelain output, so the first test run could not see them; staging and committing makes the fixture survive both run_tests.py and bare dotnet test).

Known pre-existing whole-tree errors not caused by this slice (unchanged from pre-fix in count, but error message text now correctly reads 'Broken link: #section' instead of the empty 'Broken link: '):
- project/issues/_index.md:80, project/issues/0186-*:11,17, project/issues/0188-*:19 — all `#section` anchor-only links inside literal `[label](#section)` patterns documenting the bug.
- project/tasks/identity-hijack-fix-plan.md:15 — '../../agents/Dexter/plan-identity-hijack-fix.md' (cross-tree to agents/, which is excluded from allDocs).
None of these are in code-writer writable paths; a docs-writer follow-up should backtick-escape the issue-file patterns or otherwise neutralise the noise.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review the link-validator fix slice (#0185 #0186 #0187 #0188). Commit: 5783867. Plan: dydo/agents/Brian/archive/20260519-120857/plan-link-validator-fix.md.

What landed:
- #0185: CheckCommand.ResolvePath split into ResolveBasePath (always docs root) + ResolveReportScope (user path); CheckDocValidator.Validate(basePath, reportScope?) filters docsToValidate + foldersToValidate via IsUnderScope (internal helper, separator/case/trailing-slash-aware) while allDocs stays full-tree. Path-not-found and path-outside-docs-tree now return clear errors; supplying a path at-or-above the docs root degrades to a whole-tree check.
- #0186: BrokenLinksRule line-32 condition gated on link.Target.Length > 0 so anchor-only links flow to the resolver. LinkResolver.ResolveLink short-circuits empty target via ValidateAnchor against sourceDoc.Anchors.
- #0187: ILinkResolver.ResolveToRelativeKey added; LinkResolver implements via PathUtils.ResolvePath + Path.GetRelativePath + NormalizeForKey. DocGraph takes ILinkResolver in its (now single) constructor and routes through it. GraphCommand and SnapshotService wired to pass new LinkResolver(). Services/DocLinkResolver.cs deleted.
- #0188: 5 new BrokenLinksRuleTests (cross-folder + anchor-only happy/sad/anti-regression), 5 new LinkResolverTests (cross-folder + 2-level parent + anchor-only) plus parametrized cross-resolver-agreement theory (Resolve_HandlesEveryRelativeShape_UsedByBothCallers + 2 companions covering anchored cross-folder and intentional anchor-only divergence), 2 new PathUtilsDiscoveryTests (mixed separators + parent-collapse positive correctness), 1 new CliEndToEndTests (Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder using TestData/link-validator/ fixture), new CheckDocValidatorTests covering IsUnderScope (mixed separators, case, trailing slash, partial-prefix rejection).

Verification:
- python DynaDocs.Tests/coverage/run_tests.py: 4235/4235 pass.
- python DynaDocs.Tests/coverage/gap_check.py --force-run: 140/140 modules pass tier requirements.
- dydo check dydo/project/decisions: 0 broken-link errors (was 5).
- dydo check dydo/guides: 0 (was 29).
- dydo check dydo/reference: 0 (was 27).
- dydo graph dydo/index.md: 'no outgoing links' (unchanged).

Plan deviations:
- Plan-named BrokenLinksRuleTests test 'Validate_AcceptsCrossFolderLink_WhenTargetNotInAllDocs_ButExistsOnDisk' is shape-(b)-oriented; under shape (a) the rule itself does not change, so I renamed to 'Validate_AcceptsCrossFolderLink_WhenAllDocsContainsTarget'. The integration-level guard for #0185 is CliEndToEndTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder, which exercises the validator+rule pipeline.
- ResolveToRelativeKey uses Path.GetFullPath + Path.GetRelativePath (per plan). Old DocLinkResolver silently absorbed over-traversal (../../../foo.md from a shallow source mapped to 'foo.md'); the new method returns an out-of-tree key ('../foo.md' etc.) that fails the allDocs membership test, so over-traversing links drop the edge instead of mis-mapping it. Verified no such links exist outside _system/templates/ (which BrokenLinksRule already skips), and the named smoke 'dydo graph dydo/index.md' is byte-identical (no outgoing links pre or post).
- Added 2 CheckCommand E2E tests (Check_NonExistentPath_ReturnsToolError, Check_PathOutsideDocsTree_ReturnsToolError) and refactored Execute into ValidateConfig/ValidateDocs/ValidateAgents/WriteSummary helpers to bring CRAP under the T1 threshold (was 39.7, gate is 30).
- TestData fixture is staged-and-now-committed (run_tests.py copies dirty files into the worktree, but untracked directories surface only as a single ?? entry in porcelain output, so the first test run could not see them; staging and committing makes the fixture survive both run_tests.py and bare dotnet test).

Known pre-existing whole-tree errors not caused by this slice (unchanged from pre-fix in count, but error message text now correctly reads 'Broken link: #section' instead of the empty 'Broken link: '):
- project/issues/_index.md:80, project/issues/0186-*:11,17, project/issues/0188-*:19 — all `#section` anchor-only links inside literal `[label](#section)` patterns documenting the bug.
- project/tasks/identity-hijack-fix-plan.md:15 — '../../agents/Dexter/plan-identity-hijack-fix.md' (cross-tree to agents/, which is excluded from allDocs).
None of these are in code-writer writable paths; a docs-writer follow-up should backtick-escape the issue-file patterns or otherwise neutralise the noise.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-19 19:25
- Result: PASSED
- Notes: Review PASS on substance. Code matches plan: CheckCommand split (ResolveBasePath/ResolveReportScope), CheckDocValidator.Validate(basePath, reportScope?) with IsUnderScope, BrokenLinksRule:33 gated on Length>0, LinkResolver.ResolveLink empty-target short-circuits via ValidateAnchor, ResolveToRelativeKey added, DocGraph wired through ILinkResolver, DocLinkResolver.cs deleted. Tests are real assertions (not theater): cross-resolver-agreement theory + 2 companions, IsUnderScope coverage for separator/case/trailing-slash/partial-prefix, E2E fixture via TestData/link-validator/ + csproj Content Include. Verification (locally-built dydo): per-folder checks 0/0/0 confirm Brian's claims (decisions/guides/reference). dotnet test 4242 pass 0 fail 1 skip. Whole-tree dydo check: 9 errors, all pre-existing noise class — 7 Brian flagged (#section literals in issue/inquisition files, ../../agents/Dexter cross-tree, wikilinks) plus 2 extras at project/tasks/link-validator-fix.md:35,72 where Brian's own brief text was templated into the auto-generated task file (same #section noise — docs-writer follow-up). Minor finding: Services/SnapshotService.cs:173 still has a private ResolveLink that duplicates the old DocLinkResolver shape for the snapshot outgoing-links list — out of scope per plan but spirit of #0187 leaves it orphaned; file as follow-up. gap_check FAILS on 12 modules — bubbling to Adele/Frank since none are in Brian's slice (4 from Dexter's in-progress identity-hijack, 1 from Charlie's Decision 023 (WorktreeCommand), 7 pre-existing master gaps: WhoamiCommand/ReviewCommand/DispatchService/MessageService/InboxService/WorkspaceCommand/TaskApproveHandler).

Awaiting human approval.

## Approval

- Approved: 2026-05-21 19:06
