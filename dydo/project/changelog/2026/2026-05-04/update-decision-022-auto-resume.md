---
area: general
type: changelog
date: 2026-05-04
---

# Task: update-decision-022-auto-resume

(No description)

## Progress

- [x] Clarify in Retry cap that "claim" includes same-session reclaims (cites #0153)
- [x] Add Warmup gate section between Resume launch and Retry cap (60s default, rationale, no_refresh_after_warmup fail-fast, new state.md fields)
- [x] Document `last-resume-launched-at` and `pre-resume-pid` and their reset on claim/release
- [x] Add `Services/ProcessUtils.Ancestry.cs` (new file, FindClaudeAncestor, closes #0151) to Code changes
- [x] Document the `windowId`/`useTab` plumb-through in Code changes (closes #0144 on Windows/Mac)
- [x] Add bullet for the 60s warmup gate in Re-evaluate
- [x] Add Linux-side resume window restoration to the explicit out-of-scope list (#0144 Linux half deferred)
- [x] Refresh Affects list (new file, closed issues, AgentRegistry/AgentState detail)

## Files Changed

- `dydo/project/decisions/022-auto-resume-crashed-agents.md`

## Review Summary

(Reply pending)

## Approval

- Approved: 2026-05-04 21:52
