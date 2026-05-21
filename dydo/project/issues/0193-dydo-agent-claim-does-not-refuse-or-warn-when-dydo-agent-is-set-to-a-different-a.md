---
id: 193
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-19
---

# dydo agent claim does not refuse or warn when DYDO_AGENT is set to a different agent

AgentLifecycleHandlers.ExecuteClaim and AgentRegistry.ClaimAgent do not check whether the calling process has DYDO_AGENT set to a name that disagrees with the target of the claim. With F1 in place this becomes purely a UX improvement; without F1 the operator's first role-set after the claim silently hijacks the previous agent's record. Refuse with an actionable error pointing at the unset command.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)