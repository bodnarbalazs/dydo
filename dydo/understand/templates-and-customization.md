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
| framework `*.template.md` | an installed project document: the `dydo/` files `dydo init` scaffolds, and the runtime entry files at the repository root |

Skill and resource templates are mirrored into the project; workflow sources are not.

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

`dydo init` mirrors the shipped templates and installs the framework documents, and `dydo.json`
records a content hash for every file dydo wrote. An update compares that hash against what is on
disk, and takes one of four paths:

| The file on disk | What the update does |
|---|---|
| still matching its stored hash | replaced with the new shipped text |
| a template the project has edited | the project's own `{{include:…}}` tags are re-anchored into the new shipped text, and every other edit is replaced |
| a framework document the project has edited | left alone, and reported as user-edited |
| a mirrored copy of a template dydo has retired | deleted; an untracked template is the project's own role and is kept |

`--diff` previews all of it without writing. `--force` is for the one case that stops: when an include
tag cannot be re-anchored into the new text, the update skips that file and names the tag, and
`--force` writes anyway — backing the file up first and saving what it could not place. A hash-clean
copy left over from the 2.x `mode-<name>.template.md` naming is moved to its
`skill-<name>.template.md` replacement, while a modified legacy file is kept and reported for you to
rename, because `dydo sync` compiles only `skill-*` sources.

So an edit you want to survive an update belongs in `dydo/_system/template-additions/`, not in
framework-owned text. Review the diff after an update, run `dydo sync`, and finish with `dydo check`;
flags and exit codes are in the [dydo Commands Reference](../reference/dydo-commands.md).

## Related

- [Customizing Roles](../guides/customizing-roles.md) — authoring a role, its frontmatter, what compiles where
- [Architecture Overview](./architecture.md) — where compilation sits in the system
- [dydo Commands Reference](../reference/dydo-commands.md) — full command documentation
- [Configuration](../reference/configuration.md) — runtime configuration, including model bindings
