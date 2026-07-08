---
title: dydo model has no status subcommand - active cap state is not inspectable
id: 238
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

# dydo model has no status subcommand - active cap state is not inspectable

dydo model only offers cap/uncap; there is no way to ask whether a cap is currently active, on which model, with what fallback and reset time. Today the only signal is diffing the model fields in .claude/agents/*.md (fallback model = capped), and dydo/_system is guard-off-limits so agents cannot read the cap state file directly. A dydo model status subcommand (and a line in dydo whoami/agent status) would make outage state visible to the CoS and to briefs.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)