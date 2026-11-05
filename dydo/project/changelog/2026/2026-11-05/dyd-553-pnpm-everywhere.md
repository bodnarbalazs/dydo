---
area: project
type: changelog
date: 2026-11-05
---

# DYD-553 pnpm everywhere

## Summary

`DynaDocs.Tests/coverage` now locks and restores with pnpm, as the viewer does
([DYD-553](https://linear.app/bodnar-balazs/issue/DYD-553)). `pnpm-lock.yaml` replaces
`package-lock.json`. The lock comes from `pnpm import` with the viewer's `minimumReleaseAge: 21600`,
now in the folder's own `pnpm-workspace.yaml`. It resolves the same 277 `name@version` packages with
the same sha512 integrity, and `package.json`'s eight exact direct dependencies are unchanged.

- **Restore.** Every coverage restore is `pnpm -C DynaDocs.Tests/coverage install --frozen-lockfile`:
  both CI jobs that need it (`python-coverage` and `assurance-cancellation`, on their Linux and
  Windows legs, with pnpm 11 set up before Node.js caches its store on the coverage lock) and the
  Release `validation` job. The coverage-tools provisioning steps say the same.
- **Versions gate.** pnpm's isolated layout stays. `gate_versions.javascript_versions` reads and hashes
  the pnpm lock with a small reader for the lock's YAML subset. Each declared dependency must equal
  its importer's specifier and resolved version and launch through its top-level link. Each real
  package folder under `node_modules/.pnpm/*/node_modules` must be a locked `name@version`; a symlink
  or junction there is a link, not a package. A locked package missing from the store must be
  optional in every snapshot.
- **Guard nudge.** The two warn entries that restore LC's Windows-era npm nudge, scoped to plain
  `npm install`, `npm i` and `npx`, are held test-side in `PnpmNudgeExpectation`. Agents never write
  `dydo.json` (DR 045 §10), so the user pastes `PnpmNudgeExpectation.DydoJsonEntries` into it under
  DYD-643. They are not shipped defaults, and no guard code changed.

## Proof

- `test_gate_versions.py` drives the pnpm layout, links included (junctions on Windows), and checks
  the repository's own lock against `package.json`.
- `ReleaseWorkflowTests` pins the pnpm restore and its step order in both CI jobs and in Release
  validation, and no workflow restores with npm.
- `PnpmNudgeTests` loads a `dydo.json` holding the paste block verbatim and runs it in the guard's own
  order (block nudges first, then the DYD-499 warn pass-through). It shows that:
  - the first `npm install`, `npm i` or `npx` warns and names the pnpm command;
  - the identical retry passes;
  - `npx … dydo` still blocks on every try;
  - restores, other npm commands, and quoted, commented or heredoc text stay silent;
  - beside the shipped install nudge, a plain `npm install <pkg>` warns once with both messages and
    the retry spends both markers.

Fresh frozen installs from an empty `node_modules` pass under pnpm 11.25.0 and 12.16.1. Both install
the same 228 packages, and the 49 they skip are all optional.

## Files Changed

`DynaDocs.Tests/coverage/{pnpm-lock.yaml, pnpm-workspace.yaml, gate_versions.py,
tests/test_gate_versions.py, .gitignore}`, `package-lock.json` removed;
`.github/workflows/{ci.yml, release.yml}`; `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`;
`DynaDocs.Tests/Services/{PnpmNudgeExpectation.cs, PnpmNudgeTests.cs}`;
`dydo/reference/coverage-tools.md`; `dydo/guides/testing-strategy.md`; and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
