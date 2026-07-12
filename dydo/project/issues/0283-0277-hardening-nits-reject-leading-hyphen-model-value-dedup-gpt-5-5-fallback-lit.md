---
title: 0277 hardening nits: reject leading-hyphen model value; dedup gpt-5.5 fallback literal; escape control chars in model error display
id: 283
area: backend
type: issue
severity: low
status: open
found-by: review
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
---

# 0277 hardening nits: reject leading-hyphen model value; dedup gpt-5.5 fallback literal; escape control chars in model error display

Non-blocking nits from the 0277-round2 Claude review (fix landed fbaed15a, injection closed). (1) IsValidCodexModel (TerminalLauncher.cs:181) admits leading-hyphen values so a hostile dydo.json could put 'codex -m --dangerously-bypass-approvals-and-sandbox ...' on the line - VERIFIED INERT (the token sits in -m's value position, never applied as a flag; codex binds it as the model string or errors), but it makes the e2e 'bypass flag never on the line' invariant defeatable by config; reject a leading '-' in IsValidCodexModel (one char) to restore the invariant. (2) 'gpt-5.5' fallback literal is duplicated in TerminalLauncher.DefaultCodexModel (TerminalLauncher.cs:11) and SyncCommand.cs:233 - have one reference the other so a sync-fallback change doesn't silently drift dispatch. (3) DisplayModelValue (TerminalLauncher.cs:193) escapes only \r/\n; other control chars (ESC) in a hostile model value flow raw into the dispatcher console error (cosmetic terminal-escape into the error message only, never a command line). Good small codex-infra follow-up; the leading-hyphen one is the one worth doing for the security posture.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)