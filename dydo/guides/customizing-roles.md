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

## Frontmatter

| Key | Value | Effect |
|---|---|---|
| `name` | the role name | Keep it equal to the filename, which is what the compiler actually reads. |
| `description` | one line | Becomes the skill's and the agent's description — the only text a model weighs before reaching for the role. |
| `emit` | `agent` \| `skill` | `agent` (also the default when the key is absent) adds a spawnable agent that preloads this skill; `skill` is methodology a session applies in its own thread. |
| `read-only` | `true` | The compiled agent assesses and reports; it gets no editing tools. |
| `delegates` | `true` | The role may spawn sub-agents: issue-captain directs a crew and Research sends scouts. Other workers do their own work. |
| `web` | `true` | Grants Claude WebFetch/WebSearch and Codex web_search. |
| `argument-hint` | one quoted line | Claude argument-hint and Codex interface.default_prompt. |
| `invocation` | `automatic` \| `explicit` | `explicit` puts the skill out of every model's reach: only the human, by name. Any other value fails the sync. |

`automatic` buys discovery — the model can fire on the description, and other skills can reach the role —
and costs a description that stays loaded every turn, so write it trigger-first. `explicit` costs no
context and has to be remembered instead, which is why the [dydo Glossary](../reference/dydo-glossary.md) carries the
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
agent can read it. The set is the one dydo ships: a resource name dydo does not ship is never
discovered — sync
emits nothing, and a body link to it compiles into a path that does not exist. A custom role's own
reference therefore goes in a `dydo/` document listed under its Must-Reads, and so does reference that
several roles share, unless it earns a model-invoked method skill of its own.

**Includes** — `{{include:<name>}}` pulls in `dydo/_system/template-additions/<name>.md` at the hook,
which keeps project-specific guidance out of framework text. The
[template pipeline](../understand/templates-and-customization.md) covers the available hooks.

## Model tier

`models.agents` in `dydo.json` binds an agent to a tier and `models.tiers` binds a tier to one concrete
model per vendor, so a role never names a model. A role with no binding compiles `model: inherit` on
Claude — the session's model, never a silent downgrade — and a built-in default model on Codex. See the
[configuration reference](../reference/configuration.md).

DR 047 uses `standard` for implementer, docs-writer, Research and scout; `strong` for reviewer,
specifier, hardener, issue-captain, project-planner and inquisitor. `light` remains defined but
unbound. Effort stays at host defaults. Final consolidation verifies model fallback and native
delegation/permissions after compiler setup; generated configuration alone is not a runtime proof.

## What is gone

- The separate role data file and the commands that maintained it. The template is the role, and the
  frontmatter above is the whole schema.
- Two framework roles from the retired delivery loop, the workflow harness that ran it, and one
  rubric renamed.
  `dydo sync` sweeps their compiled output from both hosts, and `dydo init` never installs them again. The
  [glossary](../reference/dydo-glossary.md)'s retired-terms paragraph carries the words themselves.

Workflow as a delivery concept is retired by DR 047. DYD-92 owns removal of its remaining compiler
emission; use the Inquisition Issue protocol rather than its old harness.

## Related

- [Templates and Customization](../understand/templates-and-customization.md) — the template pipeline end to end
- [Configuration Reference](../reference/configuration.md) — `models.agents`, hashes, nudges
- [dydo Commands Reference](../reference/dydo-commands.md) — `dydo sync`, `dydo template update`
- [dydo Glossary](../reference/dydo-glossary.md) — hat, worker, method, and the retired terms
- [Orientation](../index.md) — the shipped taxonomy and what each role is reached for
