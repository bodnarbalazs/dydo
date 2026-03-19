---
area: general
type: changelog
date: 2026-03-19
---

# Task: fix-inactive-agent-messaging

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxMetadataReaderTests.cs — Modified


## Review Summary

Implemented contextual error messages for inactive agent messaging. Changes: (1) Added GetActiveAgents() and GetActiveOversightAgents() to AgentRegistry (2) Rewrote CheckTargetActive in MessageService to show released-agent context: active agent list, dispatcher suggestion via DispatchedBy, oversight agent suggestions (3) --force only offered in reply-pending edge case, not the general case (4) Added FormatActiveAgentList, BuildInactiveTargetMessage, BuildReplyPendingMessage helpers. 10 new tests, all 2797 pass. No plan deviations.

## Code Review (2026-03-19 18:37)

- Reviewed by: Emma
- Result: FAILED
- Issues: Three issues: (1) subject param passed to CheckTargetActive but never used — reply-pending markers have a Task field and should be filtered by subject when provided, otherwise agent A with reply-pending for task X to agent B will get false-positive reply-pending message when sending about task Y. (2) BuildInactiveTargetMessage re-fetches GetAgentState(to) when CheckTargetActive already has targetState — pass it as a parameter. (3) Test Message_ToInactiveAgent_NoActiveAgents_SuggestsHuman: name claims 'no active agents' but sender is always active; test actually checks 'only sender active shows list'. Fix name and remove stream-of-consciousness comments (lines 45-48).

Requires rework.

## Approval

- Approved: 2026-03-19 18:47
