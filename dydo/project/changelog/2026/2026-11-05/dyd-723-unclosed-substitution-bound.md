---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Reading a command is bounded in time and memory (unreleased)

## Summary

The guard could no longer finish reading a command nested a few dozen levels deep
([DYD-723](https://linear.app/bodnar-balazs/issue/DYD-723)). `printf x` followed by 64 unclosed `$(`
ran a probe to 39 GB and the session's desktop was killed. In PowerShell's reading, which every
command also gets, each group word copied the words of every command inside it, a nested group's
commands included, so the word text doubled with each `$(`, `@(` or argument `(`, closed or not.
Analysing any command of up to 100,000 characters now takes well under a second and a few hundred
megabytes at most, and a command nested past the reading's bound is refused.

## Changed

- A PowerShell group word holds each command read inside it once: a nested group's commands reach
  it through that group's own word. `Write-Output @(b @(c))` reads its group word as `b c`, where it
  was `c b c`. A group with no group inside it reads as before, and so does a quoted substitution's
  command inside a group. A word's character origins follow the same rule and are referenced, not
  copied, so a group word costs one entry per word it takes.
- A reading nested more than 32 levels deep, in substitutions and groups, fails as too deep. The
  guard refuses such a command as "Command too complex to verify safely", in every dialect, and so
  when the too-deep text is a command string a shell runs, such as `bash -c '...'`. Up to 32 levels
  every command reads as before. A command with no more than 32 `(` is not read for this check.
- A PowerShell single-quoted string reads its doubled quotes in a loop. It recursed once per `''`,
  and 10,000 of them overflowed a 1 MB stack, the size of a Windows main thread's.
- The command-substitution and variable-expansion bypass searches run without backtracking, and a
  command's literal single-quoted data is emptied in one pass. Each was quadratic on a long run of
  unclosed `$(`, unclosed `${` or doubled `''`.
- In bash's reading an unquoted `$(` was never a recursion, so `printf x` followed by any number of
  unclosed `$(` is read in full, linearly, and judged through Claude's Bash tool as before. Through
  the PowerShell tool and in an unknown shell it is refused at the bound.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents. The bound exists so the guard never takes the host down.

- A wide command full of `(`, such as 20,000 `$(y)` in a row, is read once more for the bound, and
  analysing it at 100,000 characters allocates about 220 MB, where it allocated about 140 MB.

## Files Changed

`Services/ShellReader.cs`, `Services/BashCommandAnalyzer.cs`,
`DynaDocs.Tests/Services/ShellReaderTests.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, and this entry.

## Related

- [dydo 3.6.0 - Long commands are verified within the guard's budget](./dyd-610-guard-regex-budget.md)
- [dydo 3.6.0 - The guard reads PowerShell's carriage returns, typographic quotes and carets](./dyd-641-shellreader-pwsh.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
