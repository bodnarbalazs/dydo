---
id: 49
area: general
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# CompletionProvider arrays are stale — missing commands, roles, subcommands, and option handlers

Resolved high-severity correctness bug: the static arrays in `CompletionProvider` had drifted significantly behind the actual command tree, missing many top-level commands, roles, subcommands, and the `--subject` option. Fixed in commit `1ee0ba9` by refreshing the arrays.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in commit 1ee0ba9: CompletionProvider updated with all missing commands, roles, subcommands, and --subject option