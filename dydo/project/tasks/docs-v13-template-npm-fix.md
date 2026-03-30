---
area: general
name: docs-v13-template-npm-fix
status: human-reviewed
created: 2026-03-30T17:53:03.7724824Z
assigned: Grace
updated: 2026-03-30T19:09:09.1452757Z
---

# Task: docs-v13-template-npm-fix

Updated Templates/about-dynadocs.template.md and npm/README.md to match the already-fixed reference docs (dydo/reference/about-dynadocs.md and root README.md). Changes: removed old amnesia framing, replaced with structured memory/context problem framing; removed platform-agnostic claims and non-Claude Code sections; updated role table from 7 to 9 roles; removed workflow flags section; added new sections (Stop Doing Agent Work, Template Additions, Multi-Agent Orchestration, etc.). Removed SVG image references from template that don't exist in fresh inits. Updated ContainsWorkflowFlags test to ContainsInboxFlag to match new template content. All template and integration tests pass.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Updated Templates/about-dynadocs.template.md and npm/README.md to match the already-fixed reference docs (dydo/reference/about-dynadocs.md and root README.md). Changes: removed old amnesia framing, replaced with structured memory/context problem framing; removed platform-agnostic claims and non-Claude Code sections; updated role table from 7 to 9 roles; removed workflow flags section; added new sections (Stop Doing Agent Work, Template Additions, Multi-Agent Orchestration, etc.). Removed SVG image references from template that don't exist in fresh inits. Updated ContainsWorkflowFlags test to ContainsInboxFlag to match new template content. All template and integration tests pass.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-30 19:19
- Result: PASSED
- Notes: LGTM. Template matches reference doc (minus fresh-init-absent SVGs). npm README matches root README. Role table updated to 9 roles. Workflow flags replaced with --inbox. Test updated. All 3348 tests pass, coverage gate green.

Awaiting human approval.