---
title: guard --command manual-testing mode never applies shell analysis
id: 302
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# guard --command manual-testing mode never applies shell analysis

dydo guard --command "cat .env" exits 0: arg-mode input has no toolName, so ShouldRouteToShellHandler never routes to the shell analyzer and every command is allowed.

## Description

GuardCommand.ParseInput only sets toolName from stdin hook input. The documented manual-testing lane (dydo guard --command ...) therefore bypasses off-limits, dangerous-bash, and nudges entirely — verified: npx dydo and cat .env both exit 0 via --command in both DynaDocs and Pokercept, while the same commands block correctly in stdin hook mode. Fix: treat a CLI-provided --command as a bash tool call so it routes through HandleBashCommand.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3. A CLI-provided `--command` is now routed as a shell tool call, so
manual guard testing applies off-limits checks, dangerous-command analysis, and nudges.
