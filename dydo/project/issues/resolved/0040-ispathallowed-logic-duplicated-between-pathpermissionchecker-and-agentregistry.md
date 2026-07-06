---
title: IsPathAllowed logic duplicated between PathPermissionChecker and AgentRegistry
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 40
type: issue
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# IsPathAllowed logic duplicated between PathPermissionChecker and AgentRegistry
Resolved medium-severity duplication finding: `IsPathAllowed` logic existed in both `PathPermissionChecker` and `AgentRegistry`. Resolved as a coupled fix to #0039 — when `PathPermissionChecker` was deleted in commit `99a9a33`, the duplication disappeared and `AgentRegistry` became the single home for the logic.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Coupled to #0039: with PathPermissionChecker removed in commit 99a9a33, IsPathAllowed logic now lives only in AgentRegistry. No remaining duplication.