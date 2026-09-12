---
area: understand
type: concept
---

# Templates and Customization

dydo authors every role as a plain skill folder in the cross-vendor `SKILL.md` format. There is no
compile step: a skill is the source of truth, and the hosts read it where it lives. This page covers
the artifact shapes, how a project customizes one, and the framework documents `dydo init` writes.

## Skills are folders

A skill is `.claude/skills/<name>/SKILL.md` on Claude Code and `.agents/skills/<name>/SKILL.md` on
Codex, per the [agentskills.io](https://agentskills.io) standard: `name` and `description` in the
frontmatter, the methodology in the body, and optional `references/`, `scripts/`, `assets/`, or a
role's own `resources/` beside it. Each host scans its own folder; neither transforms the body.

This repository commits both copies, hand-maintained. A change edits each host's copy. DR 049 chose
committed copies over symlinks because a Windows checkout with symlinks disabled materialises a
linked folder as a small text file, stranding that host's discovery; a copy rolls back with git and
cannot strand a checkout. The two copies differ only in host-specific metadata: Claude's
`SKILL.md` frontmatter and Codex's `.agents/skills/<name>/agents/openai.yaml`.

## Links

- A project document link climbs out of the skill folder: `../../../dydo/understand/architecture.md`.
  Both hosts place a skill three levels below the repository root, so one climb resolves on either.
- A role's own resource is linked `resources/<name>.md`, resolved from the skill folder.

## Customizing a role

Edit the skill folder directly. A project's copy is its own: deliberate divergence from the
framework is accepted, and there is no automatic reconciliation. The frontmatter keys and what each
host reads are in [Customizing Roles](../guides/customizing-roles.md).

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
