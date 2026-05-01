---
id: 146
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-05-01
resolved-date: 2026-05-01
---

# Docs-writer + reviewer workflows don't gate on dydo check — schema drift accumulates

Open medium-severity workflow gap: docs-writer and reviewer mode templates didn't require `dydo check` clean before commit/approval, so frontmatter and schema drift accumulated across the docs tree (eventually 41 errors, 184 warnings). Henry's `e338115` added the gate going forward; this cleanup task addresses the historic backlog separately.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed by e338115 (Henry): reviewer template Verify section now includes 'Run dydo check' as a release gate; docs-writer template tightened to explicit exit-zero gate plus 'Before Committing' release-gate note; new 'Writing Content' subsection adds summary-paragraph guideline. Backlog cleanup of pre-existing drift handled separately by Quinn (b92e1b3 + 756bedb).