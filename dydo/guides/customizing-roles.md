---
area: guides
type: guide
---

# Customizing Roles

A role is a skill folder. Author the body directly in the cross-vendor `SKILL.md` format; there is
no template and no compile step. The [templates page](../understand/templates-and-customization.md)
covers the artifact shapes and the link rules; this page covers the frontmatter and what each host
reads.

---

## Frontmatter

| Key | Value | Effect |
|---|---|---|
| `name` | the role name | Keep it equal to the folder name; it is the identity on both hosts. |
| `description` | one line | The only text a model weighs before reaching for the role. |
| `disable-model-invocation` | `true` | Claude-only: the skill is out of every model's reach; only the human, by name. |
| `argument-hint` | one quoted line | Claude-only: the prompt the host shows after the name. Codex carries the same hint in the skill's `agents/openai.yaml`. |

`automatic` discovery buys reach — the model can fire on the description, and other skills can reach
the role — and costs a description that stays loaded every turn, so write it trigger-first. An
explicit role costs no context and has to be remembered instead, which is why the
[dydo Glossary](../reference/dydo-glossary.md) carries the taxonomy.

## What each host reads

| Artifact | Canonical path | Host behavior |
|---|---|---|
| the skill | `skills/<name>/SKILL.md` | Claude Code, Codex, and OpenCode read the same body through their discovery roots. |
| the role's own resource | `skills/<name>/resources/<n>.md` | Relative links resolve from the whole-folder projection. |
| explicit invocation | `disable-model-invocation: true` in `SKILL.md`; `skills/<name>/agents/openai.yaml` with `allow_implicit_invocation: false` | Claude Code and Codex respectively. Stable OpenCode has no claimed explicit-only control. |
| an argument hint | `argument-hint:` in `SKILL.md`; `skills/<name>/agents/openai.yaml` with `interface.default_prompt` | Claude Code and Codex respectively. |

Nothing generates the canonical files. `node setup-skills.mjs` creates only host discovery links;
edit `skills/<name>/` once.

## The context a role carries

**`## Must-Reads`** — project documents named under that heading. Write each target as a
repository-root literal path in a code span, read from the repository root — "From the repository
root, read `dydo/understand/architecture.md`" — the way every shipped role names its own (see
`skills/reviewer/SKILL.md`); `DynaDocs.Tests/Steps/CanonicalSkillSteps.cs:150-156` enforces that no
Must-Read is written as a markdown link. A `../` climb does not work here because the identical file
is read at two different depths: canonically at `skills/<name>/`, and through the host projection at
`.claude/skills/<name>/` or `.agents/skills/<name>/`. The two resolvers disagree — lexical `..`
normalization against the projected path versus POSIX `..` applied to the physical parent once the
symlink is followed — so no single relative climb is correct from every install location. A project
adds its own context by editing the skill body directly.

**Resources** — a role's own reference behind a file boundary, read only by the branches that need
it. Link it as `resources/<name>.md`, relative to the skill folder. Reference several skills share
lives instead in a model-invoked method skill or in a `dydo/` document listed under Must-Reads.

**Includes** — retired with the compiler ([Decision 049](../project/decisions/049-skills-are-the-source-retire-the-compiler.md)).
Project-specific guidance lives in the skill body, or in a project document linked under Must-Reads.

## Choosing a model for a task

Nothing in a skill or in `dydo.json` binds a role to a model or an effort. Every role is left
unbound on purpose, so whoever delegates picks the capability the task in front of them deserves;
the shipped `admiral` and `issue-captain` methods carry that judgment.

Select at the call, not in a file:

- **Claude Code** — pass `model` on the Agent call that spawns the role. Effort belongs to the
  session, so open the session at the effort the work needs.
- **Codex** — pass the model and a reasoning effort that model supports, together, on the spawn.

The [configuration reference](../reference/configuration.md) carries each host's full resolution
order and the limits worth knowing before a claim rests on one.

## What is gone

- The template pipeline: `dydo sync`, `dydo template update`, the `dydo.json.skills` switchboard,
  `frameworkHashes`, include tags and their re-anchoring, and compiler-owned output cleanup.
- Generated agent definitions (`.claude/agents/*.md`, `.codex/agents/*.toml`). A role is a skill.
  Reviewer, inquisitor and scout are non-authoring by commission and method. Native agent
  configuration may request a restrictive sandbox, but its presence is not proof that the host
  enforces the restriction.

Workflow as a delivery concept is retired by DR 047. The Inquisition Issue protocol supplies the
current audit procedure.

## Related

- [Templates and Customization](../understand/templates-and-customization.md) — the artifact shapes end to end
- [Configuration Reference](../reference/configuration.md) — dispatch-time model and effort, nudges
- [dydo Commands Reference](../reference/dydo-commands.md) — the CLI
- [dydo Glossary](../reference/dydo-glossary.md) — hat, worker, method, and the retired terms
- [Orientation](../index.md) — the shipped taxonomy and what each role is reached for
