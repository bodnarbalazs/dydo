---
area: general
name: fix-agent-tool-nudge-doc
status: review-failed
created: 2026-04-28T17:39:33.8270558Z
assigned: Brian
updated: 2026-04-28T17:43:45.9680507Z
---

# Task: fix-agent-tool-nudge-doc

Documentation ready for review. Single-line addition: S6 row in dydo/reference/guardrails.md (Tier 2 Soft-Blocks table) describing the new Agent tool soft-nudge. Style/voice mirrors existing S1/S2 rows. One commit (c2be292). Verify wording is consistent with adjacent rows and message accurately describes the (in-flight) implementation per dydo/agents/Brian/archive/20260428-141026/plan-agent-tool-nudge.md.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Documentation ready for review. Single-line addition: S6 row in dydo/reference/guardrails.md (Tier 2 Soft-Blocks table) describing the new Agent tool soft-nudge. Style/voice mirrors existing S1/S2 rows. One commit (c2be292). Verify wording is consistent with adjacent rows and message accurately describes the (in-flight) implementation per dydo/agents/Brian/archive/20260428-141026/plan-agent-tool-nudge.md.

## Code Review (2026-04-28 17:46)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Two consistency issues vs adjacent rows: (1) row name 'Agent tool soft-nudge' names the mechanism instead of the trigger and is redundant inside a 'Soft-Blocks' table — adjacent rows use trigger-noun phrases (Role mismatch / No-launch dispatch / Pending wait registration / Inactive agent messaging); suggest 'Built-in Agent tool' or 'Agent tool dispatch'. (2) Same-row inconsistency: Trigger says 'stateless sub-agent' (hyphenated) but Message says 'stateless subagent' (no hyphen) — plan and Decision 009 use 'subagent' throughout. Minor advisory: the 'Steers toward dydo dispatch...' sentence in Trigger describes purpose, not trigger; Message column already says this. Coverage: gap_check.py exit 0 (doc-only commit, no source/test churn).

Requires rework.