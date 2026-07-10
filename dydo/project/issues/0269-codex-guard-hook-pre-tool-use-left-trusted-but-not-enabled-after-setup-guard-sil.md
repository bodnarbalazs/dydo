---
title: Codex guard hook (pre_tool_use) left trusted-but-not-enabled after setup - guard silently would not fire; only the stop hook gets enabled
id: 269
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-10
---

# Codex guard hook (pre_tool_use) left trusted-but-not-enabled after setup - guard silently would not fire; only the stop hook gets enabled

v2.0.7 acceptance smoke (2026-07-10): the c1-4 dispatch preflight correctly BLOCKED a codex dispatch as unguarded. Ground-truth cause in ~/.codex/config.toml [hooks.state]: the pre_tool_use hook (THE dydo guard) had a trusted_hash recorded but NO 'enabled = true', while the stop hook of the same hooks.json had both trusted_hash and enabled=true. So codex would silently skip the guard hook while running the stop hook - the exact 'guard never fires' risk, manifesting via the per-hook enabled flag rather than the hash. c1-4's preflight caught it (feature works, fails safe), but the SETUP left the guard in that state. Root cause candidates: (a) codex's interactive hook-approval UX enables hooks per-event and the pre_tool_use approval did not write enabled=true; (b) dydo init/sync's .codex/hooks.json generation or a hash change from a regen left the guard entry stale. Fix directions: dydo init/sync should write/repair the [hooks.state] trust entry for the pre_tool_use guard hook directly (the Codex guard adapter follow-up from DR-037), OR the codex-host setup docs must call out enabling the pre_tool_use hook explicitly. Adjacent to 0254's codex-guard-adapter scope. Found live by balazs+Adele during c1-8.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)