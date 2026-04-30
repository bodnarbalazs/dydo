---
area: general
type: changelog
date: 2026-04-30
---

# Task: publish-general-wait-guide

Review newly-published dydo/guides/agent-general-wait.md against writing-docs.md and Decision 021. Verify links resolve and tone matches the existing guides corpus.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review newly-published dydo/guides/agent-general-wait.md against writing-docs.md and Decision 021. Verify links resolve and tone matches the existing guides corpus.

## Code Review (2026-04-29 21:35)

- Reviewed by: Dexter
- Result: FAILED
- Issues: FAIL. Two blocking issues. (1) Lifecycle ordering contradicts Decision 021: the guide and the new lifecycle diagram place the general wait AFTER the role step (line 26-28 box; line 32 'right after the role step'), but Decision 021 says 'right after dydo agent claim' / 'right after the claim step'. Note: the as-shipped mode templates (Templates/mode-reviewer.template.md, etc.) match the guide, so Decision 021 and the templates are themselves out of step — needs upstream reconciliation before the guide can be correct. (2) Troubleshooting line 79 is imprecise: 'dydo agent clean removes wait markers belonging to dead processes' — actually agent clean clears all .waiting/*.json in the workspace regardless of PID liveness; dead-PID self-heal is in the guard (GuardCommand.cs SelfHealAndGetPendingMarkers), not agent clean. Other checks pass: writing-docs.md frontmatter/structure correct, all 4 outbound links resolve, dydo check is clean for this file, tone matches the guides corpus. Did not run gap_check (docs-only change; working tree has unrelated source/test modifications that would conflate signal). Full notes in dydo/agents/Dexter/workspace/review-notes.md.

Requires rework.

## Approval

- Approved: 2026-04-30 12:51
