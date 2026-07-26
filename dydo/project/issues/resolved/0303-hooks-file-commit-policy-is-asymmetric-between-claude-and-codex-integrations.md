---
title: Hooks-file commit policy is asymmetric between Claude and Codex integrations
id: 303
area: backend
type: issue
severity: low
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# Hooks-file commit policy is asymmetric between Claude and Codex integrations

A fresh clone is Codex-guarded out of the box (.codex/hooks.json is committed) but unguarded for Claude until dydo init claude --join, and dydo never gitignores .claude/settings.local.json.

## Description

Init wires Claude hooks into .claude/settings.local.json (personal-scope by Claude Code convention) but does not add it to the project .gitignore — it only stays untracked on machines whose global git ignore covers it. Codex hooks land in the committed .codex/hooks.json. Decide and implement one policy: keep Codex committed + have init/join add .claude/settings.local.json to .gitignore and document that each machine runs join for Claude; or move the Claude guard wiring into a committed .claude/settings.json.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3 with an explicit asymmetric policy. Claude wiring remains machine-local
in `.claude/settings.local.json`, which init/join now gitignores; Codex wiring remains committed
in `.codex/hooks.json`. Command documentation tells Claude users to join once per clone.
