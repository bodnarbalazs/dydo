---
title: dydo model has no status subcommand - active cap state is not inspectable
id: 238
area: backend
type: issue
severity: low
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
resolved-date: 2026-07-12
---

# dydo model has no status subcommand - active cap state is not inspectable

dydo model only offers cap/uncap; there is no way to ask whether a cap is currently active, on which model, with what fallback and reset time. Today the only signal is diffing the model fields in .claude/agents/*.md (fallback model = capped), and dydo/_system is guard-off-limits so agents cannot read the cap state file directly. A dydo model status subcommand (and a line in dydo whoami/agent status) would make outage state visible to the CoS and to briefs.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-13 (landed 58dd6d90). Added 'dydo model status' - reads ModelCap markers, prints target/fallback/reset per active cap (or 'no active caps'), making cap/outage state inspectable without reading guard-off-limits _system. Documented in help + about-dynadocs. Codex Frank (Terra), reviewed. Follow-up: the whoami/agent-status cap line.