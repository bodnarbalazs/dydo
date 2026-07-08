---
title: dispatch --auto-close is opt-in but forgetting it is the common case - make it default or nudge
id: 242
area: backend
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# dispatch --auto-close is opt-in but forgetting it is the common case - make it default or nudge

Released agents' terminal tabs linger whenever the dispatcher forgets --auto-close, which happened repeatedly on 2026-07-07 and is the common failure mode. Either make auto-close the default (with --no-auto-close opt-out) or add a dispatch-time nudge when absent. Routed from auto-memory per DR 038 initial sweep.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)