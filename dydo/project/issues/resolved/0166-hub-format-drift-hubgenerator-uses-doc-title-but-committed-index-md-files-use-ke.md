---
title: Hub format drift: HubGenerator uses doc.Title but committed _index.md files use KebabToTitleCase(filename), causing 18-file noise on every dydo fix
id: 166
area: backend
type: issue
severity: medium
status: resolved
found-by: review
date: 2026-05-05
---

# Hub format drift: HubGenerator uses doc.Title but committed _index.md files use KebabToTitleCase(filename), causing 18-file noise on every dydo fix

The committed `_index.md` files in `dydo/project/changelog/2026/2026-*/` (and similar hub locations) were generated using `KebabToTitleCase(filename)` to format link labels, while the current `HubGenerator` uses `doc.Title` (the H1 of the target doc). Running `dydo fix` regenerates ~18 changelog `_index.md` files with the new formatting, producing a noisy diff that obscures real changes. Surfaced during PR1 (#0163) implementation by Brian; reverted from PR1's footprint to keep that commit focused. Needs to be resolved before PR2 of the dydo-check-drift batch runs `dydo fix` as part of its BC verification probe — otherwise PR2's diff will sweep all 18 files as collateral and the real PR2 changes (`tasks/_index.md` deletion, `project/_index.md` Tasks-section update) will be lost in the noise.

## Description

The mismatch:

- **Committed shape** (older HubGenerator behavior): link label rendered from `KebabToTitleCase(<filename-without-ext>)`. E.g. for `auto-resume-smoke-v140.md`, the label is "Auto Resume Smoke V140".
- **Current shape** (today's HubGenerator behavior): link label rendered from `doc.Title` — the H1 inside the target doc. E.g. for the same file, if its H1 is `# Task: auto-resume-smoke-v140` (or the changelog format `# 2026-05-04: auto-resume-smoke-v140`, etc.), the label becomes that text.

The two diverge for any doc whose H1 doesn't textually match `KebabToTitleCase(filename)`. Changelog entries are the visible cluster (~18 files) because they have a date-prefixed H1 by template.

This is a pre-existing bug — neither fork was actively wrong when committed, but the codebase shipped a HubGenerator change without regenerating the affected hubs. Brian observed it on the dydo project; same issue is likely in any other dydo project with auto-generated hubs (e.g. `Desktop\LC`).

## Reproduction

On main, with no other in-flight changes:

```
$ dydo fix
# Regenerates project/changelog/2026/2026-*/index.md across ~18 folders.
$ git diff --stat
# Shows ~18 _index.md modified, label-text-only diffs.
```

The diff is purely textual (the link target paths are identical); only the rendered link labels differ.

## Suggested fix paths

(Investigator should evaluate.)

1. **(a) Regenerate committed hubs to match current `HubGenerator`.** Run `dydo fix` once, commit the resulting 18-file diff with a message like "regen hubs to match HubGenerator(doc.Title) shape". After this, `dydo fix` is a no-op on a clean tree. Cleanest if `doc.Title` is genuinely the better label source.

2. **(b) Revert HubGenerator to `KebabToTitleCase(filename)`.** If the older shape was actually preferred (e.g., for date-stable, content-independent labels), revert the change in `Services/HubGenerator.cs` (line TBD by code-writer). After this, `dydo fix` is a no-op.

3. **(c) Hybrid.** Use `doc.Title` for content docs but `KebabToTitleCase` for index labels in changelog/timeline-style hubs where the H1 carries verbose date+name and would render poorly as a link label.

The choice is a design call. Read both shapes side-by-side on a representative changelog folder (e.g. `dydo/project/changelog/2026/2026-05-04/`) before picking. Brian's observation: doc.Title for changelog entries produces verbose labels because the template H1 is date-prefixed.

Recommendation lean: (a) is the simplest, most consistent path if the verbose labels are tolerable. If they're not, (b) or (c) — but either of those needs a clear scoping rule for which folders use which formatter.

## Why this needs to land before PR2

PR2 of the dydo-check-drift batch runs `dydo fix` as part of its BC verification probe ("template update + fix on this project"). With this drift unresolved, PR2's BC commit will sweep all 18 changelog `_index.md` files alongside the real PR2 changes, making the diff hard to review and obscuring whether PR2's BC story actually works. Resolving #0166 first lets PR2's BC commit focus on its intended changes (`tasks/_index.md` deletion, `project/_index.md` regen with Tasks prose).

## Related

- PR1 (#0163) — Brian observed this while running BC verification on PR1; reverted from PR1's footprint via `git checkout`.
- Plan: `dydo/agents/Brian/plan-dydo-check-drift.md` — PR2 BC migration probe section.
- `Services/HubGenerator.cs` — current implementation.
- Future: same drift may exist on any other dydo project (e.g., `Desktop\LC`); fixing here doesn't fix downstream; downstream fixes are user-run `dydo fix`.

## Resolution

(Filled when resolved)
