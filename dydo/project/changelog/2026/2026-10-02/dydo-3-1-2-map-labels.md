---
area: general
type: changelog
date: 2026-10-02
---

# dydo 3.1.2 - Map labels

dydo 3.1.2 is a refinement release for the `dydo map` viewer: each card now shows its issue's Linear
labels, the identifier doubles as the Linear link, and the pickable slot reads more plainly. It also
narrows the dangerous-bash-command guard's `pass`-CLI check and expands the missing-API-key help
text, and ships agent-guidance
changes: the coding standards (and their Scaffold copy installed by `dydo init`) now say code that
only tests call is dead and to delete it with its tests once a review or gate flags it, code
review's rubric carries the same rule for test-only and commented-out code, the inquisitor's
existing dead-code lens gained a proof form for unused members, exports and fields, and skill
citation rules now allow citing only Scaffold-carried content.

## Added

- Each card now shows up to three of its issue's Linear labels as chips in the top-left: a neutral
  rounded pill with a small dot in the label's own Linear colour and the name in the card's
  secondary text colour, styled for both the light and dark palettes. Chips sort alphabetically
  (case-insensitive); a chip that doesn't fit whole on the row is hidden rather than clipped, and
  only a lone first chip too wide for the row truncates with an ellipsis. There's no `+N` counter -
  the chip row's tooltip lists every label on the issue.
- The identifier now doubles as the Linear link: the top-right of the card shows `DYD-69 ↗` in
  monospace, styled like the former `Linear ↗` button and sharing its `aria-label`. It opens Linear
  in a new tab, and clicking it doesn't select the card. This replaces the separate identifier text
  and the old `Linear ↗` button.
- The bottom-right slot, previously the assignee or an `unassigned` label, now shows a `Pickable`
  badge instead of `unassigned` when the issue is pickable - an issue is pickable only when
  unassigned, so this is a straight swap. Assigned issues still show the assignee there, and the
  pickable ring/glow is unchanged.
- The coding standards (and their Scaffold copy installed by `dydo init`) now say code that only
  tests call is dead and, once a review or gate flags it, to delete it with its tests. Code
  review's `code.md` rubric carries the same rule, saying to delete it with its tests even when a
  gate asked for more tests or a refactor. The inquisitor's existing dead-code lens gained a proof
  form: an unused member, export or field is now proved dead by a reference search that counts
  test-only callers as no callers.

## Changed

- `dydo map`'s read-only behaviour is unchanged - it still only reads Linear, never writes. The
  Linear read itself did change: `LinearReader`'s issue fragment now also fetches each issue's
  labels, so `MapIssue` carries a `Labels` list and `/api/graph` issues expose a
  `labels: { name: string; color: string }[]` array (always present, empty when none). The
  missing-API-key help text also changed - see the bullet below.
- `dydo map`'s help text for a missing `LINEAR_API_KEY` (and the matching
  `dydo/reference/dydo-commands.md` docs, plus their Scaffold twin) now also explain how to persist
  the key across restarts, not just set it for the current session: on Windows,
  `[Environment]::SetEnvironmentVariable('LINEAR_API_KEY', $env:LINEAR_API_KEY, 'User')` after the
  session-only `$env:LINEAR_API_KEY = '<your key>'`; on bash/zsh, adding an
  `export LINEAR_API_KEY='<your key>'` line to `~/.bashrc` (making sure your login shell sources it)
  or to `~/.zshrc`/`$ZDOTDIR/.zshrc`.
- The dangerous-bash-command guard's `pass`-CLI check (`BashCommandAnalyzer`'s `OndrejPassRegex`) no
  longer flags prose with two or more words after "pass" - a commit message like "pass the lease to
  the captain" - as the `pass` password-manager CLI. A line reading `pass <one word>` is still
  blocked: it's shaped exactly like the CLI's name-call form (`pass github` prints a secret). The
  check still matches a known subcommand (`show`, `insert`, `add`, `edit`, `generate`, `rm`,
  `remove`, `delete`, `mv`, `rename`, `cp`, `copy`, `git`, `init`, `ls`, `list`, `find`, `search`,
  `grep`, `otp`), a flag, or exactly one secret-name token (bare, quoted (spaces allowed), or a
  `$(...)` substitution) after `pass`. The gaps this leaves on purpose are named in
  `OndrejPassRegex`'s comment in `Services/BashCommandAnalyzer.cs`, not enumerated here.
- Refresh now treats a label change (by name and colour, set-wise) as a changed field like any
  other, so a label edit in Linear is picked up and counted in the Refresh change notice, same as
  other field changes.
- Label colours come from Linear's own API - there's no hard-coded palette.
- Skill citation rules were tightened to cite only Scaffold-carried content, and the 3.x adoption
  migration guide no longer points adopters at the retired `dydo template update`/`dydo sync`
  commands - it now gives three manual steps: install the skill tree, reconcile the scaffold
  documents by hand, and run `dydo check`.

## Upgrade

Install or update dydo to 3.1.2. No project files change, and no new configuration is required to
see the label chips, the identifier link or the Pickable badge.

## Related

- [dydo 3.1.1 - Map polish](../2026-09-27/dydo-3-1-1-map-polish.md) - the release this follows.
