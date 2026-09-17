---
area: understand
type: concept
---

# Templates and Customization

dydo authors every role as a plain skill folder in the cross-vendor `SKILL.md` format. There is no
compile step: a skill is the source of truth, and the hosts read it where it lives. This page covers
the artifact shapes, how a project customizes one, and the framework documents `dydo init` writes.

## Skills are folders

A skill is `skills/<name>/SKILL.md`, per the [agentskills.io](https://agentskills.io) standard:
`name` and `description` in the
frontmatter, the methodology in the body, and optional `references/`, `scripts/`, `assets/`, or a
role's own `resources/` beside it. Claude frontmatter and Codex `agents/openai.yaml` stay in that one
folder. `node setup-skills.mjs` projects the whole folder into `.claude/skills/<name>` and
`.agents/skills/<name>` with a POSIX directory symlink or Windows junction. OpenCode reads both
compatibility roots; no `.opencode/skills` copy is created. No host transforms the body.

## Links

- A project document link names a repository-root literal path in a code span, read from the
  repository root: "From the repository root, read `dydo/understand/architecture.md`" — never a
  `../` climb, because the identical file is read at two different depths: canonically at
  `skills/<name>/`, and through the host projection at `.claude/skills/<name>/` or
  `.agents/skills/<name>/`. The two resolvers disagree — lexical `..` normalization against the
  projected path versus POSIX `..` applied to the physical parent once the symlink is followed — so
  no single relative climb is correct from every install location.
- A role's own resource is linked `resources/<name>.md`, resolved from the skill folder.

## Customizing a role

Edit `skills/<name>/` directly. A project's canonical folder is its own: deliberate divergence from the
framework is accepted, and there is no automatic reconciliation. The frontmatter keys and what each
host reads are in [Customizing Roles](../guides/customizing-roles.md).

Setup preflights the whole plan before it writes anything: a missing target is planned for creation,
an existing projection that already resolves to the canonical folder is accepted, and any ordinary
file, directory, or link to another target stops the run at the first such collision it finds, naming
it and leaving it and all host configuration and unrelated skills untouched. Resolve the named
collision deliberately and rerun the same command.

## Framework documents

`dydo init` scaffolds the `dydo/` documentation tree, the runtime entry points (`CLAUDE.md`,
`AGENTS.md`), `files-off-limits.md`, the host hooks, and `_system/types.json`. The framework
documents under `dydo/reference/` and `dydo/guides/` are written once; from then on a project edits
them in place. `dydo init` tops up `_system/types.json` rather than comparing it.

## Retired

`dydo sync` as a compiler, `dydo template update`, the `dydo.json.skills` switchboard,
`frameworkHashes`, `{{include:...}}` tags and their re-anchoring, and compiler-owned output cleanup
are retired ([Decision 049](../project/decisions/049-skills-are-the-source-retire-the-compiler.md)).
Nothing in `dydo.json` binds a role to a model or an effort; the caller picks capability per task.

## Related

- [Customizing Roles](../guides/customizing-roles.md) — frontmatter and what each host reads
- [Architecture Overview](./architecture.md) — where authoring sits in the system
- [dydo Commands Reference](../reference/dydo-commands.md) — full command documentation
- [Configuration](../reference/configuration.md) — runtime configuration
