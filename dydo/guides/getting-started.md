---
area: guides
type: guide
---

# Getting Started

The framework setup checklist: how an agent, or a human, builds dydo into a project from install to
the first claimable Issue, Linear workspace and host configuration included. Follow it top to
bottom. Every step says how to tell it is already done, so it also serves a project that has dydo
half set up.

## Prerequisites

- a Git repository for durable knowledge and reviewable proof;
- Claude Code or Codex, or both;
- a Linear workspace with one team for the project's work.

## 1. Install or update dydo

```bash
npm install -g dydo
# or
dotnet tool install -g dydo
dydo version
```

Done when `dydo version` prints; step 2's template update reports whether the tree is behind it. An
older install is updated the same way.

## 2. Initialize a fresh tree, or update an existing one

Fresh tree:

```bash
dydo init codex       # or: dydo init claude / dydo init all / dydo init none
```

Every mode creates the documentation tree and `CLAUDE.md`. The `claude`, `codex` and `all` modes
wire the guard hooks for the chosen runtimes, and Codex selections add `AGENTS.md`. Initialization
creates durable Decisions, changelog, pitfalls and FutureFeature documentation; live work stays in
Linear.

Existing tree, which has `dydo.json` at its root: do not run `init` again. Refresh the
framework-owned documents instead, and show the human the diff before anything is replaced:

```bash
dydo template update --diff
dydo template update
```

A copy the report calls user-edited is compared against the shipped template. If it is only older,
delete the copy and run the update again; if it carries the project's own edits, keep them and move
on. Another machine or runtime joining a project already initialized runs `dydo init codex --join`
or `dydo init claude --join`, which wires the local runtime without touching the documentation tree.

## 3. Compile the skills

```bash
dydo sync
```

Compiles the role templates and resources into the native skills and agents of every wired runtime.
Run it again after any template change. Done when `.claude/skills/` or `.agents/skills/` carries
every shipped role.

## 4. Connect Linear

Agents reach Linear through its official MCP server, which writes as the human who authorized it.
Check first: list the team's Issues over the MCP. If that answers, go to step 5.

Claude Code:

```bash
claude mcp add --transport http linear-server https://mcp.linear.app/mcp
```

then `/mcp` in a session to authorize.

Codex:

```bash
codex mcp add linear --url https://mcp.linear.app/mcp
```

which prompts the Linear login. A first-time Codex install needs `experimental_use_rmcp_client = true`
under `[features]` in `~/.codex/config.toml`.

## 5. Bring the workspace to the standard

The [Linear Workspace Standard](../reference/linear-workspace-standard.md) is the target. Compare
the team against it over the MCP and fix the differences. The MCP creates labels; statuses, project
statuses and templates are the human's clicks, so list exactly what is missing, with the standard's
names and order for statuses and names and colours for labels, and walk the human through it.

1. **Issue statuses.** The twelve, in the standard's categories and order. Linear draws the order as
   progress, so `Ready to Merge` is the last of the started ones.
2. **Project statuses.** `Backlog`, `Planning`, `Planned`, `In Progress`, `Completed`, `Canceled`.
3. **Labels.** The `Type` group with its ten labels and the `Mode` group with `AFK` and `HITL`,
   colours as listed. Retire what the standard does not name.
4. **Issue templates.** One per Type, its body from the standard.
5. **Priority.** Nothing to create; the standard's guide says how the map holder uses it.

Done when the MCP read-back of statuses, labels and templates matches the standard's tables, and
the workspace's project settings list the six Project statuses, which no MCP tool reads.

## 6. Fill in the project's context

- `dydo/understand/about.md`: purpose and domain;
- `dydo/understand/architecture.md`: components and boundaries;
- `dydo/guides/coding-standards.md`: repository conventions;
- `dydo.json`: the model tiers per vendor and the agent bindings, the nudges, the scan exclusions;
  see [Configuration](../reference/configuration.md).

Done when `dydo check` no longer warns about uncustomized foundation documents.

## 7. Host configuration

The crew tree needs three layers below a session: admiral, issue-captain, worker, scout.

- Claude Code: `.claude/settings.json` gains `"env": { "CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH": "3" }`.
- Codex: the project's `.codex/config.toml` gains `[agents]` with `max_depth = 3`.

`dydo init` writes both once DYD-86 lands; until then, by hand. Done when both files carry the key.
Claude Code's default is already 3, so today only the Codex file changes behaviour.

## 8. Check

```bash
dydo check
dydo fix
```

Both clean. Then one live check: assign an existing Issue to yourself over the MCP and unassign it,
and read the team's statuses back. That proves the claim, the write path and the status set without
touching a status, which only a captain sets.

## Run work through Linear

The operating model is in [Control Flow](../understand/control-flow.md): an idea goes to a
co-thinker, a ripe one becomes a Project through `to-project`, an admiral reads the Project and acts,
captains own Issues, every merge is a Merge Issue, and the human acts at the gates. The
[Working-Tree Contract](./working-tree-contract.md) says which branch and worktree each of them works
in.

## Related

- [DynaDocs](../reference/about-dynadocs.md)
- [Linear Workspace Standard](../reference/linear-workspace-standard.md)
- [Configuration](../reference/configuration.md)
- [Customizing Roles](./customizing-roles.md)
- [Control Flow](../understand/control-flow.md)
- [Work Model](../understand/work-model.md)
