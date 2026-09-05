---
area: understand
type: concept
---

# Templates and Customization

dydo ships every role, skill resource, and project document as a template, then compiles
or installs it with a product command. This is that pipeline: what each kind of source becomes, where
a project hooks into it, and what `dydo template update` does to a file dydo has already written.

## Sources and outputs

Shipped sources live in `Templates/`.

| Pattern | Becomes |
|---|---|
| `skill-<name>.template.md` | the `<name>` skill on both hosts, plus an agent definition when the role emits one |
| `<role>-resource-<name>.template.md` | `resources/<name>.md` beside that role's compiled skill |
| framework `*.template.md` | a project document `dydo init` writes: the `dydo/` tree, and the runtime entry files at the repository root |

Six of the
installed documents stay framework-owned, and they are the only documents a later update compares
against a stored hash: `reference/about-dynadocs.md`, `reference/dydo-commands.md`,
`reference/dydo-glossary.md`, `reference/writing-docs.md`, `reference/linear-workspace-standard.md` and
`guides/working-tree-contract.md`. Every other document `dydo init` writes — `understand/about.md`,
`understand/architecture.md`, `guides/coding-standards.md`, `welcome.md`, `glossary.md`,
`files-off-limits.md`, `index.md`, the hubs and folder meta files, `CLAUDE.md`, `AGENTS.md`, and
`_system/template-additions/_README.md` beside its `extra-verify.md.example` — is written once and is
the project's from then on. Two init outputs a later update still writes: `dydo.json`, where it
refreshes the stored hashes and adds shipped defaults, and `_system/types.json`, which is topped up
rather than compared.

## Authoring a role

The frontmatter keys, what each of them compiles to on each host, and how `## Must-Reads` and a
role's `resources/` reach a spawned agent are in
[Customizing Roles](../guides/customizing-roles.md).

## Include tags

`{{include:name}}` inserts `dydo/_system/template-additions/name.md` at a hook in a skill template,
and resolves to nothing when that file is absent, leaving no trace in the output. Five hooks ship:
`extra-must-reads`, `extra-verify`, `extra-review-steps`, `extra-review-checklist`, and
`extra-test-guidance`.

That folder is where durable customization belongs: an addition stays separate from the shipped
text, is shared by every skill template that names it, and survives the updates below.

## Compilation

```bash
dydo sync
```

`dydo sync` compiles every source into the native artifacts for both hosts; the output map is in
[Architecture Overview](./architecture.md). Compiled files are build products: never edit them
directly — change the source and sync.

Its cleanup is an allowlist of the roles, workflows, and resources dydo itself has retired.
Workflow emission is gone; sync removes only `.claude/workflows/run-sprint.js` and
`.claude/workflows/inquisition.js`, preserving custom siblings and nested files byte-for-byte.
It removes the workflow directory only when empty, including when it started empty, and performs
this cleanup even when only Codex is selected. Delete a template of your own and its last artifacts are yours to
remove, or their descriptions keep loading every turn.

## Template updates

```bash
dydo template update --diff
dydo template update
```

`dydo init` installs the six framework-owned
documents, recording a content hash for each of them in `dydo.json`. An update compares those hashes
against what is on disk, and takes one of two paths per file:

| The file on disk | What the update does |
|---|---|
| still matching its stored hash | replaced with the new shipped text |
| one of the six framework-owned documents, edited | left alone, and reported as user-edited |

Beyond that comparison the same run creates any newly shipped framework-owned document
missing from disk; tops up `_system/types.json` with frontmatter types added since the project was
scaffolded, creating it when absent and leaving a malformed one alone with a warning; adds shipped
nudge and scan-exclusion defaults to `dydo.json`; and
deletes a retired framework asset — today `_assets/dydo-diagram.svg` — when the copy on disk is one
the framework wrote, keeping a modified copy as the project's own.

`--diff` previews the file changes without writing; the `dydo.json` defaults are neither previewed
nor applied under `--diff`.

Review the
diff after an update, run `dydo sync`, and finish with `dydo check`; flags and exit codes are in the
[dydo Commands Reference](../reference/dydo-commands.md).

## Related

- [Customizing Roles](../guides/customizing-roles.md) — authoring a role, its frontmatter, what compiles where
- [Architecture Overview](./architecture.md) — where compilation sits in the system
- [dydo Commands Reference](../reference/dydo-commands.md) — full command documentation
- [Configuration](../reference/configuration.md) — runtime configuration, including model bindings
