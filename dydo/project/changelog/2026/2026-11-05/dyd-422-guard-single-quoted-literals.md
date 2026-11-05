---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Single-quoted literal data is no bypass (unreleased)

## Summary

The guard no longer refuses a single `sed`, `printf` or `echo` command that writes single-quoted
data as a bypass ([DYD-422](https://linear.app/bodnar-balazs/issue/DYD-422), split from DYD-406
after the DYD-421 tribunal). ``sed -i 's/`x`/y/' f`` and
`printf '%s' 'first line<newline>$HOME is literal' > notes.md` now write their literal
destinations, which are still path-checked. The GS-02 `sed -i` writes that chain `git` stay refused
as on master; relief for git-chained lines is deferred to a follow-up.

## Changed

- Single-quoted text is not searched for a substitution, a variable or a line break only when the
  whole line is one `sed`, `printf` or `echo` command
  ([Guard System](../../../../understand/guard-system.md#layer-2-bash-command-analysis), bypass
  detection). The line has no `;`, `&&`, `||`, `|`, `|&`, `&`, group, subshell, substitution,
  process substitution, heredoc, here-string, continuation, compound command or assignment word.
- Bash's reading and PowerShell's reading must both find that one command and hold the quote in the
  same word, whatever shell the call declares; a Codex `pwsh -Command` envelope from a caller read
  as bash runs its text in PowerShell.
- `sed`: one `s` script with no `e` or `w` flag, and only its closed option list (`-n -E -r -s -z -u`,
  a bare `-i` or `--in-place`, `--posix`, `--sandbox` and the like; no backup suffix, `-e`, `-f` or
  `-l`). `printf`: no option and no `%n`. `echo`: no word PowerShell reads as a parameter, so bash's
  `-n`, `-e` and `-E` take the relief away too.
- No text outside single quotes may be read otherwise by either shell: bash's `$'...'`, a lone `&`,
  a caret, `--%`, a typographic quote, or a word the shell may expand (`*`, `?`, `[`, `{`, `~`,
  PowerShell's `@`). Bash turns `?f` into an existing file named `-f`.
- No break character outside single quotes either: a carriage return (pwsh ends a statement at a
  bare one, so `echo 'x'<CR>iex '...'` runs `iex`), any other control character but tab and line
  feed, NEL, or a Unicode line or paragraph separator. Inside single quotes both shells keep them
  literal, so the multi-line `printf` still writes its destination.
- Everything else is searched as before and refused as on master: a second command of any kind
  (`git`, `grep`, `head`, a pipe), `echo don\'t $(rm dydo/index.md) won\'t` for every caller, the
  same line in a `cat <<EOF` body, `eval 'echo x' '> $OUT'`, apostrophes in comments and expanding
  bodies, command strings and runners (`bash -c`, `eval`, `iex`, `cmd /c`, `env -S`,
  `pwsh -Command`, `ssh`, `watch`, `su -c`, `script -c`, `xargs`, `find -exec`,
  `docker`/`kubectl exec`, `tmux`), `sed` with `e` or `w`, `awk`, and a variable or substituted
  destination.
- The relief changes the bypass search only. The file operations a substitution in literal data
  would make are still extracted and checked, and protected and off-limits paths are refused as
  before.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- Relief for git-chained lines, such as the GS-02 `sed -i ... && git diff --stat && git commit`, is
  deferred to a follow-up; they are refused as on master.
- The relief does not follow a file into a later call: `printf '%s' '$(...)' > .git/hooks/pre-commit`
  passes, and a later `git commit` runs it, as with a quoted-heredoc hook.
- Pre-existing state is not read: a file an earlier call left, such as one named `-f`, a profile,
  an alias or git configuration set before the call.
- A `sed` script's `r`, `R`, `w` or `W` command naming a file is not a path the guard extracts, with
  or without the relief; the relief takes only a single `s` command.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `Services/ShellReader.cs`, `DynaDocs.Tests/GuardRetroCommands.cs`,
`DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, `dydo/understand/guard-system.md`, and this entry.

## Related

- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
