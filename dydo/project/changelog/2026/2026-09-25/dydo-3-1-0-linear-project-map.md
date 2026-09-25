---
area: general
type: changelog
date: 2026-09-25
---

# dydo 3.1.0 — Linear project map

dydo 3.1 adds `dydo map`, a read-only browser map of one Linear Project. The viewer is new in this
release; refinements follow in 3.1.x.

## Added

- `dydo map [--no-browser]` serves a local viewer on `http://localhost:<free port>/`, prints the URL
  and opens it unless `--no-browser` is given. It reads a personal Linear API key from
  `LINEAR_API_KEY`; without one it prints how to create a key and exits `2` before any network call.
  The server answers GET only and the key never reaches the browser.
- The viewer is a React Flow graph laid out with ELK: choose a team, then a Project. Parent issues
  are plates holding their sub-issues, with Collapse all and Expand all. Each issue shows its
  identifier, title and Linear status; pickable work (`Todo`, unassigned, no open blocker) is
  highlighted. Blocking relations are directional edges, a blocker outside the Project is a dashed
  node, Related links stay hidden until revealed, and only an issue's dedicated button opens Linear.
- Every page load reads Linear afresh; nothing is cached, polled or written, so F5 shows Linear's
  current state.
- The viewer bundle is embedded in the dydo binary when `viewer/dist/` is built first. A dydo built
  without it serves a "viewer not built" page at `/`.

## Changed

- [DR 052](../../../decisions/052-dydo-map-read-only-linear-view.md) records `dydo map` as a
  read-only Linear view and narrows DR 044's boundary texts: Linear still owns live work, and dydo
  keeps no Linear cache, poller or mirror.
- The gate stack gains a fourth stack, `viewer`, in `gap_check.json`: test, static (TypeScript
  build, lint, source metrics, dependencies, unused exports, clones) and coverage rows. Its mutation
  row is unavailable, since no TypeScript mutation mechanism is adopted.
- CI typechecks, lints, tests, covers, builds and runs the Playwright end-to-end suite for the
  viewer, then runs a full-stack end-to-end suite through a published linux-x64 dydo binary. The
  release workflow builds the viewer once and embeds that bundle in the native binaries, the
  validation build and the NuGet package.

## Upgrade

Install or update dydo to 3.1.0. To use the map, set `LINEAR_API_KEY` to a personal Linear API key
and run `dydo map`. No project files change.

## Related

- [dydo Commands](../../../../reference/dydo-commands.md) — `dydo map` reference.
- [Visual Linear project map](../../../plans/visual-linear-project-map.md) — The reviewed plan.
