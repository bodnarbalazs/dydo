---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Reading a command is bounded in time and memory (unreleased)

## Summary

Fix hop 6 caps command text at **1,048,576 UTF-16 characters**, before guard configuration
or command analysis. Longer commands are always refused, including retries, with
"Command too complex to verify safely: command length exceeds limit of 1048576 characters".
The analyzer enforces the same ceiling. The Python-stdin fallback keeps its exact pattern
and now has the dangerous-pattern 250 ms timeout; exhaustion follows the existing hard-refusal
path. The ceiling supplements that timeout and the existing nesting and reading budgets.

The measured legitimate maximum is 144,028 characters: `DangerousPatternBudgetTests`
`LongLiteralData` repeats its 36-character GitHub command 4,000 times inside a 28-character
literal-data wrapper. Its scaled historical evidence case is 117,867 characters (twenty
5,891-character bodies, nineteen separators and that wrapper). Both DYD-610 cases and all
their assertions remain unchanged. The ceiling gives the largest case 7.28 times headroom.
The largest historical command fixture is `GuardRetroCommands.Gs02CatAppendEvidence`,
6,039 characters; `Gs02SetContentEvidence` is 4,135. The largest command field in the
downstream hook fixtures is 1,526 characters (`TestData/downstream-lc/codex-hooks.json`,
`hooks.PreToolUse[1].hooks[0].command`); the officer-transition corpus maximum is 490.
These measured strings use only BMP characters; the counts above are UTF-16 lengths.
JSON envelope bytes, non-command documents/patterns, dependency/generated files and
malformed-input stress generators are excluded as legitimate command-size evidence.
The latter retain their original tests: all original 50k/100k performance rows remain below
the ceiling. New benign boundary tests assert allowance at the limit and prompt refusal above
it, without replacing or weakening the existing allocation, time or growth assertions.

PowerShell group nudges now follow emitted character ranges instead of whole source-word
ranges. Comments omitted from a nested group word stay exempt; emitted quoted operands still
count. Normalized heredoc origins project back to their original coordinates. The obsolete
test-only `RunsAt` wrapper is removed, with its behavioral assertions moved to the production
`NudgeClassifier` seam. ConfigFactory, nudge captures, messages and retry keys are unchanged.

Fix hop 5 indexes cat-heredoc closers before stripping, replaces the remaining legacy
denylist suffix searches with linear regex matching at the same 250 ms timeout, and visits
overlapping Python runner operand ranges once. The Captain extended ownership to
`Services/CommandPosition.cs`: `env` now passes the original word list and a start index
instead of copying every remaining suffix. New analyzer regressions and compatibility
oracles cover these paths; the DYD-610 oracle and inventory tests are unchanged.

The clean integration merge is `aa07f2709ff78c09c67b6794664b2d24c524047a`, bringing in
feature tip `5cd105878f92f72f7d4217ab2940813bafc3e1fe` after checkpoint `b02e9873`.
On that merged guard, all nudge matches share one reading and classification. Repeated
quoted search operands no longer reparse the whole command for every excluded match.
Mixed heredoc and here-string openers select the earliest candidate before scanning its body.
Heredoc provenance validation does not copy overlapping source ranges when one command
declares many bodies; the inherited long-body regression covers that case.
Quoted-text stripping uses the same pattern with linear matching: an escaped quote can no
longer restart an unmatched search through every subsequent escaped quote.

Every command nudge regex now has a 250 ms timeout, and command nudge matching shares a
250 ms cumulative budget across block, warn and PR-body checks. Exhaustion refuses with
"Command too complex to verify safely", including on retry. Built-in patterns that support
the linear regex engine use it without changing their definitions; lookaround patterns and
configured patterns retain their original engine under the timeout. ConfigFactory definitions,
messages, captures used in messages, and retry identities are unchanged.
The hook also catches regex timeouts before its unexpected-error fallback, so the shared
compiler's timeout cannot make a configured new-file nudge fall through to allowance.

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
- Splitting a command indexes PowerShell's parts once to find which bash parts it lacks. Each bash
  part searched the whole list, so doubling distinct commands quadrupled the matching work. The
  output keeps both readings' order and duplicates within each reading.
- Checking multiline quotes against parsed words builds a substring index once per reading. Each
  quote searched all words before, allocating about 2 GB for 90,000 characters of distinct quoted
  echo commands. Longer words are indexed first, so a group's child words already present in its
  text are not stored again. Substring matches and their treatment of escaped quotes stay the same.
- Nudge provenance checks index completed execution and child memberships once per reading. A long
  pipeline is walked iteratively, with its compound syntax checked once, instead of recursively
  searching every command again for each pipe. Origin lookups skip earlier runs with binary search,
  so many substitutions in a CRLF heredoc do not rescan every preceding line. Quoted data keeps the
  same exemptions.
- Cutting heredoc and here-string bodies reads each piece once. Each cut read, split and copied the
  whole rest of the command, so 4,000 heredocs took eight seconds and allocated 22 GB. The rest
  must still read, as before: the command is read whole once, and each cut's own text from its
  start to the line after its body, unless that whole reading read the heredoc to the same line.
  Where a piece read alone fails but the whole rest would read, the cutting stops there and the
  rest stays code.
- The heredoc and here-string searches start only at an opener a later line closes. One that
  nothing closes, such as `<<E` in quoted text, was searched to the end of the command, once per
  opener: 16,000 of them took 1.6 seconds. The search finds what it found.
- The newline bypass check scans the joined layout once, instead of once per heredoc or
  here-string piece. The password-file deny pattern drops its redundant `echo.*>>` alternative:
  its final `>` already matches the retained alternative, but each `echo` restarted a scan of the
  remaining command. Both checks keep the same verdicts while removing quadratic CPU work.

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
- On Linux, the analysis time-ratio tests measure calling-thread CPU to exclude
  shared-host scheduling noise. Their input sizes, seven rounds, warm-up and ratio threshold stay
  the same, and the one-second assertion still measures wall time. Other hosts retain wall timing.
- Built-in lookaround patterns and configured nudge regexes can still take nonlinear time
  below the matching budget. They now refuse promptly on timeout; this hop does not change
  ConfigFactory definitions. More efficient dangerous-pattern matching can finish a negative
  match where the old implementation timed out and refused.

## Files Changed

`Services/ShellReader.cs`, `Services/BashCommandAnalyzer.cs`,
`DynaDocs.Tests/Services/ShellReaderTests.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Services/ThreadCpuTime.cs`, `DynaDocs.Tests/Integration/GuardSecurityTests.cs`, and this entry.
Fix hop 5 also changes `Services/CommandPosition.cs`, `Commands/GuardCommand.cs`,
`DynaDocs.Tests/Services/BashCommandAnalyzerScanTests.cs`, and
`DynaDocs.Tests/Commands/GuardCommandTests.cs`. The feature merge imports its own changes;
the implementation report lists those separately from this hop's authored files.
Fix hop 6 adds `DynaDocs.Tests/Services/BashCommandAnalyzerInputBoundTests.cs` and its test
association, and extends the existing guard integration tests.

## Related

- [dydo 3.6.0 - Long commands are verified within the guard's budget](./dyd-610-guard-regex-budget.md)
- [dydo 3.6.0 - The guard reads PowerShell's carriage returns, typographic quotes and carets](./dyd-641-shellreader-pwsh.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
