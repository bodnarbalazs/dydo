---
title: Codex support docs still describe Claude-only setup paths
id: 232
area: reference
type: issue
severity: medium
status: in-flight
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Codex support docs still describe Claude-only setup paths

Reference docs and templates omit automatic Codex hooks and direct Codex users to Claude init/join paths.

## Description

Inquisition doc-drift finding: Codex is now wired through .codex/hooks.json and dydo init codex --join, but several docs still say hooks are automatic only for Claude or direct users to dydo init claude. Examples: dydo/reference/dydo-commands.md and Templates/dydo-commands.template.md say only Claude Code hooks are automatic; dydo/reference/configuration.md documents .claude/settings.local.json but not .codex/hooks.json or Codex matchers; dydo/reference/about-dynadocs.md and Templates/about-dynadocs.template.md use dydo init claude --join only; Templates/agent-workflow.template.md says DYDO_HUMAN fix is dydo init claude; dydo/reference/roles/inquisitor.md says inquisitor exists only as a Claude Code agent + skill.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)
