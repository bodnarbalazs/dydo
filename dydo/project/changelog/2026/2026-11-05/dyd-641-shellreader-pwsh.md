---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - The guard reads PowerShell's carriage returns, typographic quotes and carets (unreleased)

## Summary

The guard's PowerShell reading now matches pwsh 7.6.9 for three characters it misread
([DYD-641](https://linear.app/bodnar-balazs/issue/DYD-641), found during DYD-422). Where it cannot
match, the reading is uncertain and every rule treats it that way. A command pwsh runs after a bare
carriage return, a typographic quote or a caret is now read by the bypass checks, the literal-body
relief, the dangerous-pattern readings, the nudges, the dydo-command rules, the PR-ready check and
the merge-route blocks ([Guard System](../../../../understand/guard-system.md#layer-2-bash-command-analysis)).
DYD-422's local bans on these characters in the literal-data relief stay as defence in depth.

## Changed

- A bare carriage return is a line break in PowerShell's reading. It ends a statement and a `#`
  comment, it ends a here-string's opener line and starts its terminator line, and after a backtick
  between words it continues the line. So `echo 'x'<CR>gh pr merge 5` is refused as a raw merge.
  Inside quotes it stays literal.
- The embedded-newline check counts a bare carriage return as a line break in every dialect. It
  counts one inside a double-quoted span too, unless every reading holds the span in a word, so
  `echo a > notes.md # x"<CR>Remove-Item dydo/index.md<CR># y"` is refused.
- File-operation extraction reads a bare carriage return both ways in every dialect: it takes the
  commands pwsh ends at one, and the whole command as bash reads it, since bash keeps one in a word
  and runs the words after it as the same command's arguments. So `echo hello<CR>rm dydo/index.md`
  is refused for pwsh's `rm`, and `rm <CR> dydo/index.md`, `sed -i<CR> s/a/b/ dydo/index.md` and
  `bash -c 'rm <CR> dydo/index.md'` for bash's, which deletes or edits the file.
- `‘ ’ ‚ ‛` and `“ ” „` are PowerShell quotes. They open and close a string as `'` and `"` do, a
  doubled one is literal, and they work in here-string openers and terminators too. So
  `echo ‘a'; dydo mode set overnight on; ’b'` meets the mode-set warning.
- The here-string relief keeps a body as code when pwsh ends the here-string sooner, at a line break
  or bare carriage return followed by a quote of the opener's family and `@`.
- Around a heredoc or here-string body, the literal-body relief ends a line at a bare carriage
  return too. So a comment it ends hides no code before the opener, and no leading pipe after the
  terminator. `# x<CR>iex (` before a here-string, and `# c<CR>| iex` after one, keep the body as
  code and are refused.
- A caret is literal to pwsh and an escape to cmd. Where the two would read a line differently, the
  reading holds both commands and is uncertain. `echo a^;git push origin main` is refused as a push
  to the default branch, and a PR call on such a line is refused unread.
- Lines that hold none of the three characters read as before. A carriage return that a line feed
  follows is half of one CRLF line break, so a CRLF-ended here-string write such as
  `@'<CR><LF>hello<CR><LF>'@ | Set-Content notes.md<CR><LF>echo done` is allowed as before.
- Bash's reading is unchanged and keeps all three as word text. Because the embedded-newline check
  and file-operation extraction also take pwsh's reading of a bare carriage return whatever the
  shell, a bash call holding one can be refused for what pwsh would run.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- cmd's own handling of a bare carriage return is not modelled. There was no cmd host to measure it.
- File-operation extraction does not know typographic quotes, so in PowerShell
  `echo 'a’; Remove-Item notes.md; ’b'` deletes the file but extracts no delete (DYD-661).
- Inside a word, a backtick before a CRLF still reads as a line continuation, but pwsh ends the
  statement at the line feed (DYD-665).
- The command string `iex` runs is not searched for file operations (DYD-666).

## Files Changed

`Services/ShellReader.cs`, `Services/BashCommandAnalyzer.cs`,
`DynaDocs.Tests/Services/ShellReaderTests.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`DynaDocs.Tests/Services/CommandPositionTests.cs`, `DynaDocs.Tests/Services/DydoInvocationScannerTests.cs`,
`DynaDocs.Tests/Services/PrCallScannerTests.cs`, `DynaDocs.Tests/Services/MergeRouteScannerTests.cs`,
`DynaDocs.Tests/Integration/GuardSecurityTests.cs`, `DynaDocs.Tests/coverage/test-associations.json`,
`dydo/understand/guard-system.md`, and this entry.

## Related

- [dydo 3.6.0 - Single-quoted literal data is no bypass](./dyd-422-guard-single-quoted-literals.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
