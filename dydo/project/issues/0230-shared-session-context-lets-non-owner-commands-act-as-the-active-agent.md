---
title: Shared session context lets non-owner commands act as the active agent
id: 230
area: backend
type: issue
severity: high
status: in-flight
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Shared session context lets non-owner commands act as the active agent

Mutating commands trust shared .session-context without caller ownership, allowing msg/dispatch/provenance to run as another active agent.

## Description

Inquisition finding: AgentRegistry.GetSessionContext falls back to shared .session-context when DYDO_AGENT is absent or not owned. MessageService and DispatchService then call GetCurrentAgent(sessionId) and use that identity without VerifyCallerOwnsAgent. Consequence: a plain shell or wrong terminal can send messages, dispatch agents, or stamp found-by/from/assigned provenance as the last shared-context agent. Evidence: Services/AgentRegistry.cs GetSessionContext and GetCurrentAgent; Services/AgentSessionManager.cs GetSessionContext; Services/MessageService.cs; Services/DispatchService.cs. WaitCommand has the ownership gate, so this is an inconsistent trust boundary.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)
