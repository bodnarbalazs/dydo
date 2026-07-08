---
area: general
type: changelog
date: 2026-07-08
needs-human: false
---

# Task: readme-2.0-rewrite-test-fix

Review commit 7bdf562 on master (readme-2.0-rewrite-test-fix), exactly 4 files: Templates/about-dynadocs.template.md, dydo/reference/about-dynadocs.md, DynaDocs.Tests/Services/FolderScaffolderTests.cs, DynaDocs.Tests/Services/TemplateGeneratorTests.cs.

WHAT/WHY: 15 tests were red on master because the 2.0 rewrite left about-dynadocs (template + reference copy) linking to the internal Decision 024 doc via a relative path that doesn't resolve in a fresh init -> 'dydo check' emitted 1 error on scaffolded about-dynadocs.md, cascading into InitCheckIntegrationTests(5)/FixCommandIntegrationTests(3)/ChangelogStructureTests(3) + 4 content-assertion tests. Fix: repoint the link to the GitHub URL (matches README.md, kept byte-identical between template and reference for CommandDocConsistency Test 10), and realign the 4 content-assertion tests to the placeholder reality (assert VISUAL/_assets placeholder instead of removed diagram; document inbox commands instead of the removed --inbox flag; renamed 2 now-misleading test methods).

VERIFY: fresh-init 'dydo check' = 0 errors; full suite green (4352/0) on the commingled tree; gap_check gate green after Frank's #216 refactor landed. Scope was readme-2.0 reds only; Notion/dydo-commands changes belong to other agents' slices and are NOT in this commit. Please review the diff of 7bdf562 for correctness and test-assertion quality; report verdict to origin (Emma).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 7bdf562 on master (readme-2.0-rewrite-test-fix), exactly 4 files: Templates/about-dynadocs.template.md, dydo/reference/about-dynadocs.md, DynaDocs.Tests/Services/FolderScaffolderTests.cs, DynaDocs.Tests/Services/TemplateGeneratorTests.cs.

WHAT/WHY: 15 tests were red on master because the 2.0 rewrite left about-dynadocs (template + reference copy) linking to the internal Decision 024 doc via a relative path that doesn't resolve in a fresh init -> 'dydo check' emitted 1 error on scaffolded about-dynadocs.md, cascading into InitCheckIntegrationTests(5)/FixCommandIntegrationTests(3)/ChangelogStructureTests(3) + 4 content-assertion tests. Fix: repoint the link to the GitHub URL (matches README.md, kept byte-identical between template and reference for CommandDocConsistency Test 10), and realign the 4 content-assertion tests to the placeholder reality (assert VISUAL/_assets placeholder instead of removed diagram; document inbox commands instead of the removed --inbox flag; renamed 2 now-misleading test methods).

VERIFY: fresh-init 'dydo check' = 0 errors; full suite green (4352/0) on the commingled tree; gap_check gate green after Frank's #216 refactor landed. Scope was readme-2.0 reds only; Notion/dydo-commands changes belong to other agents' slices and are NOT in this commit. Please review the diff of 7bdf562 for correctness and test-assertion quality; report verdict to origin (Emma).

## Approval

- Approved: 2026-07-08 10:15
