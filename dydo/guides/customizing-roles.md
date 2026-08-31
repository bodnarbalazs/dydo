---
area: guides
type: guide
---

# Customizing Roles

The skill template is the role. One `skill-<name>.template.md` carries the metadata in its frontmatter
and the whole methodology in its body; `dydo sync` compiles it into a skill on every host and, when the
frontmatter asks for one, a spawnable agent. Compiled output is a build product: fix the template and
sync again.

---

## Add a role

1. Write the source at `dydo/_system/templates/skill-<name>.template.md`. The filename names the role.
2. Run `dydo sync`.
3. Read the compiled skill on your host before you rely on it — that file is what the model sees.

```markdown
---
mode: data-migrator
description: Data or schema has to move — use when a change needs a migration written, ordered, and reversible.
emit: agent
read-only: false
delegates: false
invocation: automatic
---

# Data Migrator

Move data between shapes without losing a row.

## Must-Reads

1. [architecture.md](../../../understand/architecture.md)

{{include:extra-must-reads}}

## Boundary

What this role decides, and what it leaves to its invoker.

## Method

Numbered steps, each ending on a state someone can check.

## Return

The exact shape the receiver expects.
```

How the body is written — description as trigger, one anchor, a completion criterion on every step —
belongs to the `writing-for-agents` skill and its `skill-mechanics` resource, which state these same
mechanics for the agent doing the writing. This guide covers where the source lives and what the
compiler does with it.

## Frontmatter

| Key | Value | Effect |
|---|---|---|
| `mode` | the role name | Keep it equal to the filename, which is what the compiler actually reads. |
| `description` | one line | Becomes the skill's and the agent's description — the only text a model weighs before reaching for the role. |
| `emit` | `agent` \| `skill` | `agent` (also the default when the key is absent) adds a spawnable agent that preloads this skill; `skill` is methodology a session applies in its own thread. |
| `read-only` | `true` | The compiled agent assesses and reports; it gets no editing tools. |
| `delegates` | `true` | The role may spawn sub-agents. A worker does its own work and goes without it. |
| `invocation` | `automatic` \| `explicit` | `explicit` puts the skill out of every model's reach: only the human, by name. Any other value fails the sync. |

`automatic` buys discovery — the model can fire on the description, and other skills can reach the role —
and costs a description that stays loaded every turn, so write it trigger-first. `explicit` costs no
context and has to be remembered instead, which is why the [orientation file](../index.md) carries the
taxonomy. An `emit: agent` role stays `automatic`: an agent's preload cannot reach an explicit skill.

## What compiles where

| Source | Claude Code | Codex |
|---|---|---|
| the template body | `.claude/skills/<name>/SKILL.md` | `.agents/skills/<name>/SKILL.md` |
| `emit: agent` | `.claude/agents/<name>.md`, carrying `skills: [<name>]` and the `Skill` tool | `.codex/agents/<name>.toml`, whose instructions name the skill to load |
| `read-only: true` | agent tools without `Edit`/`Write` | `sandbox_mode = "read-only"`; a writing role gets `workspace-write` |
| `delegates: true` | the `Agent` tool on the agent | — (a Codex agent carries no tool list) |
| `invocation: explicit` | `disable-model-invocation: true` in `SKILL.md` | `.agents/skills/<name>/agents/openai.yaml` with `allow_implicit_invocation: false` |
| a shipped `<role>-resource-<n>.template.md` | `.claude/skills/<name>/resources/<n>.md` | `.agents/skills/<name>/resources/<n>.md` |

## The context a role carries

**`## Must-Reads`** — markdown links under that heading. They survive into the compiled skill body with
their targets rewritten to resolve from the emitted folder, and repeat as repo-relative paths in a
spawned agent's context block. Write each target as the document's path under `dydo/`, behind a
`../../../` climb (`../../../understand/architecture.md`) or spelled out (`dydo/understand/architecture.md`);
the compiler normalizes both. Close the list with `{{include:extra-must-reads}}` so a project can add its
own without editing framework text.

**Resources** — a `<role>-resource-<name>.template.md` is a role's own reference behind a file
boundary, read only by the branches that need it. A shipped role's body links it as
`resources/<name>.md`, and the compiler rewrites that to the host's emitted path so even a preloaded
agent can read it. The set is the one dydo ships: a project-local file of a shipped resource's name
overrides that resource's content, but a resource name dydo does not ship is never discovered — sync
emits nothing, and a body link to it compiles into a path that does not exist. A custom role's own
reference therefore goes in a `dydo/` document listed under its Must-Reads, and so does reference that
several roles share, unless it earns a model-invoked method skill of its own.

**Includes** — `{{include:<name>}}` pulls in `dydo/_system/template-additions/<name>.md` at the hook,
which keeps project-specific guidance out of framework text. The
[template pipeline](../understand/templates-and-customization.md) covers the available hooks.

## Model tier

`models.roles` in `dydo.json` binds a role to a tier and `models.tiers` binds a tier to one concrete
model per vendor, so a role never names a model. A role with no binding compiles `model: inherit` on
Claude — the session's model, never a silent downgrade — and a built-in default model on Codex. See the
[configuration reference](../reference/configuration.md).

## Override a shipped role

`dydo template update` mirrors every shipped skill and resource template into `dydo/_system/templates/`
and records a hash for each in `dydo.json`. Edit a copy and `dydo sync` compiles your version — it reads
the project-local copy before the shipped source — but only until the next `template update`: once the
shipped text has moved, that update replaces every edit in the mirrored copy, including any
`{{include:…}}` hook you added. The only hooks that survive are the ones the shipped template already
carries.

Durable customization of a shipped role is therefore an addition file under
`dydo/_system/template-additions/`, filling a hook the shipped template ships, or a role of your own
under a new name — never an edit to the mirrored copy. The
[template pipeline](../understand/templates-and-customization.md) carries the update flow in full.

One ordering trap survives either choice: a mirrored copy older than the shipped source compiles in
place of it, so update first, sync second, and read what changed.

## What is gone

- The separate role data file and the commands that maintained it. The template is the role, and the
  frontmatter above is the whole schema.
- Two framework roles from the retired delivery loop, the workflow harness that ran it, and one
  rubric renamed.
  `dydo sync` sweeps their compiled output from both hosts, `dydo template update` deletes the mirrored
  copies and prunes their hashes, and `dydo init` never installs them again. The
  [glossary](../reference/dydo-glossary.md)'s retired-terms paragraph carries the words themselves.
- The sweep has one deliberate escape hatch: a project-local `skill-<name>.template.md` under a retired
  name defines that role again and suppresses its cleanup.

## Related

- [Templates and Customization](../understand/templates-and-customization.md) — the template pipeline end to end
- [Configuration Reference](../reference/configuration.md) — `models.roles`, hashes, nudges
- [dydo Commands Reference](../reference/dydo-commands.md) — `dydo sync`, `dydo template update`
- [dydo Glossary](../reference/dydo-glossary.md) — hat, worker, method, and the retired terms
- [Orientation](../index.md) — the shipped taxonomy and what each role is reached for
