# Skills

The 29 canonical dydo skills live one level deeper than they used to, grouped by what kind of work
they do:

- [`orchestration/`](orchestration/README.md) — 8 skills. Running Projects and Issues: captains,
  admirals, reviewers, and the people-facing triage around them.
- [`engineering/`](engineering/README.md) — 10 skills. Writing, diagnosing, and shaping code and its
  design.
- [`productivity/`](productivity/README.md) — 11 skills. Thinking, writing, and working with the
  human day to day.

8 + 10 + 11 = 29, the full canonical set.

`setup-skills.mjs` walks `skills/<category>/<skill>/SKILL.md` but still projects every skill flat
into `.claude/skills/<skill>` and `.agents/skills/<skill>` — the category folders are a source-tree
convenience only; each host still sees 29 flat entries with no category level.

Each skill's frontmatter carries `disable-model-invocation: true` when it is **user-invoked** (the
human names it explicitly); otherwise it is **model-invoked** (an agent may load it on its own
judgment). Each category README lists its skills with their invocation mode and one-line
description, taken from the skill's own frontmatter.
