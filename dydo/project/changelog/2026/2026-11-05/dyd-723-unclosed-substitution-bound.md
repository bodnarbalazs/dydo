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
- A reading nested more than 32 levels deep, in substitutions, groups and `(`, fails as too deep.
  The guard refuses such a command as "Command too complex to verify safely", in every dialect, and
  so when the too-deep text is a command string a shell runs, such as `bash -c '...'`. bash reads an
  unquoted `$(` as `$` and a group, so 33 of them nested are refused through Claude's Bash tool too,
  as through the PowerShell tool and in an unknown shell. Up to 32 levels every command reads as
  before.
- **Behaviour change.** The command strings a command runs (`bash -c`, `eval`, `cmd /c`, `pwsh -c`
  and the others) are read eight levels below it, as since DYD-406. A ninth level was read as
  uncertain and the command allowed, unread; it is now refused as too complex to verify, so a
  substitution nested past the bound inside nine `bash -c` cannot pass unread. `eval` written nine
  times, or nine `bash -c` around any command, is refused where it was allowed.
- Each command line a command runs is read once however many readings run it, and reading them has
  a budget: about ten readings of the command's length. `cmd /c` reads its command string both
  ways, so each level of a chain of `cmd` was read twice as often as the level above, 512 times at
  the eighth, and 7,000 characters of it allocated 835 MB. A chain of eight `cmd` around a short
  command reads as before; around a long one, which reads past the budget, it is refused as too
  complex.
- A PowerShell single-quoted string reads its doubled quotes in a loop. It recursed once per `''`,
  and 10,000 of them overflowed a 1 MB stack, the size of a Windows main thread's.
- The command-substitution and variable-expansion bypass searches are scans, not regexes: each
  looks for a closer once, however many openers precede it, and finds what the regexes found. A
  command's literal single-quoted data is emptied in one pass. Each was quadratic on a long run of
  unclosed `$(`, unclosed `${` or doubled `''`.
- A command with no heredoc or here-string opener is not read to cut literal-data bodies, and one
  with no single quote is not read for literal single-quoted data. Neither reading could change
  the answer, and skipping them cuts a long command's allocation by more than half.
- A data command's single quotes are paired with its words in one walk, and sed's script is found
  once. Each quote searched the words from the first, and for sed found the script again, so
  `sed 's/a/b/'` followed by 33,329 `''` took seven seconds.
- Cutting heredoc and here-string bodies reads each piece once. Each cut read, split and copied the
  whole rest of the command, so 4,000 heredocs took eight seconds and allocated 22 GB. The rest
  must still read, as before: the command is read whole once, and each cut's own text from its
  start to the line after its body, unless that whole reading read the heredoc to the same line.
  Where a piece read alone fails but the whole rest would read, the cutting stops there and the
  rest stays code.
- The heredoc and here-string searches start only at an opener a later line closes. One that
  nothing closes, such as `<<E` in quoted text, was searched to the end of the command, once per
  opener: 16,000 of them took 1.6 seconds. The search finds what it found.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents. The bound exists so the guard never takes the host down.

- Every command is read once more for the bound, since a command string nests with no `(`. A
  long command allocates a few tens of megabytes more for it; at 100,000 characters the most any
  measured shape allocates is about 180 MB, many heredocs.
- The command strings a command runs are still read only eight levels deep, below the 16 the
  reading's own bound must allow: raising it would multiply the reading of a long chain.
- The 256 MB and one-second bounds are Release, uninstrumented. Coverage instrumentation multiplies
  allocation by up to about 30, so the test holds the absolute budget at 10,000 characters and the
  growth ratios at 100,000; the one-second bound is asserted when the tests run in Release.

## Files Changed

`Services/ShellReader.cs`, `Services/BashCommandAnalyzer.cs`,
`DynaDocs.Tests/Services/ShellReaderTests.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, and this entry.

## Related

- [dydo 3.6.0 - Long commands are verified within the guard's budget](./dyd-610-guard-regex-budget.md)
- [dydo 3.6.0 - The guard reads PowerShell's carriage returns, typographic quotes and carets](./dyd-641-shellreader-pwsh.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
