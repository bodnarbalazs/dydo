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
  `\>|` or `` `>| `` still pipes.
- fd duplication and close (`2>&1`, `1>&2`, `>&2`, `2>&-`, `3>&1-`) write no file.
- A redirection the guard reads only now (an fd number, `>|` or `>&`) into `/dev/null`,
  `/dev/stdout`, `/dev/stderr`, `/dev/fd/N` or PowerShell's `$null` writes no file. So
  `echo "$HOME" 2>/dev/null`, `ls $(pwd) 2>/dev/null` and `Get-ChildItem $HOME 2>$null` stay
  allowed with their bypass warning. `> /dev/null`, `>> /dev/null` and `&> /dev/null` beside a `$`
  word are refused as before.
- `N>> /dev/null` beside a `$` word, which the old reading refused by matching only its second `>`,
  is now allowed like `N> /dev/null`.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- Redirection characters inside quoted text are read as redirections, as before for `>` and `>>`,
  and now for the fd-numbered forms too.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, and this entry.

## Related

- [dydo 3.6.0 - Single-quoted literal data is no bypass](./dyd-422-guard-single-quoted-literals.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
