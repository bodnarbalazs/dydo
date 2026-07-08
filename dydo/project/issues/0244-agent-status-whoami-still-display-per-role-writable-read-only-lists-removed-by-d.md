---
title: agent status / whoami still display per-role writable/read-only lists removed by DR 024
id: 244
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

# agent status / whoami still display per-role writable/read-only lists removed by DR 024

Per-role write RBAC was removed in dydo 2.0 (DR 024 s2) - the guard ignores writable/read-only path lists - but dydo agent status and whoami still print them, misleading agents into thinking paths are enforced or forbidden. Drop or reword the display. Note issue 0223 may overlap - dedup on triage. Routed from auto-memory per DR 038 initial sweep.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)