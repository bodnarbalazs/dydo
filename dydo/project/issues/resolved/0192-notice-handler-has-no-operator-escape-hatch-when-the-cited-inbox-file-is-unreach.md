---
title: NOTICE handler has no operator escape hatch when the cited inbox file is unreachable by the calling agent
id: 192
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-12
---

# NOTICE handler has no operator escape hatch when the cited inbox file is unreachable by the calling agent

GuardCommand.NotifyUnreadMessages self-heals 'id in state.md, file absent' but does NOT handle the inverse — 'file present in inbox of an agent the calling process cannot resolve to' — leaving the operator in an unrecoverable file-IO deadlock. InboxService.ExecuteClear also has no '--force --file <path>' option. With F1/issue #0183 fixed the phantom-file mechanism stops, but the deadlock primitive remains a latent class — any future bug that lands a file in an unreachable inbox re-triggers it. Defense-in-depth fix.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-12 (landed b0b3086c). Defense-in-depth escape from the unreachable-inbox deadlock: dydo inbox clear --force --file archives a specific inbox file, BOUNDED to genuinely-orphaned inboxes (owner has no live .session) - live owners are refused (closes cross-agent tampering); path validation rejects traversal/cross-drive/non-agent paths; archives never deletes. NotifyUnreadMessages recovery hint reworked to detect the REAL clear-side (GetSessionContext) resolution failure (round-1's hint was dead code + could tell an agent to archive its own readable mail). Codex Charlie (Terra, 2 rounds), Claude security-reviewed PASS (orphaned precondition + path validation fail-closed, authoritative server-side backstop). New options documented in dydo-commands.md + template. Non-blocking follow-ups noted (hint 'read-first' wording, IAgentRegistry seam, dead-PID orphan test).