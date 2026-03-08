---
type: decision
status: proposed
date: 2026-03-08
area: messaging
---

# 003 — Agent Messaging

## Problem

Agents have no way to communicate after dispatch. `dydo dispatch` is the only inbox mechanism, and it's heavy — role assignment, agent reservation, terminal launch. When Agent A dispatches work to Agent B, B completes and releases with no way to report results back. Agents frequently assume dispatched agents can respond. They can't.

## Decisions

### Messages reuse the inbox directory

**Rejected:** Separate `messages/` directory per agent. Would require a new directory to manage, new guard rules, and the release-blocking logic (which checks inbox) wouldn't cover messages without duplication.

**Chosen:** Messages go into the existing `inbox/` directory alongside dispatch items. A `type: message` frontmatter field distinguishes them. Existing dispatch items have no `type` field — treated as `type: dispatch` for backward compatibility. This reuses all existing infrastructure: inbox show/clear, release blocking, archive on clear.

### Guard notification via blocking (not advisory)

**Rejected:** Print a warning alongside a successful (exit 0) tool call. The agent would see it but could easily ignore it — especially if the tool output is long. Claude processes stderr and stdout but doesn't reliably act on warnings it didn't ask for.

**Chosen:** Block the tool call (exit 2) with a notification message. The agent is forced to acknowledge the message because it can't proceed. The notification explicitly tells the agent their tool call was valid but paused, and to retry after reading. This reuses the `UnreadMustReads` pattern — an `UnreadMessages` list in agent state, cleared when the message file is read via the Read tool. Dydo bash commands are exempt from the block so the agent can run `inbox show` and `inbox clear`.

**Why not just block once?** A one-shot "block then never again" approach was considered. Problem: if the block fires on a Glob or Grep, the agent might not have enough context to understand what was blocked. By blocking until the message is actually read, we guarantee the agent processes it. The agent reads the file, the guard detects the read, clears that message ID, and subsequent tool calls succeed.

### `dydo wait` has no timeout

**Rejected:** A `--timeout` flag. Creates complexity: what exit code on timeout? What should the agent do? Agents would need conditional logic around timeout vs success, making the workflow templates harder to write.

**Chosen:** No timeout. The command polls every 10 seconds indefinitely. When Claude Code's bash timeout kills it (default 120s, configurable to 600s), `dydo wait` catches the signal and prints "No message received yet. Run 'dydo wait' again to continue waiting." The agent re-runs the command. While `dydo wait` is running, the agent consumes no API tokens — it's blocked on a bash tool call. This is the key insight that makes it practical.

### Messaging to inactive agents requires `--force`

**Rejected:** Silently allow messaging inactive agents. The message would sit in inbox until the agent is eventually claimed, but the sender would have no signal that the recipient won't see it anytime soon.

**Rejected:** Block entirely. There are valid use cases — leaving a message for a future dispatch.

**Chosen:** Error by default with a clear message suggesting `--force`. The inbox survives claim/release cycles (inbox is in `SystemManagedEntries`, excluded from workspace archiving), so the message will be there when the agent eventually claims. But no `UnreadMessages` state update occurs for inactive agents — the workflow handles it via normal `--inbox` processing on claim.

### `message` is canonical, `msg` is alias

Consistent with the verbose style of other commands (`dispatch`, `agent`, `inbox`). `msg` is the natural shorthand agents will use in practice. Having both means the docs use the readable form while agents can type the short form.

### Messaging is opt-in, not default workflow

Most dispatch flows don't need a response. The workflow templates mention messaging as an option ("Need results back?"), not a requirement. Only planners dispatching complex subtasks benefit from the wait-and-respond pattern. Making it default would add cognitive load and extra steps to every agent session.

## Future: LLM Council

Messaging enables multi-agent deliberation as emergent behavior. Multiple agents could explore a topic, share insights via messages, and consider each other's conclusions. This is primarily a prompt engineering challenge, not a tooling one. Could add `dydo vote` for formal consensus. Defer to a future iteration once the basic messaging primitive proves stable.
