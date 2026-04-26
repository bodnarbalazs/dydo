---
id: 40
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-26
---

# IsPathAllowed logic duplicated between PathPermissionChecker and AgentRegistry

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Coupled to #0039: with PathPermissionChecker removed in commit 99a9a33, IsPathAllowed logic now lives only in AgentRegistry. No remaining duplication.