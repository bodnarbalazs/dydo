---
area: project
type: folder-meta
---

# Changelog

Chronological record of completed work. Essential for debugging and understanding what changed when.

## When to Write an Entry

Create a changelog entry when:
- A task is approved
- Significant changes are deployed
- Bugs are fixed

## Folder Structure

Organize by year and date:
```
changelog/
├── 2025/
│   ├── 2025-01-15/
│   │   ├── auth-refactor.md
│   │   └── token-migration.md
│   └── 2025-01-20/
│       └── api-versioning.md
└── 2026/
    └── ...
```

> **Note:** This structure is a suggestion. Flat organization or other schemes work fine-dydo doesn't enforce changelog folder structure.

## File Format

Filename: `topic-name.md` (kebab-case)

Required sections:
- **Summary** - What was done and why
- **Files Changed** - Every file touched (critical for debugging)

## Releases

Each release entry carries an `## Upgrade steps` section: the commands and edits an existing
project runs to adopt it, each one a person can follow by hand, and every skill renamed. The
`update-dydo` skill reads it and applies the steps its own steps do not cover. The section says
None when a release has no steps.

A release whose commits rename terms mechanically also carries `### Merge normalisations` under
its Upgrade steps: one numbered entry per rename, in the order to apply them, each naming its
source, replacement, matching rule, scope and exceptions. Every change the commit makes in that
scope fits the rule or is named as carried by the merge; every use it keeps fits an exception.
`update-dydo` applies them to temporary copies of base, ours and theirs before a three-way merge,
so a renamed line does not conflict with a project's edit to it.

- [dydo 3.5.0 - Dispatch rule, decision skill and overnight mode](./2026/2026-10-23/dydo-3-5-0-dispatch-decisions-overnight-mode.md)
- [dydo 3.4.1 - Guard refusals as a deny decision](./2026/2026-10-18/dydo-3-4-1-guard-deny-decision.md)
- [dydo 3.4.0 - New-file nudges and --root](./2026/2026-10-17/dydo-3-4-0-new-file-nudges-and-root.md)
- [dydo 3.3.0 - Working specs and communication](./2026/2026-10-14/dydo-3-3-0-working-specs-and-communication-skills.md)
- [dydo 3.2.0 - Sitrep and skill voice](./2026/2026-10-10/dydo-3-2-0-sitrep-and-skill-voice.md)
- [dydo 3.1.5 - Skill release preparation](./2026/2026-10-05/dydo-3-1-5-skill-release.md)
- [dydo 3.1.3 - Map change memory](./2026/2026-10-04/dydo-3-1-3-map-change-memory.md)
- [dydo 3.1.2 - Map labels](./2026/2026-10-02/dydo-3-1-2-map-labels.md)
- [dydo 3.1.1 - Map polish](./2026/2026-09-27/dydo-3-1-1-map-polish.md)
- [dydo 3.1.0 - Linear project map](./2026/2026-09-25/dydo-3-1-0-linear-project-map.md)
- [dydo 3.0.0 - Linear PM and Notion runtime removal](./2026/2026-08-27/dydo-3-0-0-linear-pm-and-notion-runtime-removal.md)

---

## Related

- [Decisions](../decisions/_decisions.md) - Why choices were made
