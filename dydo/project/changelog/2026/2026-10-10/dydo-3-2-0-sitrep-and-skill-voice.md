---
area: general
type: changelog
date: 2026-10-10
---

# dydo 3.2.0 - Sitrep and skill voice

## Summary

Minor release adding the `sitrep` skill and aligning the voice of every skill. It also carries the
3.1.5 preparation, which was never tagged: v3.1.4 remains the last published tag before this one.

## Changed

- New user-invoked `sitrep` skill: a situation report for a user arriving cold, covering how the
  work got here, where each thread stands, what needs him, and what comes next.
- Skills say "the user" instead of "the human", and refer to the user as he/him.
- `issue-captain` writes the PR body with the `pr` skill.
- `admiral` gains an Altitude boundary: work reaches Issues through captains; asked for hands-on
  work, the admiral declines and offers to be demoted to captain.
- Every em dash in the repository is replaced with a hyphen, except in the protected
  `dydo/index.md`. The `IMPLEMENTED -` form line in the code-writer skill and the Linear workspace
  standard changes with it.
- Align the runtime and npm package versions to 3.2.0, point NuGet release notes to this entry, and
  permit publication only on a push of the exact v3.2.0 tag.

## Files Changed

`skills/` (new `skills/productivity/sitrep/`, wording across the tree, skill and category READMEs),
`dydo/reference/` and `Scaffold/dydo/reference/` glossary and Linear workspace standard,
`DynaDocs.Tests/Steps/CanonicalSkillAssertionTests.cs`, the release preparation files
(`DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`npm/test/install.test.cjs`), this entry and `dydo/project/changelog/_changelog.md`. The em dash
sweep touches text files across the repository, including history, C# messages and comments.

## Related

- [dydo 3.1.5 - Skill release preparation](../2026-10-05/dydo-3-1-5-skill-release.md)
