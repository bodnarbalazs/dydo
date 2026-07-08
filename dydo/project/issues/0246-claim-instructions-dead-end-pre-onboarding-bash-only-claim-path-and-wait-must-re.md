---
title: Claim instructions dead-end pre-onboarding: Bash-only claim path and wait/must-read staging not discoverable at stage 0
id: 246
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# Claim instructions dead-end pre-onboarding: Bash-only claim path and wait/must-read staging not discoverable at stage 0

dydo agent claim works only via the Bash tool (the guard hook plumbs the session ID down that path; PowerShell fails with no actionable hint), the claim binding lands after the hook completes so claim-and-whoami chained in one call misleads, and the general-wait + must-read requirements are only revealed by successive guard blocks. The durable onboarding sequence (claim via Bash, set role, background dydo wait, read must-reads) should live in the stage-0-readable workflow.md template so fresh agents do not rediscover it by trial and error. Routed from auto-memory per DR 038 initial sweep; the memory dydo-claim-via-bash-only stays as a pending-fix buffer entry until this lands (see repo issue 0211 for claim-store history).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)