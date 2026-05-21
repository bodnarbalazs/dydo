---
id: 205
area: project
type: issue
severity: low
status: open
found-by: manual
date: 2026-05-19
---

# Anchor-only [label](#section) literals in issue/task body text trip post-fix link validator (backtick-escape per workaround)

Post-#0186, anchor-only links validate against the source doc's own anchors; several issue and task files quote [label](#section) literally as bug documentation, which the parser now extracts as anchor-only links pointing at non-existent sections on the host doc — producing 7+ pre-existing whole-tree broken-link errors that did not show up at inquisition time but exist independently of the fix.

## Description

Surfaced by the link-validator-fix slice (commit 5783867, see Brian's deviation notes + Dexter's review note). Pre-fix, the same files emitted 'Broken link: ' (empty target) for the same patterns; post-fix the error text is now 'Broken link: #section' which is the correct, more informative shape. Count unchanged, error is just now visible. Affected files (as of commit 5783867): dydo/project/issues/_index.md:80, dydo/project/issues/0186-anchor-only-links-label-section-produce-empty-target-broken-link-error.md:11+17, dydo/project/issues/0188-test-coverage-gap-for-link-validation-shapes-that-triggered-0184.md:19, dydo/project/tasks/link-validator-fix.md:35+72 (Dexter found these), plus the unrelated cross-tree dydo/project/tasks/identity-hijack-fix-plan.md:15 broken link into agents/ which is excluded from allDocs (separate cleanup). All of these are in docs-writer paths (not code-writer writable). Fix: backtick-escape the literal patterns ('[label](#section)' as code) so they leave the doc.Links extraction. Filing per Adele's instruction during link-validator-fix release flow.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)