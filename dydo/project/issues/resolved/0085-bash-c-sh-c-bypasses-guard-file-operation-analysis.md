---
id: 85
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# bash -c / sh -c bypasses guard file operation analysis

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

TryAnalyzeShellC at Services/BashCommandAnalyzer.cs:517 extracts bash/sh/zsh -c payloads via FindShellCArgument and recursively calls AnalyzeSubCommand+AnalyzeRedirection. InlineInterpreterRegex (:288) blocks python/node/ruby/perl/php inline execution outright. Fix commits ea35282 and 4b162e2. Verified by Adele.