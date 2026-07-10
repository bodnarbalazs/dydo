---
title: dydo sync emits .codex/agents/*.toml tools as a comma-string, but codex wants a ToolsToml struct - codex ignores every compiled agent role
id: 271
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

# dydo sync emits .codex/agents/*.toml tools as a comma-string, but codex wants a ToolsToml struct - codex ignores every compiled agent role

Live 2026-07-10 (codex run in repo during c1-8 setup): OpenAI Codex rejects ALL SIX dydo-compiled codex agent role files with 'Ignoring malformed agent role definition: failed to deserialize ... invalid type: string "read, grep, glob, bash, edit, write", expected struct ToolsToml' (code-writer/docs-writer/inquisitor/reviewer/sprint-auditor/test-writer). Cause: dydo sync's Codex compiler writes the tools field as a comma-separated STRING (tools = "read, grep, glob, bash, edit, write" at .codex/agents/*.toml:4), but codex's schema expects a ToolsToml STRUCT. So codex silently ignores every compiled worker role - the DR-024 dual-compilation Codex leg is non-functional: a codex-hosted workflow has NO valid dydo worker subagents. Fix: the sync Codex emitter must serialize tools in codex's expected ToolsToml struct shape (verify the exact schema against the codex agent-role reference - it is NOT a bare string). Add a codex-toml-shape assertion test (the sync tests validated content, not codex-parseability - same fake-vs-wire gap class as 0261). Blocks codex-runs-a-workflow (adoption step 3); does NOT block the c1-8 dispatched-Tier-1 smoke (that path uses inbox/claim, not compiled worker roles). 2.0.8 codex-enablement wave. Found by balazs+Adele.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)