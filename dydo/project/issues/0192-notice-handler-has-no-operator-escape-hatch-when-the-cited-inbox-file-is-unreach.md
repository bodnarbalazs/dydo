---
title: NOTICE handler has no operator escape hatch when the cited inbox file is unreachable by the calling agent
id: 192
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# NOTICE handler has no operator escape hatch when the cited inbox file is unreachable by the calling agent

GuardCommand.NotifyUnreadMessages self-heals 'id in state.md, file absent' but does NOT handle the inverse — 'file present in inbox of an agent the calling process cannot resolve to' — leaving the operator in an unrecoverable file-IO deadlock. InboxService.ExecuteClear also has no '--force --file <path>' option. With F1/issue #0183 fixed the phantom-file mechanism stops, but the deadlock primitive remains a latent class — any future bug that lands a file in an unreachable inbox re-triggers it. Defense-in-depth fix.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)