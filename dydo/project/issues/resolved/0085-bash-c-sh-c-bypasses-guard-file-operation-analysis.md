---
title: bash -c / sh -c bypasses guard file operation analysis
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 85
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# bash -c / sh -c bypasses guard file operation analysis
Resolved medium-severity security bug: the guard's bash file-operation analysis didn't descend into `bash -c` / `sh -c` / `zsh -c` payloads, so wrapping a write in `bash -c '...'` bypassed the path checks. Fixed by extracting shell-`-c` payloads via `TryAnalyzeShellC` and recursively analyzing them; commits `ea35282` and `4b162e2`.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
TryAnalyzeShellC at Services/BashCommandAnalyzer.cs:517 extracts bash/sh/zsh -c payloads via FindShellCArgument and recursively calls AnalyzeSubCommand+AnalyzeRedirection. InlineInterpreterRegex (:288) blocks python/node/ruby/perl/php inline execution outright. Fix commits ea35282 and 4b162e2. Verified by Adele.