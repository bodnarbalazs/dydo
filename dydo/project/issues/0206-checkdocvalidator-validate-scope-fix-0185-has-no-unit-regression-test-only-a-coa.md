---
id: 206
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-21
---

# CheckDocValidator.Validate scope fix (#0185) has no unit regression test; only a coarse E2E covers it

The #0185 fix lives in CheckDocValidator.Validate (scan allDocs from docs root, derive docsToValidate via IsUnderScope, still pass full allDocs to every rule); the only regression guard is the coarse E2E Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder, and there is no unit test on Validate asserting the allDocs-vs-docsToValidate split.

## Description

Confirmed by the link-validator-fix-verification inquisition (Brian, 2026-05-21), Finding 2; ruled CONFIRMED by judge Emma.

The #0185 bug was never in LinkResolver or BrokenLinksRule -- it lived in scope: CheckCommand/CheckDocValidator narrowed allDocs to the requested subfolder, so cross-folder links resolved against an incomplete doc set. The fix lives in CheckDocValidator.Validate(basePath, reportScope): it scans allDocs from the docs root, derives docsToValidate by filtering with IsUnderScope, and crucially still passes the full allDocs (not docsToValidate) as the context argument to every rule.Validate / rule.ValidateFolder call (CheckDocValidator.cs:46,54).

Coverage gap:
- The slice's new BrokenLinksRule unit tests Validate_AcceptsCrossFolderLink_WhenAllDocsContainsTarget and Validate_AcceptsTwoLevelParentLink_AcrossFolders exercise the resolver with the target already present in allDocs -- i.e. code that was never broken. They do not reproduce the #0185 failure mode.
- CheckDocValidatorTests.cs has 7 tests, all on IsUnderScope (the easy half). There is no unit test on CheckDocValidator.Validate itself.
- The only regression guard for the actual scope fix is the single E2E test CliEndToEndTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder, whose assertion is a coarse Assert.DoesNotContain(`Broken link:`). A regression changing rule.Validate(doc, allDocs, ...) to rule.Validate(doc, docsToValidate, ...) at CheckDocValidator.cs:46 would silently reintroduce #0185 and only that one coarse, slow (full init + process spawn) E2E test would catch it.

Recommended test: a CheckDocValidator unit test that builds a multi-folder doc set, calls Validate(basePath, reportScope) with reportScope set to one subfolder, and asserts both that a cross-folder link from an in-scope doc to an out-of-scope target is NOT flagged broken, and that an in-scope doc with a genuinely missing target IS flagged. Suggested name: Validate_Scoped_AcceptsCrossScopeLink_ButCatchesMissingTarget.

Reference: dydo/project/inquisitions/link-validator-fix-verification.md, Finding 2.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)