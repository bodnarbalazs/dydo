---
id: 188
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# Test coverage gap for link-validation shapes that triggered #0184

BrokenLinksRule, LinkResolver, DocLinkResolver, PathUtils.ResolvePath, and CheckCommand all lack tests for the link shapes that produced issue #0184 — the bug shipped because no test exercises any of the failing inputs.

## Description

**Concrete gaps observed:**

1. DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs — every test that uses a valid target adds it to allDocs. **No test passes an allDocs list that omits a real on-disk target file**, so the rule's failure mode for issue #0184 is uncovered. No ../../ (two-up) coverage. No anchor-only [label](#section) test.
2. DynaDocs.Tests/Services/LinkResolverTests.cs covers ValidateAnchor directly but never exercises ResolveLink with an empty link.Target.
3. **No DocLinkResolverTests.cs exists** (verified by glob DynaDocs.Tests/**/*LinkResolver*). DocLinkResolver is only exercised transitively through DocGraphTests. Behavioural drift between the two resolvers (issue #187) cannot be detected.
4. PathUtilsDiscoveryTests.cs:40-54 has 3 ResolvePath tests but they only assert "contains no backslash, contains no .." — they do not verify the resolved path is correct. (The inquisitor's claim of *zero* direct tests is mistaken on the count; the substantive point about under-testing holds.)
5. DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs:124 runs dydo check . (whole tree). **Nothing exercises dydo check <subfolder>** and asserts that an in-tree link to a sibling folder is not reported broken.

**Regression tests this fix slice must add:**

- BrokenLinksRuleTests.Validate_AcceptsCrossFolderLink_WhenTargetNotInAllDocs_ButExistsOnDisk
- BrokenLinksRuleTests.Validate_AcceptsTwoLevelParentLink_AcrossFolders (../../foo/bar.md)
- BrokenLinksRuleTests.Validate_AcceptsAnchorOnlyLink_WhenAnchorExistsOnSamePage
- BrokenLinksRuleTests.Validate_ReportsAnchorOnlyLink_WhenAnchorDoesNotExist
- BrokenLinksRuleTests.Validate_DoesNotEmitEmptyTargetError_ForAnchorOnlyLink
- PathUtilsTests.ResolvePath_HandlesMixedSeparators
- PathUtilsTests.ResolvePath_CollapsesParentSegments
- LinkResolverTests.ResolveLink_AcceptsCrossFolderRelativeLink
- LinkResolverTests.ResolveLink_AcceptsTwoLevelParentLink
- LinkResolverTests.ResolveLink_AnchorOnlyLink_ValidatesAgainstSourceDocAnchors
- DocLinkResolverTests.Resolve_AgreesWithLinkResolver_OnEveryRelativeShape (cross-resolver parametrised agreement — the most important regression guard)
- CheckCommandTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder

**Reference:** dydo/project/inquisitions/link-validator-resolver-divergence.md finding 4.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD: the named link-validation regression tests were added (BrokenLinksRuleTests.cs:146-201, LinkResolverTests.cs:116-140,198); the DocLinkResolver cross-check is moot since that class was deleted. Triage sweep 2026-07-04 (Brian, CoS).