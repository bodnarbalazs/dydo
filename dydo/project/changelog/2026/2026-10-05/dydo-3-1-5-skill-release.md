---
area: general
type: changelog
date: 2026-10-05
---

# dydo 3.1.5 - Skill release preparation

## Summary

Prepare the 3.1.5 patch package carrying the skill updates merged in PR #142. The public v3.1.4
tag points to that merge, but source versions and publication guards still named 3.1.3. The admiral
pushed the tag before preparing those fields; the tag built without publishing a new package.
The existing v3.1.4 tag remains unchanged.

## Changed

- The merged skill updates include Matt Pocock's v1.3.1 `pr` skill and its OpenAI metadata
  byte-for-byte, the rename of `self-improvement` to `retro` with its existing local behavior,
  and explicit glossary reads in `code-writer` and the parallel design resource.
- Align the runtime and npm package versions to 3.1.5 and point NuGet release notes to this entry.
- Permit publication only on a push of the exact v3.1.5 tag. Validation dependencies, permissions,
  OIDC, npm provenance, publishing environments and dispatch dry-run behavior remain unchanged.

## Files Changed

Release preparation changes `DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`npm/test/install.test.cjs`, this entry and `dydo/project/changelog/_changelog.md`. The skill changes
were merged separately; this preparation does not edit them.

## Release Status

This entry records preparation, not publication. After human merge, verify the merged commit and
absence of v3.1.5, push the annotated tag, and confirm the GitHub release and NuGet/npm 3.1.5
packages before reporting the release available. Updating the package does not automatically
upgrade skill files already installed in downstream projects.

## Related

- [dydo 3.1.3 - Map change memory](../2026-10-04/dydo-3-1-3-map-change-memory.md)
