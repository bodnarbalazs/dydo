---
area: understand
type: concept
---

# Templates and Customization

dydo ships every role, skill resource, workflow, and framework document as a template, then compiles
or installs it with a product command. This is that pipeline: what each kind of source becomes, where
a project hooks into it, and what `dydo template update` does to a file dydo has already written.

## Sources and outputs

Shipped sources live in `Templates/`. A project's mirrored copies, and any role it authors itself,
live in `dydo/_system/templates/`.

| Pattern | Becomes |
|---|---|
| `skill-<name>.template.md` | the `<name>` skill on both hosts, plus an agent definition when the role emits one |
| `<role>-resource-<name>.template.md` | `resources/<name>.md` beside that role's compiled skill |
| `workflow-<name>.js` | `.claude/workflows/<name>.js` |
| framework `*.template.md` | a project document `dydo init` writes: the `dydo/` tree, and the runtime entry files at the repository root |

Skill and resource templates are mirrored into the project; workflow sources are not. Six of the
installed documents stay framework-owned, and they are the only ones a later update revisits:
`reference/about-dynadocs.md`, `reference/dydo-commands.md`, `reference/dydo-glossary.md`,
`reference/writing-docs.md`, `guides/how-to-use-docs.md` and `guides/working-tree-contract.md`.
Everything else `dydo init` writes — `understand/about.md`, `understand/architecture.md`,
`guides/coding-standards.md`, `welcome.md`, `glossary.md`, `files-off-limits.md`, `index.md`, the
hubs and folder meta files, `CLAUDE.md` and `AGENTS.md` — is written once and is the project's from
then on.

## Authoring a role

The frontmatter keys, what each of them compiles to on each host, how `## Must-Reads` and a role's
`resources/` reach a spawned agent, and how to add or override a role are in
[Customizing Roles](../guides/customizing-roles.md).

## Include tags

`{{include:name}}` inserts `dydo/_system/template-additions/name.md` at a hook in the template, and
resolves to nothing when that file is absent, leaving no trace in the output. Five hooks ship:
`extra-must-reads`, `extra-verify`, `extra-review-steps`, `extra-review-checklist`, and
`extra-test-guidance`. A project's own template may define any other name.

That folder is where durable customization belongs: an addition stays separate from framework-owned
text, is shared by every template that names it, and survives the updates below.

## Compilation

```bash
dydo sync
```

`dydo sync` compiles every source into the native artifacts for both hosts; the output map is in
[Architecture Overview](./architecture.md). Compiled files are build products: never edit them
directly — change the source and sync.

Its cleanup is an allowlist of the roles, workflows, and resources dydo itself has retired, not a
general output cleaner. Delete a template of your own and the artifacts it last compiled are yours to
remove, or their descriptions keep loading every turn.

## Template updates

```bash
dydo template update --diff
dydo template update
dydo template update --force
```

`dydo init` mirrors the shipped skill and resource templates and installs the six framework
documents, recording a content hash for each of them in `dydo.json`. An update visits exactly that
set — nothing else — and takes one of four paths:

| The file on disk | What the update does |
|---|---|
| still matching its stored hash | replaced with the new shipped text |
| a mirrored template the project has edited | replaced outright by an update that ships new text, the project's added `{{include:…}}` tags with it; those tags are carried into the new text only while the shipped text itself is unchanged |
| one of the six framework documents, edited | left alone, and reported as user-edited |
| a mirrored copy of a template dydo has retired | deleted; a role the project authored itself is untracked, and is kept |

`--diff` previews all of it without writing. `--force` covers the one case that stops: when a
carried-over tag finds no place in the new text, the update skips that file and names the tag, and
`--force` writes anyway — backing the file up first and saving what it could not place. A hash-clean
copy left over from the 2.x `mode-<name>.template.md` naming is moved to its
`skill-<name>.template.md` replacement, while a modified legacy file is kept and reported for you to
rename, because `dydo sync` compiles only `skill-*` sources.

That carry-over is not a durability mechanism: it runs only while the shipped text is unchanged, and
it stores the hash of what it wrote, so the next update replaces the file and those tags with it. Two
things do survive — content in `dydo/_system/template-additions/`, reached through a hook the shipped
template already carries, and a role the project authored itself, which no update tracks. Review the
diff after an update, run `dydo sync`, and finish with `dydo check`; flags and exit codes are in the
[dydo Commands Reference](../reference/dydo-commands.md).

## Related

- [Customizing Roles](../guides/customizing-roles.md) — authoring a role, its frontmatter, what compiles where
- [Architecture Overview](./architecture.md) — where compilation sits in the system
- [dydo Commands Reference](../reference/dydo-commands.md) — full command documentation
- [Configuration](../reference/configuration.md) — runtime configuration, including model bindings
