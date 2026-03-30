---
type: decision
status: accepted
date: 2026-03-30
area: cli
---

# 015 — Shell Completions: Keep Code, Remove from Docs

## Context

`dydo completions` generates shell completion scripts (bash/zsh/powershell) and has a full infrastructure: `CompletionsCommand`, `CompletionProvider`, `CompleteCommand` (`_complete`), and `ShellCompletionInstaller`. Unit test coverage is solid (19+ tests).

However, dynamic completions (e.g., tab-completing task names like `dydo task approve cli-<tab>`) don't work end-to-end. The static command tree completing is less valuable since users already know those commands.

## Decision

Keep all completion code in place. Remove references from user-facing documentation (command reference, README, about-dynadocs). Mark as a future feature — the plumbing is there for when someone makes dynamic completions work end-to-end.

## Rationale

- The code costs nothing to maintain sitting idle
- Removing it would mean rebuilding from scratch later
- Advertising a feature that doesn't deliver its key value (dynamic completions) misleads users
