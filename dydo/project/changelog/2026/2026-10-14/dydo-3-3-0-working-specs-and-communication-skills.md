---
area: general
type: changelog
date: 2026-10-14
---

# dydo 3.3.0 - Working specs and communication

## Summary

Minor release adding temporary Project-scoped working specs and user-invoked communication skills.

## Changed

- Working specs hold current shared feature intent under `dydo/reference/working-specs/`;
  preserve durable knowledge and retire the Project subtree when its work is complete.
- Replace `sitrep` with pstack's `ketchup`, retaining Dydo's `bro` voice guidance; add
  user-invoked `in-your-own-words` for restating the user's goals.
- PR bodies include the independent reviewer PASS verbatim. Skill distribution verifies both
  host projections after setup.
- Verification-skill creation requires usable empty-data launch, reports missing prerequisites,
  and discloses the proof limits of stand-in environments.
- Align runtime and npm versions to 3.3.0 and permit publication only on the exact v3.3.0 tag.

## Files Changed

`skills/`, `dydo/reference/`, `Scaffold/dydo/reference/`, `dydo/understand/`,
`Scaffold/dydo/understand/architecture.md`, `dydo/guides/`, `dydo/project/decisions/`, `THIRD-PARTY-NOTICES.md`, `npm/THIRD-PARTY-NOTICES.md`,
`DynaDocs.csproj`, `npm/package.json`, `.github/workflows/release.yml`,
`DynaDocs.Tests/Steps/CanonicalSkillAssertionTests.cs`,
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
`DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`, `npm/test/install.test.cjs`, this entry and its changelog hub.

## Related

- [Feature Spec Convention](../../../../reference/feature-spec-convention.md)
- [dydo 3.2.0 - Sitrep and skill voice](../2026-10-10/dydo-3-2-0-sitrep-and-skill-voice.md)
