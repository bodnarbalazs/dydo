---
area: general
type: changelog
date: 2026-10-17
---

# dydo 3.4.0 - New-file nudges and --root

## Summary

Minor release adding new-file nudges to the guard and a `--root` option to the project-inspection commands.

## Changed

- `"match": "new-file"` nudges in `dydo.json` match the project-relative path a Write, Edit,
  MultiEdit, NotebookEdit or Codex `apply_patch` is about to create, and fire only when nothing
  exists there. `dydo validate` rejects an unknown `match`. Files created through a shell are not
  seen (LC-759, PR #149).
- `dydo check`, `dydo validate` and `dydo graph` take `--root <dir|file>` to work on another
  project and name the root they used (LC-1002).
- The admiral and issue-captain nudges steer officers toward cross-vendor dispatch and reviewers
  (PR #150).
- Align runtime and npm versions to 3.4.0 and permit publication only on the exact v3.4.0 tag.

## Files Changed

`Commands/`, `dydo/reference/dydo-commands.md`, `DynaDocs.csproj`, `npm/package.json`,
`.github/workflows/release.yml`, `DynaDocs.Tests/Commands/`, `DynaDocs.Tests/Integration/`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `npm/test/install.test.cjs`, this entry and its changelog hub.

## Related

- [dydo 3.3.0 - Working specs and communication](../2026-10-14/dydo-3-3-0-working-specs-and-communication-skills.md)
