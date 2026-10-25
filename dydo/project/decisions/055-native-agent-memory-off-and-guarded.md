---
area: general
type: decision
status: proposed
date: 2026-10-25
---

# 055 - Native Agent Memory Off and Guarded

dydo turns the hosts' native agent memory off and the guard denies agent writes to every host
memory folder. Durable knowledge goes to Git and dydo. Proposed in
[DYD-381](https://linear.app/bodnar-balazs/issue/DYD-381).

## Context

dydo's premise is documentation as memory: agents onboard from structured, reviewable project
knowledge ([about.md](../../understand/about.md)). A host memory store is a second memory outside
the repo, unreviewed and per user.

[DR 038](./038-auto-memory-policy.md) kept that store as a buffer, steered by one routing line in
the entry file and drained by sweeps. The line is gone: the skill craftsmanship restoration left the
shared entry prompt with no memory policy
([plan](../plans/dydo-3-skill-craftsmanship-restoration.md)). The guard still lets every memory
write through: `GuardCommand.IsNativeMemoryPath` exempts `~/.claude/projects/*/memory/` and no
other folder.

Two events, 2026-10-24 and 2026-10-25:

- Balazs, on an admiral's memory file: "I would probably prefer if you'd delete those memories
  because the whole reason you're building Dydo is so these idiot things don't happen."
- A Claude session in another project wrote notes to
  `~/.claude-personal2/projects/<project>/memory/`, a profile folder set as `CLAUDE_CONFIG_DIR`.
  Nothing stopped it: the exemption knows only `~/.claude`, the routing line was gone, and file
  nudges see only repo paths.

DR 038's Revisit When named this escalation: memory keeps collecting project facts, so move
enforcement into the guard.

## Decision

1. **Memory off.** `dydo init` and `--join` write `"autoMemoryEnabled": false` into
   `.claude/settings.json`, which also turns off subagent memory, and `memories = false` under
   `[features]` in `.codex/config.toml` (Codex memories run only when that key is `true`).
2. **Guard denies memory writes.** An agent write to a host memory folder is blocked:
   - Claude: `projects/*/memory/` and `agent-memory*/` under `~/.claude`, any `~/.claude*` profile
     folder and `CLAUDE_CONFIG_DIR`; the `autoMemoryDirectory` folder when set; the project's
     `.claude/agent-memory*/`.
   - Codex: `$CODEX_HOME/memories/` (default `~/.codex/memories/`).

   This replaces the exemption in `GuardCommand.IsNativeMemoryPath`.
3. **The limit.** Codex writes memories from a background process the guard never sees, so the
   Codex half rests on the config flag. The guard stops accidents, not rogue agents.

## Consequences

- Officers and admirals lose their own memory notes, which this repo's maintainers rely on today.
  A lesson goes into a skill, a DR or a doc; recurring friction goes through the `retro` skill.
- Migration, per memory folder: route each entry to a skill, DR, doc or ticket, or delete it, then
  empty the folder. Facts about the user go to the project's docs or the user's own global
  `CLAUDE.md` or `AGENTS.md`, files the user owns.
- `update-dydo`'s What to compare list reports each memory switch and folder until this lands.

## Supersedes

On acceptance this amends [DR 038](./038-auto-memory-policy.md): memory stops being a buffer,
and its routing line and sweep give way to the config switches and the guard. DR 038 is not edited
while this record is proposed.

## Related

- [DR 038 - Auto-Memory Policy](./038-auto-memory-policy.md)
- [About This Project](../../understand/about.md)
