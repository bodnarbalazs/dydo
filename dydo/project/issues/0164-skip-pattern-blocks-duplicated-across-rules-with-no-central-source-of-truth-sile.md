---
id: 164
area: project
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-04
---

# Skip-pattern blocks duplicated across rules with no central source of truth - silent inconsistency vector

## Description

Architectural meta-finding synthesizing the proximate causes of issues for findings #2 and #5.\n\nEvidence:\n- FrontmatterRule:22-26, BrokenLinksRule:24-26, NamingRule:17-19 all carry the IDENTICAL 4-line _system/templates/ + _system/template-additions/ skip block.\n- SummaryRule lacks it (proximate cause of issue for finding #2).\n- RelativeLinksRule has no path skips at all.\n- No rule skips _system/.local/ (proximate cause of issue for finding #5).\n- Services/HubGenerator.IsExcludedPath:279-289 and Commands/FixHubHandler.IsExcludedFolder:134-146 are TWO separate exclusion lists with overlapping but distinct semantics.\n\nThree sources of truth, all slightly different, none applied at the scan boundary. No live drift caused by this once #2 and #5 are fixed - severity is low because the value is preventive, not remedial. Preventing the next instance is the goal.\n\nFix path (planner's call - two acceptable shapes):\n1. Hoist a small RuleScopeFilter. One source of truth for 'files that no rule should look at' (_system/.local/**, hidden dirs). Per-rule 'protected virtual bool SkipForThisRule(DocFile)' for rule-local logic (e.g., template-additions for content rules).\n2. Or extend RuleBase with a ShouldSkip(DocFile) virtual + a project-wide default returning true for _system/.local/.\n\nAlternative: do this at the orchestration layer (Services/DocChecker or whichever assembles rules) instead of inside RuleBase, depending on testability preferences.\n\nNote: some duplication may be load-bearing. RelativeLinksRule arguably should run on template-additions to catch wikilinks; OrphanDocsRule's MainDocFolders allowlist is fundamentally different scoping. The architectural shape should preserve those distinctions.\n\nConfirmed by inquisition dydo-check-drift.md finding #6 (judge: Dexter).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)