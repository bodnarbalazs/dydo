---
area: reference
type: reference
linear-project: https://linear.app/bodnar-balazs/project/dydo-360-current-release-25acbbb9a90b
---

# Safe Feature Merging

## Problem

Agents need to integrate reviewed feature work without gaining authority to merge into the default
branch. A host classifier's judgment cannot enforce that boundary consistently across Claude and
Codex. The command, guard and host permissions must agree on which operations are allowed.

## Behavior

Agents merge reviewed work into `feature/*` through `dydo merge`; the default branch remains the
user's merge. The command binds the operation to the reviewed head, checks its PASS and CI, and
produces a merge commit under [DR 061's command contract](../../../project/decisions/061-agents-merge-only-into-feature-branches.md#decision).

The delivered command and init allow rules do not establish the remaining guard blocks or genuine
host parity. Guard behavior must close the other agent merge routes; the same command must receive
the same allow or block outcome on Claude and Codex. Those shared obligations remain pending until
their owning tickets supply the required evidence. Linear holds their live state and dependencies.

## Shared decisions

[DR 061](../../../project/decisions/061-agents-merge-only-into-feature-branches.md) governs this spec.
Its Decision 1–6 owns the command/refusals, review and CI checks, merge form and crew boundary;
Decision 7–8 owns the planned guard blocks and host parity. Its
[Consequences](../../../project/decisions/061-agents-merge-only-into-feature-branches.md#consequences)
retain the accepted residuals: guards stop accidents, not rogue agents. The user removes the stopgap
`autoMode.allow` entry and the user-level `Bash(gh pr merge:*)` allow rule only after the parity gate
passes in LC.

[Safe feature merging (DYD-417)](https://linear.app/bodnar-balazs/issue/DYD-417), sections
“Contract amendment” and “Captain's acceptance contract,” owns the delivery split and amendments to
the earlier Linear design. The command, guard and parity tickets keep their bounded outcomes and
exact gates; this spec supplies their common intent.

## Out of scope

Default-branch merging by agents, squash/rebase integration, new override paths and stronger
guarantees than DR 061's accepted residuals are outside this feature. This spec grants no settings,
permission, runtime-mode or release changes.

This spec changes no hold, dependency or prerequisite on the safe-merge tickets or their linked
holds; Linear owns their current state. Recording this intent authorizes no writer, probe, test or
review of held delivery.

## Acceptance checks

### Command and integration

[Command delivery (DYD-424)](https://linear.app/bodnar-balazs/issue/DYD-424) supplies the delivered
command's evidence under DR 061. The merge SHA remains the candidate for combined gates and fresh
merge review. This spec does not recast that earlier delivery as work performed from this spec.

### Guard behavior

Use [Guard blocks (DYD-425)](https://linear.app/bodnar-balazs/issue/DYD-425), sections “Outcome,”
“Sequencing” and “Gates,” for the exact blocked and allowed cases, Bash/PowerShell/Codex envelopes,
dependency and red-before-fix proof. The decided hard blocks are not established by command tests
or by writing this spec.

### Host parity

Use [Claude/Codex parity (DYD-426)](https://linear.app/bodnar-balazs/issue/DYD-426), sections
“Outcome (the condition, with evidence)” and “Constraints,” for the actual command matrix across
both hosts and checkout kinds, expected outcomes and installation prerequisites. Every pair must
match; a mismatch is a blocker. Synthetic events or a dry-run alone do not establish that matrix.
The real merge stays in the throwaway feature branch, never the default branch.

## Related

- [Feature Spec Convention](../../feature-spec-convention.md) - custody, section pins and reconciliation
- [Working Specs](../_working-specs.md) - current shared intent by Project
