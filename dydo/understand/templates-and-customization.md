---
area: understand
type: concept
---

# Templates and Customization

dydo authors role methodologies, skill resources, workflows, and framework documents as templates, then
uses product commands to compile or install their outputs. Templates contain durable process guidance;
they do not define a repository work hierarchy or a Linear schema.

## Template sources

Shipped sources live in `Templates/`. Installed project copies and overrides live in
`dydo/_system/templates/`.

| Pattern | Purpose |
|---|---|
| `mode-<name>.template.md` | Role methodology and emission metadata |
| `<role>-resource-<name>.template.md` | Skill-specific reference resource |
| `workflow-*.js` | Host-native workflow source |
| framework `*.template.md` files | Installed orientation, reference, and folder documents |

The mode template's frontmatter selects whether the role emits only an in-session skill or also a
spawnable worker-agent definition. The body becomes the compiled methodology.

## Compilation

```bash
dydo sync
```

`dydo sync` reads shipped templates plus project overrides and emits the supported native artifacts:

- Claude skills and worker agents under `.claude/`;
- Codex worker agents under `.codex/agents/`;
- shared Codex skills under `.agents/skills/`;
- supported native workflows.

Compiled outputs are generated artifacts. Never edit them directly; change the source template and sync.

## Include tags

`{{include:name}}` inserts `dydo/_system/template-additions/name.md` at a supported hook. Additions keep
project-specific guidance separate from framework-owned text and survive product updates more reliably.

Common hooks include extra must-reads, verification steps, review checks, completion gates, and testing
guidance. A custom template may define additional include names.

## Template updates

```bash
dydo template update --diff
dydo template update
dydo template update --force
```

The diff form previews framework-owned changes. The normal update uses stored hashes to refresh clean
files and re-anchor supported include hooks in customized files. `--force` is a deliberate fallback
that overwrites when re-anchoring cannot succeed and creates backups where applicable.

Review the diff after an update, run `dydo sync`, and finish with `dydo check`. Framework documents and
compiled artifacts must agree with their sources.

## Work-model boundary

Role methods receive Linear Issue/Project context from the host or coordinator. Coordinated work may
link to a reviewed repository Project plan, but templates do not create a second PM schema, client,
cache, or Markdown mirror.

## Related

- [Customizing Roles](../guides/customizing-roles.md)
- [CLI Commands](../reference/dydo-commands.md)
- [Configuration](../reference/configuration.md)
