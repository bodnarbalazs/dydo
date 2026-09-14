<!-- Adapted from mattpocock/skills writing-for-agents/SKILL-MECHANICS at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Skill mechanics

The skill-specific branch of writing-for-agents: what changes when the document is a dydo skill —
frontmatter, the invocation choice, and where its reference lives. Everything else about writing it
is the universal reference in this skill's body.

**The template is the skill.** One `skill-<name>.template.md` carries the metadata and the
methodology; `dydo sync` compiles it for every host and owns every host-specific detail. The
`skill-` prefix makes a skill template, any other `*.template.md` is a document or a resource,
`emit: agent` adds a spawnable agent, and `name` is the identity. For where sources live, see
[customizing-roles.md](../../../../dydo/guides/customizing-roles.md).

## Frontmatter

| Key | Value | What the compiler does with it |
|---|---|---|
| `name` | the filename slug | Identity on both hosts. `dydo sync` refuses a template whose `name` is missing or differs from its `skill-<name>` filename. |
| `description` | one line | Becomes the skill's and the agent's description. |
| `emit` | `agent` \| `skill` | `agent` also compiles a spawnable agent that preloads this skill (`skills: [<name>]`) and carries the `Skill` tool; `skill` is methodology a session applies in its own thread. Missing means `agent`. |
| `invocation` | `automatic` \| `explicit` | `explicit` sets `disable-model-invocation: true` on Claude and `allow_implicit_invocation: false` in Codex's `agents/openai.yaml`. Missing means `automatic`. |
| `read-only` | `true` | The compiled agent gets no `Edit`/`Write` and Codex's read-only sandbox: it assesses and reports. |
| `delegates` | `true` | Grants the `Agent` tool, so the skill may spawn sub-agents; `research` has it for `scout`, every other worker does its own work. |
| `web` | `true` | Grants Claude's `WebFetch`/`WebSearch` and flips Codex's `web_search` toggle. |
| `argument-hint` | `"<what to type>"` | Shown by the host after the skill's name: Claude's `argument-hint`, Codex's `interface.default_prompt`. |

## Invocation

Two choices, trading the two loads:

- **`automatic`** keeps a description the agent can fire on, and other skills can reach it. You can
  still type its name: model-invocation always _includes_ human reach; a description only ever adds
  agent discovery, never removes the human's. That description is a context pointer forced to stay
  loaded every turn — permanent context load in exchange for discoverability — so write it
  trigger-first, one trigger per branch, with this skill's pointer rules applied in full.
- **`explicit`** keeps the skill out of the agent's reach: only the human typing its name invokes
  it, and no other skill can. Zero context load, but it spends cognitive load — a human has to know
  it exists, which is why the locked dydo glossary carries the taxonomy instead of leaving that to memory.
  Its description turns human-facing: one punchy line, trigger lists stripped.

Pick `automatic` only when the agent must reach the skill on its own, or another skill must. If it
only ever fires by hand, make it `explicit` and pay no context load — except for an `emit: agent`
skill, which stays `automatic` because the agent's `skills:` preload cannot reach an explicit skill.
Split a model-invoked skill off an existing one when it has a distinct leading word that should
trigger it alone — a word you actually use in your prompts — or when another skill must reach it;
that independent reach costs a permanently loaded description, so it has to be worth one.

## Where reference lives

- **`## Must-Reads`** — markdown links to project documents under that heading. They survive into
  the compiled skill body, rewritten to resolve from the folder the skill is emitted into, and into
  a spawned agent's context block as repo-relative paths. Close the list with
  `{{include:extra-must-reads}}` so a project can add its own without editing framework text.
- **Resources** — `<skill>-resource-<name>.template.md` compiles to `resources/<name>.md` beside the
  skill, and the body reaches it by that same path, rewritten to the host's emitted location so even
  a preloaded agent can `Read` it. This is disclosure with a file boundary: one skill's own
  reference, reached only by the branches that need it. Reference several skills share lives
  instead in a model-invoked method skill, or in a `dydo/` document each of them lists under
  Must-Reads.

## Regeneration

`dydo sync` compiles every source. Its cleanup is an allowlist of retirements in `SyncCommand`: a
template you retire joins it, or its compiled output survives in every installed project and its
description loads every turn. The template is the skill; everything under `.claude/`, `.codex/` and
`.agents/` is a build product — fix the template and sync again rather than editing what came out.
What no tool may rewrite is listed in [files-off-limits.md](../../../../dydo/files-off-limits.md).
