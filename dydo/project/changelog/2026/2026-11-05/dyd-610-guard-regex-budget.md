---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Long commands are verified within the guard's budget (unreleased)

## Summary

A long benign command is no longer refused as "Command too complex to verify safely" because a
command-shaped dangerous rule ran past its 250 ms budget
([DYD-610](https://linear.app/bodnar-balazs/issue/DYD-610)). The 6,039-character GS-02 `cat >>`
evidence write was refused this way under coverage instrumentation, and long literal data could be
refused without it: a run of `{`, `!` or `\` of a few thousand characters, or a repeated command word
such as `rm -f local` or `gh repo edit x` in a heredoc body.

## Changed

- The 17 command-shaped dangerous rules (those that start with the command-name anchor: `dd`,
  the David Ondrej denylist's `rm`, disk, `curl | sh`, `git push`, git recovery, `chmod`/`chown`,
  `gh`, password-manager and keychain rules, and both raw worktree removal rules) match in time
  linear in the command ([Guard System](../../../../understand/guard-system.md#layer-2-bash-command-analysis)).
  Each is a NonBacktracking regex, as the `sudo rm` rule already was.
- What each rule refuses is unchanged. A test keeps every rule's former pattern as an oracle and
  finds no command on which the two answer differently, over the suite's commands and generated
  ones.
- Every rule keeps its 250 ms timeout, a timeout still refuses the command as too complex to
  verify, and the rules keep their order and refusal messages.
- A guard process now builds these matchers when it first analyses a command, which adds about
  100 ms to that first analysis.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- The rules outside the command-name anchor are unchanged. The anywhere-in-the-text `dd` rule for
  `sd*`/`nvme*`/`vd*`/`mmcblk*` targets still slows on a long line repeating `dd ... of=`, and the
  password-store rule reads a few million characters before its budget runs out.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `DynaDocs.Tests/Services/DangerousPatternBudgetTests.cs`,
`DynaDocs.Tests/Services/DangerousPatternOracleTests.cs`,
`DynaDocs.Tests/coverage/test-associations.json`, and this entry.

## Related

- [dydo 3.6.0 - Single-quoted literal data is no bypass](dyd-422-guard-single-quoted-literals.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
