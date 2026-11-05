---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - PR-ready check allows lines without gh (unreleased)

## Summary

Inside a Git tree, the PR-ready check no longer refuses a command that holds no gh word just because
the check could not finish reading it
([DYD-515](https://linear.app/bodnar-balazs/issue/DYD-515)). A bare `case` word in a `$(...)`, or an
apostrophe PowerShell reads as an open quote, made an ordinary `arch="$(case "$(uname -m)" in ...)"`,
`echo "$(echo case)"` or `v=$(date); echo it\'s` read as a PR call through "unreadable shell
syntax". The refusal fired in clean and dirty trees alike, on first call and on retry.

## Changed

- A line the check cannot finish reading is still refused unread when it holds a partial PR call. When
  it holds a nested part (a `$(...)`, a group, an expanding body), it is refused unread only when its
  raw text holds the word `gh`, `pwsh` or `powershell`, under any path, case or `.exe`
  ([Guard System](../../../../understand/guard-system.md)). This is the same raw-shape gate the
  password-store, Python-stdin and mode-set rules use. Wrapper strings, argv arrays and substitutions
  are covered, since gh still shows in the text. An encoded call (`pwsh -EncodedCommand`, `-enc`,
  `-ec`) shows no gh, so the word of the program that decodes it counts too.
- A real gh PR call after such a failure is still refused unread, on the same paths, and so is an
  encoded PowerShell command after it.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Services/PrCallScanner.cs`, `DynaDocs.Tests/Services/PrCallScannerTests.cs`,
`DynaDocs.Tests/Integration/GuardPrReadyTests.cs`, `dydo/understand/guard-system.md`, and this entry.

## Related

- [dydo 3.6.0 - Merge report waits for GitHub](./dyd-535-merge-report.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
