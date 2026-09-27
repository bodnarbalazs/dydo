---
area: general
type: changelog
date: 2026-09-27
---

# dydo 3.1.1 — Map polish

dydo 3.1.1 is a refinement release for the `dydo map` viewer introduced in 3.1.0: a faster way to
find a Project, a dark theme, and a Refresh that updates the map without losing your place.

## Added

- The toolbar's native Project `<select>` is now a searchable combobox. Open Projects group into
  In Progress, Planned, Paused and Backlog sections, each sorted by target date (earliest first,
  undated last). A collapsed Closed (N) section lists completed Projects newest-first; searching
  also reaches Canceled Projects, which otherwise never appear in the browsed list.
- A System/Light/Dark toggle in the toolbar overrides the OS's `prefers-color-scheme` and persists
  the choice in the browser's local storage. The dark palette follows the viewer's existing
  Linear-styled look, and an In Progress issue's card gets a soft glow in its status colour when
  dark mode is active.
- The canvas now shows a graph-paper background, a 24px minor grid and a 120px major grid, styled
  for both the light and dark palettes.
- A favicon of three linked nodes in the viewer's accent colour, with its own dark-mode variant.
- A toolbar **Refresh** button (and its `R` keyboard shortcut, which fires when focus isn't in a
  text field or dropdown and no modifier key is held) re-fetches the current Project in place,
  without a page reload. It keeps the viewport, selection and theme as they were, fades out removed
  issues, then animates changed and added issues one by one in reading order, and reports what
  changed with a notice such as "3 changed · 1 new · 1 removed" or "No changes".
- Two new engineering skills, adapted from cursor/plugins pstack: `create-verification-skill`
  (user-invoked, both host locks) generates a project-local `verify-<app>` skill with a
  deterministic control CLI, Launch/Doctor/Drive/Evidence/Cleanup sections and a nested feature
  map, preferring a dev-only quick login over typed credentials; the generated skill itself stays
  model-invoked. `maintain-verification-skill` (model-invoked) keeps that verify skill and map true
  to the running app, ending `clean`, `changed` or `blocked`, and reports product regressions as
  Bugs. Both are registered in the skill READMEs, glossary, control-flow tables and third-party
  notices, taking the canonical skill count from 29 to 31.

## Changed

- `dydo map`'s CLI surface, Linear API access and read-only behaviour are unchanged from 3.1.0;
  everything above is viewer-side polish.
- The `issue-captain` and `admiral` skills now choose the strongest supported model for a real
  engineering challenge and the smallest adequate one otherwise.
- GitHub now cleans up merged branches: `bodnarbalazs/dydo` deletes a PR's head branch on merge,
  and a `protect-default-branch` ruleset blocks deleting or force-pushing `master`, with no bypass
  actors. The push-delete guard's reason now reflects this, telling agents to clean up locally
  instead with `git worktree remove`, `git branch -d` and `git fetch --prune`, and to name any
  other remote branch for the human. The `issue-captain` and `chief-of-staff` skills' cleanup steps
  changed to match, and the working-tree contract's Cleanup section now says "delete" means the
  local branch.

## Upgrade

Install or update dydo to 3.1.1. No project files change, and no new configuration is required to
use the picker, dark mode or Refresh.

## Related

- [dydo 3.1.0 — Linear project map](../2026-09-25/dydo-3-1-0-linear-project-map.md) — the release
  this refines.
