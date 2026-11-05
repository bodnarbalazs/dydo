---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Single-quoted literal data is no bypass (unreleased)

## Summary

The guard no longer refuses an ordinary single-quoted data write as a bypass
([DYD-422](https://linear.app/bodnar-balazs/issue/DYD-422), split from DYD-406 after the DYD-421
tribunal). The GS-02 `sed -i` with backticks in its script, the same-session single-line
``sed -i 's/`x`/y/' f`` and `printf '%s' 'first line<newline>$HOME is literal' > notes.md` now write
their literal destinations, which are still path-checked.

## Changed

- Single-quoted text is not searched for a substitution, a variable or a line break only where no
  shell or program on the line can run it
  ([Guard System](../../../../understand/guard-system.md#layer-2-bash-command-analysis), bypass
  detection). Bash's reading of the line, and PowerShell's too wherever PowerShell may run it (a
  PowerShell tool call or an unknown shell), must close it as one single-quoted string in the same
  word. That word must be an argument
  of `sed` (one `s` command, no `e` or `w` flag, no option holding an `e` or an `f`), `printf`, `echo`
  or `grep`, whose output goes only to a file, the terminal or a data command. The line must read for
  certain as simple commands: no substitution, group, compound command, continuation, background
  job, `$'...'`, or, where PowerShell may run it, a caret or typographic quote.
- Everything else is searched as before and refused as on master: `echo don\'t $(rm dydo/index.md)
  won\'t` for every caller, a PowerShell tool call included, since bash's reading is always
  required; the same line in a `cat <<EOF` body, `eval 'echo x' '> $OUT'`,
  apostrophes in comments and expanding bodies, command strings and runners (`bash -c`, `eval`,
  `iex`, `cmd /c`, `env -S`, `pwsh -Command`, `ssh`, `watch`, `su -c`, `script -c`, `xargs`,
  `find -exec`, `docker`/`kubectl exec`, `tmux`), data piped or fed to a shell or another program,
  `sed` with `e` or `w`, `awk`, and a variable or substituted destination.
- The relief changes the bypass search only. The file operations a substitution in literal data
  would make are still extracted and checked, and protected and off-limits paths are refused as
  before.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- The relief does not follow a file it writes: `printf '%s' '$(...)' > run.sh && sh run.sh` passes,
  as a quoted-heredoc script then run does.
- A `sed` script's `r`, `R`, `w` or `W` command naming a file is not a path the guard extracts, with
  or without the relief; the relief takes only a single `s` command.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `Services/ShellReader.cs`,
`DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, `dydo/understand/guard-system.md`, and this entry.

## Related

- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
