---
area: general
type: changelog
date: 2026-04-29
---

# Task: fix-agent-tool-nudge-wording

Reviewing fix(guard) commit 20e7b10. Changes the Agent-tool soft-nudge from forced-retry-with-marker (BLOCKED + .agent-tool-nudge marker) to warn-and-allow stderr notice that returns Success on every call. New message text (verbatim from Adele's brief) frames the purpose distinction between the Agent tool (subagent inherits identity/role/permissions, fine for read discovery and autonomous code-writing in same task) and dydo dispatch (fresh stateful role-scoped agent in its own session, for separable work). The CheckAgentToolNudge helper became a one-line stderr write so I inlined it at the HandleSearchTool call site (one helper deleted, no behavior change). AgentRegistry cleanup of .agent-tool-nudge marker removed (marker no longer exists; .no-launch-nudge-* and .no-wait-nudge-* cleanups untouched). Tests: First/SecondCall and BypassMarker and MarkerCleanedOnRelease deleted; FirstCall_FailsWithNudge renamed to IdentityWithRole_EmitsNudgeAndPasses; EmptyToolInput_FailsWithNudge renamed to EmptyToolInput_EmitsNudgeAndPasses; both now assert Success + 'NOTICE' + 'dydo dispatch' + 'subagent inherits'. GlobTool_DoesNotFireAgentNudge marker assertion (now vacuous) replaced with stderr DoesNotContain checks for NOTICE/subagent. NoIdentity_Blocks and IdentityNoRole_Blocks unchanged. BypassAgentToolNudge helper removed from IntegrationTestBase. Tests: 3867 pass, gap_check 136/136 (delta -3 tests). Blocker: dydo/reference/guardrails.md S6 row still describes the old behavior; outside code-writer writable paths, flagged to Adele for docs-writer dispatch. No worktree this round per Adele.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Reviewing fix(guard) commit 20e7b10. Changes the Agent-tool soft-nudge from forced-retry-with-marker (BLOCKED + .agent-tool-nudge marker) to warn-and-allow stderr notice that returns Success on every call. New message text (verbatim from Adele's brief) frames the purpose distinction between the Agent tool (subagent inherits identity/role/permissions, fine for read discovery and autonomous code-writing in same task) and dydo dispatch (fresh stateful role-scoped agent in its own session, for separable work). The CheckAgentToolNudge helper became a one-line stderr write so I inlined it at the HandleSearchTool call site (one helper deleted, no behavior change). AgentRegistry cleanup of .agent-tool-nudge marker removed (marker no longer exists; .no-launch-nudge-* and .no-wait-nudge-* cleanups untouched). Tests: First/SecondCall and BypassMarker and MarkerCleanedOnRelease deleted; FirstCall_FailsWithNudge renamed to IdentityWithRole_EmitsNudgeAndPasses; EmptyToolInput_FailsWithNudge renamed to EmptyToolInput_EmitsNudgeAndPasses; both now assert Success + 'NOTICE' + 'dydo dispatch' + 'subagent inherits'. GlobTool_DoesNotFireAgentNudge marker assertion (now vacuous) replaced with stderr DoesNotContain checks for NOTICE/subagent. NoIdentity_Blocks and IdentityNoRole_Blocks unchanged. BypassAgentToolNudge helper removed from IntegrationTestBase. Tests: 3867 pass, gap_check 136/136 (delta -3 tests). Blocker: dydo/reference/guardrails.md S6 row still describes the old behavior; outside code-writer writable paths, flagged to Adele for docs-writer dispatch. No worktree this round per Adele.

## Code Review

- Reviewed by: Leo
- Date: 2026-04-28 20:44
- Result: PASSED
- Notes: PASS. Commit 20e7b10 cleanly inverts the Agent-tool nudge from forced-retry-with-marker to warn-and-allow stderr notice. Inlined Console.Error.WriteLine at the HandleSearchTool agent branch (helper deleted, no behavior change), .agent-tool-nudge cleanup removed from AgentRegistry, .no-launch-nudge-* and .no-wait-nudge-* cleanups intact. Notice text is accurate (subagent inherits identity/role/permissions per same PreToolUse hook) and frames the purpose distinction with dydo dispatch as Adele specified. Tests: 3867 pass, gap_check 136/136 (forced-run, fresh worktree). Renamed tests assert Success + 'NOTICE' + 'dydo dispatch' + 'subagent inherits'; marker-based tests (SecondCall/BypassMarker/MarkerCleanedOnRelease) correctly deleted; GlobTool_DoesNotFireAgentNudge updated to negative stderr assertions; NoIdentity_Blocks and IdentityNoRole_Blocks unchanged (DoesNotContain 'dydo dispatch' still holds since notice fires only after stage-2). All edits within code-writer scope. Out-of-scope (already flagged by Emma): dydo/reference/guardrails.md S6 row still describes old marker behavior — needs docs-writer dispatch via Adele.

Awaiting human approval.

## Approval

- Approved: 2026-04-29 12:04
