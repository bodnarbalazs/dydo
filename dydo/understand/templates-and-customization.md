---
area: understand
type: concept
---

# Templates and Customization

dydo authors role methodology, skill resources, workflows, and framework documents as templates, then
compiles or installs them with product commands. This is what a template's frontmatter means, what
each kind of source turns into, and how a project customizes any of it without losing updates.

## Template sources

Shipped sources live in `Templates/`. A project's copies and overrides live in
`dydo/_system/templates/`, which `dydo sync` reads first, so a copy there shadows the shipped source
of the same name.

| Pattern | Becomes |
|---|---|
| `skill-<name>.template.md` | the `<name>` skill on both hosts, plus an agent definition when the role emits one |
| `<role>-resource-<name>.template.md` | `resources/<name>.md` beside that role's compiled skill |
| `workflow-<name>.js` | `.claude/workflows/<name>.js` |
| framework `*.template.md` | an installed project document: the `dydo/` files `dydo init` scaffolds, and the runtime entry files at the repository root |

Skill and resource templates are mirrored into a project; workflow sources are not.

## The role's frontmatter

The skill template is the role. Its filename names the role, and its frontmatter tells the compiler
what to emit.

| Key | Value | What the compiler does with it |
|---|---|---|
| `mode` | the role name | Read by nothing — the filename names the role. Keep the two equal. |
| `description` | one line | Becomes the description of both the skill and the agent — the line a model, or a human, routes on. |
| `emit` | `agent` or `skill` | `agent` compiles a spawnable agent beside the skill, which loads that skill before working; `skill` compiles methodology a session applies in its own thread. |
| `read-only` | `true` | The compiled agent gets no Edit or Write, and runs in the read-only sandbox on Codex: it assesses and reports. |
| `delegates` | `true` | Grants the Agent tool, so the role may spawn sub-agents. A worker does its own work and goes without it. |
| `invocation` | `automatic` or `explicit` | `explicit` means only the human's typed name invokes it: `disable-model-invocation: true` on Claude, `allow_implicit_invocation: false` on Codex. |

An omitted key takes the permissive default: an agent that may write, may not delegate, and may be
model-invoked. The agent-facing statement of the same keys is the `skill-mechanics` resource of the
`writing-for-agents` skill; keep the two in agreement.

## The body

Everything after the frontmatter becomes the methodology. The compiler resolves include tags,
de-personalizes the prose, renumbers ordered lists, and rewrites the body's links to resolve from the
folder the skill is emitted into. Two parts carry a contract with the compiler:

- **`## Must-Reads`** — the markdown links under that heading survive into the compiled skill body and
  are also collected into a spawned agent's context block. Close the list with
  `{{include:extra-must-reads}}` so a project can add its own without editing framework text.
- **`resources/<name>.md` links** — rewritten to the host's emitted path, so an agent that holds its
  skill preloaded, with no folder to resolve a relative link against, can still read the resource.

## Compilation

```bash
dydo sync
```

`dydo sync` reads shipped templates plus project overrides and writes the native artifacts for both
hosts; the full output map is in [Architecture Overview](./architecture.md). Compiled files are build
products: never edit them directly — change the source template and sync.

Its cleanup is an allowlist of the roles, workflows and resources dydo itself has retired, not a
general output cleaner. Delete a template of your own and the artifacts it last compiled are yours to
remove, or their descriptions keep loading every turn.

## Include tags

`{{include:name}}` inserts `dydo/_system/template-additions/name.md` at a supported hook, and resolves
to nothing when the file is absent. Additions keep project-specific guidance separate from
framework-owned text, so they survive product updates. Common hooks cover extra must-reads,
verification steps, review checks, completion gates, and testing guidance; a custom template may
define include names of its own.

## Template updates

```bash
dydo template update --diff
dydo template update
dydo template update --force
```

The diff form previews framework-owned changes without writing. The normal update uses stored content
hashes to refresh files the project has not touched, to re-anchor supported include hooks in files it
has, and to prune copies of templates dydo has retired. `--force` is the deliberate fallback when
re-anchoring cannot succeed: it overwrites, and backs up first. A hash-clean copy left over from the
2.x `mode-<name>.template.md` naming is moved to its `skill-<name>.template.md` replacement; a
modified or conflicting legacy file is preserved and reported for you to rename, because `dydo sync`
compiles only `skill-*` sources.

Review the diff after an update, run `dydo sync`, and finish with `dydo check`. Flags and exit codes
are in the [dydo Commands Reference](../reference/dydo-commands.md).

## Related

- [Customizing Roles](../guides/customizing-roles.md) — authoring a new role or overriding a shipped one
- [Architecture Overview](./architecture.md) — where compilation sits in the system
- [dydo Commands Reference](../reference/dydo-commands.md) — full command documentation
- [Configuration](../reference/configuration.md) — runtime configuration, including model bindings
