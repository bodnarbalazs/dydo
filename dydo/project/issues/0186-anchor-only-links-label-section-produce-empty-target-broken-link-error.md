---
id: 186
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-19
---

# Anchor-only links [label](#section) produce empty-target Broken link error

Anchor-only links are routed through the non-markdown branch of BrokenLinksRule which resolves an empty link target against the source directory and reports it as broken, regardless of whether the anchor exists on the same page.

## Description

**Latent in production today** — the only ](#…) matches in dydo/**/*.md outside templates live under dydo/_system/templates/mode-judge.template.md, which RuleSkipPaths.IsTemplateOrAddition bypasses. Any new doc that uses [label](#section) outside _system/templates/ or _system/template-additions/ will trigger the bug.

**Mechanism:**

1. LinkExtractor.SplitAnchor("#section") returns (path: "", anchor: "section") so LinkInfo.Target is empty.
2. BrokenLinksRule.cs:32 tests !link.Target.EndsWith(".md") — true for the empty string — and routes the link into the non-markdown branch.
3. BrokenLinksRule.cs:34-44 calls PathUtils.ResolvePath(Path.Combine(basePath, doc.RelativePath), "") which evaluates to the source doc's parent directory. File.Exists is false for a directory path, so the rule emits Broken link:  (with empty target — the .md branch is the only path that appends the anchor info to the error message).

**Reproducer:** dydo check dydo/agents/Frank/repro (preserved hand-crafted tree). Three anchor-only links → three Broken link:  errors.

**Fix:** Detect anchor-only links (empty Target with non-null Anchor) and validate the anchor against the source doc's own Anchors list. Note: the suggested fix in the inquisition report (that LinkResolver.ResolveLink would handle anchor-only links correctly if called) is incorrect — with empty Target, ResolvePath returns the source directory, not the source file, so the allDocs membership test would also fail. The fix needs to either explicitly handle the anchor-only case in BrokenLinksRule, or change LinkResolver to recognise an empty Target and treat the source doc as the target.

Suggested regression tests:
- BrokenLinksRuleTests.Validate_AcceptsAnchorOnlyLink_WhenAnchorExistsOnSamePage
- BrokenLinksRuleTests.Validate_ReportsAnchorOnlyLink_WhenAnchorDoesNotExist
- BrokenLinksRuleTests.Validate_DoesNotEmitEmptyTargetError_ForAnchorOnlyLink

**Reference:** dydo/project/inquisitions/link-validator-resolver-divergence.md finding 2.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)