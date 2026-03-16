---
area: general
name: baton-passing-docs
status: pending
created: 2026-03-16T15:44:16.3274271Z
assigned: Dexter
---

# Task: baton-passing-docs

(No description)

## Progress

- [x] Updated guardrails.md — H15 updated for baton-passing / reply_required decoupling, H25 added for dispatched code-writer review enforcement
- [x] Updated code-writer role doc — dispatch pattern changed to `--no-wait`, H25 reference added, baton-passing explained
- [x] Updated reviewer role doc — dispatch changed to `--no-wait`, on-pass section documents reply obligation messaging
- [x] Updated planner role doc — removed `--wait` option, removed dispatch-and-wait transition option
- [x] Added summary paragraph to decision 010, ran `dydo fix` to regenerate indexes
- [ ] Template files (`Templates/mode-*.template.md`) — read-only for docs-writer, need code-writer to update

## Files Changed

- `dydo/reference/guardrails.md` — H15 updated, H25 added, extensibility section updated, decision 010 linked
- `dydo/reference/roles/code-writer.md` — dispatch pattern `--wait` → `--no-wait`, H25 reference, decision 010 linked
- `dydo/reference/roles/reviewer.md` — dispatch `--wait` → `--no-wait`, baton-passing on-pass messaging, decision 010 linked
- `dydo/reference/roles/planner.md` — removed `--wait` option, removed dispatch-and-wait transition
- `dydo/project/decisions/010-baton-passing-and-review-enforcement.md` — added summary paragraph

## Review Summary

(Pending)

## Notes for Brian

Template files (`Templates/mode-code-writer.template.md`, `mode-reviewer.template.md`, `mode-planner.template.md`) need matching updates but are outside docs-writer write permissions. Specific changes needed:
- **code-writer template**: Remove "Reporting back to origin?" manual messaging section (baton-passing makes this unnecessary when dispatching reviewer for same task)
- **planner template**: Add `--auto-close` to the dispatch example (line 129)
- **reviewer template**: Add upstream messaging in the PASS completion section (reply obligation for last-in-chain agents)