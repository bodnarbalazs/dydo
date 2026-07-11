---
title: dydo read empty/whitespace target throws ArgumentException instead of friendly not-found error
id: 280
area: backend
type: issue
severity: low
status: open
found-by: review
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# dydo read empty/whitespace target throws ArgumentException instead of friendly not-found error

Surfaced by the 0274 cross-vendor review (landed 7d21f85c): after moving off-limits before existence, an empty/whitespace target reaches Path.GetFullPath("") which throws ArgumentException (unhandled -> stack trace) instead of the friendly 'neither an inbox message id nor an existing file path' error it produced pre-change (empty string used to die harmlessly in File.Exists). Fails CLOSED - no security/off-limits impact, false-block side only. One-line fix: string.IsNullOrWhiteSpace(target) fall-through to the existing not-found error, before ResolveWorktreePath/absolutize. Good tiny codex dogfood or fold into a read follow-up.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)