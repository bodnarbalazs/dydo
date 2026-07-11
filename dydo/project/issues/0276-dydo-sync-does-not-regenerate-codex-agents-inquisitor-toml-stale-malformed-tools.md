---
title: dydo sync does not regenerate .codex/agents/inquisitor.toml - stale malformed tools line + stale model (0271 emitter-fix coverage gap)
id: 276
area: backend
type: issue
severity: medium
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# dydo sync does not regenerate .codex/agents/inquisitor.toml - stale malformed tools line + stale model (0271 emitter-fix coverage gap)

c1-8 setup (2026-07-11): after installing the 0271-fixed HEAD build and running dydo sync, the 5 worker-role .codex/agents/*.toml regenerated CLEAN (tools line dropped, model = gpt-5.6-terra from the openai tier). But .codex/agents/inquisitor.toml was NOT regenerated - it retained the malformed tools = 'read, grep, glob, bash' line AND a stale model = gpt-5.5 (not the openai tier mapping). So dydo sync's Codex emitter covers worker roles + sprint-auditor but MISSES inquisitor (a QA agent). Either sync should emit inquisitor.toml through the 0271-fixed path, or the file is orphaned cruft sync no longer manages and should be removed. Stopgap applied by hand (dropped the tools line so codex stops rejecting it), but root cause is a sync-coverage gap - the 0271 emitter fix does not reach the inquisitor codex artifact, and its model is not tier-resolved. Route to Grace (0271/2.0.8 completeness) or the codex-emitter follow-up. Also verify no other QA/non-worker codex agent (judge, etc.) is similarly skipped.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)