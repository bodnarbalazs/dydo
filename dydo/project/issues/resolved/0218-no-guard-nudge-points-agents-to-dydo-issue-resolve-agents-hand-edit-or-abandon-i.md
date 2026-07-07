---
id: 218
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-07
resolved-date: 2026-07-07
---

# No guard nudge points agents to 'dydo issue resolve' — agents hand-edit or abandon issue closure

dydo issue resolve <id> --summary exists but nothing surfaces it: an agent whose work fixes an issue may hand-edit the issue file, or (if issues/ is outside its writable paths) wrongly conclude it can't be closed and abandon it. A guard nudge should detect issue-file status edits and steer agents to the CLI.

## Description

`dydo issue resolve <id> --summary "..."` is the canonical way to close an issue (it flips `status` and records the resolution), but nothing surfaces the command at the point of need. An agent whose work fixes an issue can therefore fail to close it — either by hand-editing the file, or by concluding it "can't close" the issue and abandoning it, leaving the board to accrete stale open issues.

Observed live: a code-writer finished the fix for #216 and reported to the human that the issue "needs an oversight role or docs-writer to close because issues/ is outside my writable paths" — a factually wrong hand-off caused purely by not knowing `dydo issue resolve` exists (it is available regardless of file-write permissions).

## Proposed

A **soft** guard nudge (warn / exit 0 — inject guidance, never block) that fires when an agent hand-edits an issue file's `status:` frontmatter (Write/Edit under `dydo/project/issues/**`) and points it at:

> To resolve an issue, run `dydo issue resolve <id> --summary "..."`.

Scope = nudge only. Two hard constraints on the design:

- **Soft, never a wall.** It guides; it must not block the write. Nobody gets stopped — the point is only that if the situation above recurs, a hint is right there in the tool result.
- **Granular first — examine feasibility before building.** Establish whether the trigger can be targeted precisely (a `status:`-frontmatter change on an issue file) so agents who legitimately edit issue files aren't nagged on every touch. If a clean signal that avoids false positives isn't achievable, that finding shapes (or vetoes) the nudge.

## Notes

Discoverability / workflow-integrity gap, not a correctness bug. A write-triggered nudge inherently only helps the "agent that attempts an edit" path; the pure give-up-without-trying path (the exact case observed) is out of scope for a nudge — flagged here only so a future sprint decides consciously whether separate doc/onboarding work is warranted.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Won't build the nudge. Root cause was a one-off: the agent didn't know 'dydo issue resolve' (already documented in dydo/reference/dydo-commands.md) and wrongly assumed a write-permission wall (2.0 removed per-role write RBAC; only dydo/_system/** and secrets are off-limits). The proposed nudge fires on a Write/Edit to an issue file — an action the agent never took (it gave up), so it would not have prevented the observed failure; building it for an unobserved hand-edit scenario is speculative (anti-slop). Fix applied instead: added a one-line 'dydo issue resolve' reminder next to the existing 'dydo issue create' guidance in the shipped default template Templates/mode-code-writer.template.md (238 template tests green). Caveat: this repo's live mode files render from the off-limits dydo/_system/templates override, so a docs-writer/human should mirror the same line there to make it live here. Real tooling gap tracked separately as #217 (gap_check staleness).