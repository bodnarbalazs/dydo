---
title: Dispatch role validation sees dispatcher as unknown role and orchestrator gating blocks chief-of-staff routing
id: 237
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

# Dispatch role validation sees dispatcher as unknown role and orchestrator gating blocks chief-of-staff routing

dydo dispatch --role orchestrator failed for a chief-of-staff caller: validation resolved the caller role as unknown and the prior-co-thinker-experience rule makes orchestrator dispatch impossible from the documented CoS routing path.

## Description

## Observed

`dydo dispatch --to Grace --role orchestrator --task adopt-orphaned-codex-slices --brief-file ... --auto-close --tab` failed (exit 2) with:

    You are a unknown role. Orchestrator requires prior co-thinker experience on this task. Ask the user for clarification.

The dispatching agent was Adele with role `chief-of-staff`, correctly set and visible in `dydo agent status` (session 55ced0cd, 2026-07-08).

## Two defects

1. **Dispatcher role resolves to "unknown"**: the validation path does not see the caller's actual role (`chief-of-staff`), even though claim + role were set normally and every other command respected them. Whatever state the dispatch validation reads is not the same state `dydo agent status` reads.

2. **Orchestrator gating blocks the chief-of-staff routing path**: requiring "prior co-thinker experience on this task" before an orchestrator dispatch makes it impossible for a chief-of-staff to route work to a fresh orchestrator session — which is exactly the documented routing model (chief-of-staff mode file: "Routing means messaging or, when a fresh session is warranted, a top-level dispatch of an orchestrator or co-thinker"). The rule as implemented forces the workaround of dispatching a co-thinker and having them self-convert, which is what was done here.

Grammar nit while in there: "a unknown role" → "an unknown role".

## Impact

Chief-of-staff cannot dispatch orchestrators at all; routing doctrine and enforcement disagree. Workaround exists (dispatch co-thinker), severity medium.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)