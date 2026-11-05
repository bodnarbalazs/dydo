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
  of `sed` (one `s` command, no `e` or `w` flag, only the options on its closed list), `printf`, `echo`
  or `grep`, whose output goes only to a file, the terminal or a pipe into `grep` or `echo`.
- Every command in the command, in each reading, must be state-free: `sed`, `printf`, `echo`,
  `grep`, `head`, or `git diff|add|status|commit|log`, named exactly. An alias, function or builtin
  change (`alias`, `enable`, `Set-Alias`, `New-Alias`, `sal`, `nal`, `Set-Item alias:`), an
  assignment, an unknown program or `git -c` anywhere takes the relief away. So do `printf -v` in any
  spelling and `%n`, which assign variables (bash evaluates a `-v` name's subscript), and
  PowerShell's `echo` with a parameter word such as `-OutVariable`. A command with a heredoc or
  here-string body gets no single-quote relief.
- Each state-free command takes only the options on its closed list, in every spelling (clusters,
  attached values, `--opt=value`, options after operands); an abbreviation or any other option
  takes the relief away. `sed`: `-n -E -r -s -z -u` and a bare `-i` or `--in-place`, `--posix`,
  `--sandbox` and the like, but no backup suffix (`-i.bak`, `-i'.git/*'`, `--in-place=SUFFIX`),
  `-e`, `-f`, `-l` or `--follow-symlinks`. `printf`: none. bash `echo`: `-n -e -E`. `grep`: matching
  and formatting flags, `-f` (patterns) included, no `--pre`. `head`: counts. `git`: read-only
  formatting options such as `--stat`, `--short`, `--format`, `-1`, `-qam`, but never `--ext-diff`,
  `--textconv`, `-p`, `-e`, `--amend`, `--no-verify`, `--output`, `--git-dir`, `--work-tree`,
  `--exec-path`, `--template`, `--config-env`, `-c` or `-C`.
- No command on the line may read a file the line writes as code. Every `sed` on it must only
  substitute (one `s` script, listed options only). A line that runs `git` may write no path git
  reads as configuration, attributes or hooks: a `.git` component, `.gitattributes`, `.gitmodules`,
  `.gitconfig`, `.mailmap` or `.config/git/`, through any redirection or `sed -i` operand, the whole
  write set of the listed options. These checks are case-blind, read `\` both ways, fold `.` and `..`, and count a target the
  shell may expand. So `sed -f run.sed` after writing `run.sed`, and `git diff` after writing
  `.git/config`, are refused as on master.
- The line must read for certain: no substitution, group, compound command, continuation, lone `&`,
  `$'...'`, or, where PowerShell may run it, a caret, `--%` or typographic quote.
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

- The relief does not follow a file between calls: a git hook written on a line that runs no git
  passes, and a later `git commit` runs it, as with a quoted-heredoc hook. On one line, both the hook
  with `git commit` and `printf ... > run.sh && sh run.sh` are refused.
- Pre-existing state, the same class as a pre-existing hook, is not read: a profile, alias or git
  configuration set before the call, an `include` or `core.hooksPath` that names another file, or a
  symlink that already leads to a git path. Only the call's own text is.
- An fd-numbered redirection (`1> .git/config`) takes the relief away, but master's path extraction
  records no write for it, so the call passes with a warning, as on master.
- A `sed` script's `r`, `R`, `w` or `W` command naming a file is not a path the guard extracts, with
  or without the relief; the relief takes only a single `s` command.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `Services/ShellReader.cs`, `DynaDocs.Tests/GuardRetroCommands.cs`,
`DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, `dydo/understand/guard-system.md`, and this entry.

## Related

- [dydo 3.6.0 - Guard blocks raw merges](../2026-11-04/dyd-425-guard-merge-blocks.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
