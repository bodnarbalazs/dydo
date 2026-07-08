---
title: Codex support docs still describe Claude-only setup paths
id: 232
area: reference
type: issue
severity: medium
status: resolved
found-by: inquisition
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
resolved-date: 2026-07-08
---

# Codex support docs still describe Claude-only setup paths

Reference docs and templates omit automatic Codex hooks and direct Codex users to Claude init/join paths.

## Description

Inquisition doc-drift finding: Codex is now wired through .codex/hooks.json and dydo init codex --join, but several docs still say hooks are automatic only for Claude or direct users to dydo init claude. Examples: dydo/reference/dydo-commands.md and Templates/dydo-commands.template.md say only Claude Code hooks are automatic; dydo/reference/configuration.md documents .claude/settings.local.json but not .codex/hooks.json or Codex matchers; dydo/reference/about-dynadocs.md and Templates/about-dynadocs.template.md use dydo init claude --join only; Templates/agent-workflow.template.md says DYDO_HUMAN fix is dydo init claude; dydo/reference/roles/inquisitor.md says inquisitor exists only as a Claude Code agent + skill.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in dc1333b: guard/init docs describe both Claude (.claude/settings.local.json) and Codex (.codex/hooks.json) wiring; configuration reference gains integrations.codex, the Stop hook, and per-runtime matchers (incl. apply_patch); runtime-neutral phrasing (CLAUDE.md or AGENTS.md) across about-dynadocs.md, its template, and the README clone triple; 'dydo init codex --join' documented; inquisitor role doc lists Codex artifact paths. A sprint-audit finding (four README paragraphs left diverged outside the clone-sync test's enforced sections) was fixed and re-reviewed before landing.
