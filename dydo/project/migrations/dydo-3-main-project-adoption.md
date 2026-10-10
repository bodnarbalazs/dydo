---
area: project
type: context
---

# Main-project adoption of dydo 3

This record describes the local adoption boundary for dydo 3.0.0. Live PM belongs in Linear; Git and
dydo retain durable knowledge, reviewed plans, Decisions, and FutureFeatures.

The 3.0 candidate removes the local Notion provider, its watchdog, token and vault code, and the
consumerless external-data sync engine. It does not read, alter, archive, or delete remote Notion
content or local rollback stores.

Release, tag, and publication remain separate human acceptance actions.

## Adopting 3.x

dydo no longer compiles or updates a project's skills or documents
([Decision 049](../decisions/049-skills-are-the-source-retire-the-compiler.md) section 3 retires
`dydo sync` as a compiler and `dydo template update`; section 4 promises no automatic
reconciliation). An adopting project does three things by hand:

1. **Install the skill tree.** Copy `skills/`, `setup-skills.mjs` and `THIRD-PARTY-NOTICES.md` from
   the dydo repository into the project root, commit them, and run `node setup-skills.mjs`, which
   links every skill flat into `.claude/skills/` and `.agents/skills/`
   ([Native skills](../../understand/architecture.md#native-skills); the steps are in the
   repository `README.md`, "Install the skills"). The copy is the project's own from then on.
2. **Reconcile the scaffold documents.** `dydo init` writes a scaffold file only when it is absent
   (`Services/ScaffoldTree.cs`, `WriteDydoTree`), so a project's `dydo/` copies freeze at their
   first init and fall behind the shipped skills. Compare each file under `Scaffold/dydo/` in the
   dydo repository with the project's copy and bring the project's copy up to it. Search `skills/`
   for the `dydo/` files and sections the shipped skills cite, and make sure each cited section
   exists in the project's copy. Keep a deliberate local divergence, such as a localized workspace
   standard or working-tree contract, as long as the cited sections still stand.
3. **Validate.** Run `dydo check`.

## Related

- [Skills Are the Source: Retire the Compiler](../decisions/049-skills-are-the-source-retire-the-compiler.md) - why nothing reconciles skills or documents
- [Scaffold and Customization](../../understand/scaffold-and-customization.md) - how `dydo init` scaffolds and what a project edits
