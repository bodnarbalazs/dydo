<!-- Adapted from mattpocock/skills writing-for-agents/SKILL-MECHANICS at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Skill mechanics

The skill-specific branch of writing-for-agents: what changes when the document is a dydo skill —
frontmatter, invocation, and where its reference lives. Everything else about writing it is the
universal reference in this skill's body.

**The skill folder is the skill.** dydo authors every role directly in the cross-vendor `SKILL.md`
format; there is no template and no compile step. The canonical artifact is
`.claude/skills/<name>/SKILL.md` and its committed Codex copy is `.agents/skills/<name>/SKILL.md`,
per the [agentskills.io](https://agentskills.io) standard: `name` and `description` in the
frontmatter and the methodology in the body. Both are hand-maintained.

## Frontmatter

| Key | Value | What the host does with it |
|---|---|---|
| `name` | the folder slug | Identity on both hosts; keep it equal to the folder name. |
| `description` | one line | The only text a model weighs before reaching for the skill. |
| `disable-model-invocation` | `true` | Claude-only: the skill is out of every model's reach; only the human, by name. Codex's twin is `allow_implicit_invocation: false` under the skill's `agents/openai.yaml`. |
| `argument-hint` | `"<what to type>"` | Claude-only: the prompt the host shows after the name. Codex's twin is `interface.default_prompt` under the skill's `agents/openai.yaml`. |

The historical template keys — `emit`, `read-only`, `delegates`, `invocation`, `web` — described a
compiled agent and are retired with the compiler. A role is now a skill; host sandbox and permission
settings, not a generated agent file, keep a read-only reviewer from writing.

## Invocation

Two choices, trading the two loads:

- **automatic** (the default) keeps a description the agent can fire on, and other skills can reach
  it. You can still type its name: model-invocation always _includes_ human reach. That description
  is a context pointer forced to stay loaded every turn — write it trigger-first, one trigger per
  branch, with this skill's pointer rules applied in full.
- **explicit** keeps the skill out of every model's reach: only the human typing its name invokes
  it, and no other skill can. Zero context load, but it spends cognitive load — a human has to know
  it exists, which is why the locked dydo glossary carries the taxonomy. Its description turns
  human-facing: one punchy line, trigger lists stripped. On Claude this is
  `disable-model-invocation: true` in `SKILL.md`; on Codex it is `allow_implicit_invocation: false`
  in `.agents/skills/<name>/agents/openai.yaml`.

## Where reference lives

- **`## Must-Reads`** — markdown links to project documents under that heading. Author each target
  as the document's path under `dydo/`, behind a `../../../` climb that resolves from the skill
  folder on both hosts (`../../../dydo/understand/architecture.md`). Project additions are edits to
  the skill body itself; include tags are retired.
- **Resources** — `resources/<name>.md` beside the skill, reached by that same folder-relative path.
  This is disclosure with a file boundary: one skill's own reference, reached only by the branches
  that need it. Reference several skills share lives instead in a model-invoked method skill, or in
  a `dydo/` document each of them lists under Must-Reads.

## Distribution

One canonical skill folder per host is committed and hand-maintained; DR 049 chose committed copies
over symlinks because a checkout with symlinks disabled materialises a link as a text file and
strands the host. A skill change edits both `.claude/skills/<name>/` and `.agents/skills/<name>/`.
What no tool may rewrite is listed in [files-off-limits.md](../../../../dydo/files-off-limits.md).
