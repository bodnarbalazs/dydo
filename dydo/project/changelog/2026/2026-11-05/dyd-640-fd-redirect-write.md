---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - An fd-numbered redirection writes its target (unreleased)

## Summary

The guard now reads every output redirection as a write to its target, with or without an fd
number ([DYD-640](https://linear.app/bodnar-balazs/issue/DYD-640)). Before, `printf x 1> dydo/index.md`
recorded no write, so the protected tier never saw it and the call ran with a warning. It is now
refused like `printf x > dydo/index.md`, through Claude's Bash and PowerShell tools alike.

## Changed

- `N>`, `N>|`, `>|`, `>&word` (word not digits or `-`) and `word2>file`, which bash reads as the
  word and a plain `>file`, write their target, checked against the off-limits and protected tiers
  ([Guard System](../../../../understand/guard-system.md#layer-2-bash-command-analysis), file
  operation extraction). `>`, `>>`, `N>>`, `&>` and `&>>` already did.
- `>|` is the clobber redirection, not a pipe: `printf x >| notes.md` stays one command. An escaped
  `\>|` or `` `>| `` still pipes; `\\>|`, whose backslash is itself escaped, is a clobber.
- fd duplication and close (`2>&1`, `1>&2`, `>&2`, `2>&-`, `3>&1-`) write no file. `>&$fd` and
  `>&"$fd"` write the file the variable names, as bash does when its value is no number, so
  `fd=dydo/index.md; printf x >&"$fd"` is refused.
- A redirection the guard reads only now (an fd number, `>|` or `>&`) into `/dev/null`,
  `/dev/stdout`, `/dev/stderr`, `/dev/tty` or `/dev/fd/N` writes no file, nor, in PowerShell only,
  into its unquoted `$null`. For these two tests the guard reads the target where the shell ends
  it: quotes removed, and ended at the first unquoted `)` or `` ` ``, which closes the substitution
  or subshell around the redirection, wherever it falls, so text after it is not part of the target.
  A quoted `)` is part of it: `>&"1)"` writes the file `1)`. `}` ends the target in PowerShell,
  where it closes a script block; bash ends no word at `}`, so `2>/dev/null}` writes the file
  `/dev/null}`. A backslash-newline continuation after the operator is read through to the target
  on the next line. So `echo "$HOME" 2>/dev/null`, `ls $(pwd) 2>/dev/null`,
  `out=$(git status 2>&1)`, `` x=`ls 2>&1` ``, `(echo $HOME >&2)`,
  `tag="$(git describe --always 2>/dev/null)-dirty"`, `echo "$(git status 2>&1):done"`,
  `if [ -n "$(git status --porcelain 2>/dev/null)" ]; then ...`, a continued `2>\` then
  `/dev/null`, and PowerShell's `Get-ChildItem $HOME 2>$null` and
  `Invoke-Command { Get-ChildItem $HOME 2>$null}` stay allowed with their bypass warning, while
  `x=$(printf y 2>dydo/index.md)`, `x=$(git status 2>dydo/index.md)suffix` and a continued `2>\`
  then `dydo/index.md` are refused. `> /dev/null`, `>> /dev/null` and `&> /dev/null` beside a `$`
  word are refused as before.
- Escaped quote and closing characters remain filename text during sink classification. Escape
  pairs keep their spelling, so decoding a filename grants no additional sink exemption.
- `$null` outside PowerShell is a shell variable naming a file, and a quoted `'$null'` or `"$null"`
  is no PowerShell variable, so each stays a write: `null=dydo/index.md; printf x 1>>$null` is
  refused, and so is bash's `echo $HOME 2>$null`, a write through a variable beside a `$` word.
- `N>> /dev/null` beside a `$` word, which the old reading refused by matching only its second `>`,
  is now allowed like `N> /dev/null`.
- The refusal names the redirection as written: the whole fd (`Write via 10>`), and a plain `>` for
  `word2>file`.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- Redirection characters inside quoted text are read as redirections, as before for `>` and `>>`,
  and now for the fd-numbered forms too.
- `>&$fd` and `>&"$fd"` beside a `$` word, which base did not read, are now refused even when the
  variable holds a descriptor number: the guard cannot tell the value, and acceptance 2 reads
  `>&word` whose word is no number as a write. So are bash's `echo $HOME 2>/dev/null}`, a write to
  the file `/dev/null}`, and `>&"1)"`, a write to the file `1)`.
- A write target closed by a `( )` subshell's `)`, such as `(printf y >dydo/index.md)`, keeps the
  `)` in its path, for `>` as before and for the newly read forms alike, so the path names no
  guarded file. A target inside `$(...)` or backticks is read again from the inner
  command and checked.
- A plain `>` or `>>` followed by a backslash-newline continuation still reads the backslash as its
  target, as before; only the newly read forms read through to the next line.
- The integrated feature's uncertain-command rule still refuses a substitution used as an
  executable, with or without a descriptor redirect. A known outer command with nested
  substitutions and a sink remains allowed.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, and this entry.

## Related

- [dydo 3.6.0 - Single-quoted literal data is no bypass](./dyd-422-guard-single-quoted-literals.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
