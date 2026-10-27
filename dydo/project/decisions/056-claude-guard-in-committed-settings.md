---
area: general
type: decision
status: accepted
date: 2026-10-27
---

# 056 - Claude Guard in Committed Settings

dydo wires the Claude Code guard hook into the committed `.claude/settings.json`, so a fresh clone
is guarded. The allow list stays in the machine-local `.claude/settings.local.json`. Ruled by
Balazs on 2026-10-27 in [DYD-392](https://linear.app/bodnar-balazs/issue/DYD-392), reversing
issue 0303.

## Context

Issue 0303, resolved in dydo 2.2.3 (commit `36c866e8`) without a Decision Record, fixed an
asymmetric policy: "Claude wiring remains machine-local in `.claude/settings.local.json`, which
init/join now gitignores; Codex wiring remains committed in `.codex/hooks.json`." Its reason was
that `settings.local.json` is personal scope by Claude Code convention, so each machine runs
`dydo init claude --join` once per clone. The issue also named the other option: move the Claude
guard wiring into a committed `.claude/settings.json`.

The reason eroded. `--join` already writes the committed `.claude/settings.json` on every machine,
for `env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` and for `autoMemoryEnabled`
([DR 055](./055-native-agent-memory-off-and-guarded.md)), so the Claude wiring was no longer
machine-local in practice. Meanwhile a fresh clone ran Claude unguarded until someone ran `--join`,
while the same clone was Codex-guarded out of the box.

Three options were weighed in DYD-392:

- **A.** Keep the guard local and have `dydo check` warn when it is missing.
- **B.** Move all Claude wiring, guard and allow list, into the committed `.claude/settings.json`.
- **C.** Commit the guard and keep the allow list local; init moves dydo's hook entries out of
  `settings.local.json`.

## Decision

Option C.

1. **The guard is committed.** `dydo init claude` and `--join` write one `hooks.PreToolUse` entry
   running `dydo guard --host claude` into `.claude/settings.json`. It replaces dydo's earlier
   entry in place; any other dydo guard entry and a Stop entry running `dydo guard --stop` are
   removed. Hooks dydo did not write stay as they are.
2. **The allow list stays local.** `.claude/settings.local.json` keeps the `dydo` and
   `dydo clean` allow entries, loses the guard and Stop hooks an older dydo wired there, and stays
   gitignored.
3. **Codex is unchanged.** Its guard stays committed in `.codex/hooks.json`; the two hosts now share
   one policy.

## Consequences

- A fresh clone is guarded on Claude Code from its first session, as it already was on Codex.
- A teammate without a dydo CLI on the path gets a non-blocking hook error on every matched tool
  call, as a Codex teammate already does. The guard then enforces nothing for that teammate.
- The allow entries that let `dydo` run without a prompt stay per machine: a machine gets them
  from `dydo init claude` or `--join`.
- A machine an older dydo wired would run the guard twice if both files carried it, so init
  removes dydo's hooks from `settings.local.json`.

## Supersedes

This reverses the policy recorded in issue 0303 (dydo 2.2.3). No earlier Decision Record held that
policy.

## Related

- [Guard System](../../understand/guard-system.md) - What the hook does once wired
- [CLI Commands](../../reference/dydo-commands.md#dydo-init) - The files init writes
- [DR 055 - Native Agent Memory Off and Guarded](./055-native-agent-memory-off-and-guarded.md)
