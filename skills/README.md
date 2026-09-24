# Skills

The 29 canonical dydo skills, sorted by kind:

- [`roles/`](roles/README.md) — 9 skills. The skills that carry an identity: `roles/officers/`
  hold something and never switch (admiral, issue-captain, chief-of-staff); `roles/crew/` are
  spawned for one bounded job and return their result to whoever sent them.
- [`engineering/`](engineering/README.md) — 6 skills. The craft: diagnosing, shaping and designing
  code.
- [`productivity/`](productivity/README.md) — 14 skills. Thinking, writing, and moving work along
  with the human.

9 + 6 + 14 = 29, the full canonical set.

`setup-skills.mjs` walks the tree by rule, not by depth: a folder holding `SKILL.md` is a skill, and
any other folder under `skills/` is a category to walk into. It still projects every skill flat into
`.claude/skills/<skill>` and `.agents/skills/<skill>` — the category folders are a source-tree
convenience only; each host still sees 29 flat entries with no category level.

Each skill's frontmatter carries `disable-model-invocation: true` when it is **user-invoked** (the
human names it explicitly); otherwise it is **model-invoked** (an agent may load it on its own
judgment). Each category README lists its skills with their invocation mode and one-line
description, taken from the skill's own frontmatter; `roles/README.md` lists officers and crew
under their own headings.
