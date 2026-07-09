---
title: c1-1 Host-Agnostic dydo read Verb
blocked-by:
due:
needs-human: false
priority: High
sprint: c1-codex-adoption
status: ready
work-type: feature
area: backend
type: context
---

# c1-1 Host-Agnostic `dydo read` Verb

The lead 0254 fix: codex hosts read files via shell, so the guard's Read-tool-observed tracking
never registers — unread state persists, `dydo inbox clear --all` blocks ("read them first",
InboxService.cs:100-102), and release wedges. Design fixed in co-think (balazs 2026-07-09):
**one host-agnostic verb that PRINTS the content and registers the read** — display-equals-ack,
no blind acking. Same ceremony on every host; Claude's hook-observed tracking stays and this is
additive.

## Behavior

`dydo read <target>`
1. `<target>` is an inbox message id → print the message (reuse `inbox show`'s single-item
   rendering) and mark it read (`AgentRegistry.MarkMessageRead`).
2. `<target>` is a file path → print the file content and, when it matches an unread must-read,
   mark it complete (`MarkMustReadComplete`) — same path-matching the guard uses.
3. Unknown target → actionable error naming both accepted forms; nothing marked.
4. Requires a claimed identity (resolve the caller like sibling commands do); never registers
   reads for a different agent.

## Implementation notes

- **Extract, don't duplicate:** `GuardCommand.TrackReadCompletion` (GuardCommand.cs:646-666)
  already does must-read + inbox matching for observed Reads. Extract that logic into a new
  `Services/ReadTrackingService.cs` consumed by BOTH the guard call site and `ReadCommand`. The
  guard's behavior must be byte-identical after extraction.
- The invariant: content emission and read registration happen in one code path — no flag or
  internal entry point that registers without printing.

## Files

- `Commands/ReadCommand.cs` — NEW, factory pattern like siblings.
- `Services/ReadTrackingService.cs` — NEW (extraction).
- `Commands/GuardCommand.cs` — TrackReadCompletion delegates to the service (extraction only; no
  behavior change; this file is then handed to c1-2).
- `Services/InboxService.cs` — expose single-item rendering for reuse.
- `Services/MustReadTracker.cs`, `Services/AgentRegistry.cs` — expose the matching/marking seams
  the service needs (keep public surface minimal).
- `Program.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs` — register the verb.
- Tests: NEW `DynaDocs.Tests/Commands/ReadCommandTests.cs` (message-id path, file path,
  must-read completion, unknown target, no-blind-ack invariant, identity required);
  `DynaDocs.Tests/Commands/CommandSmokeTests.cs` factory array; guard read-tracking regression
  (existing Guard tests stay green — extraction is invisible).
- Doc surfaces per the 6-surface rule (`dydo/guides/adding-a-command.md`): help text, smoke
  factory, `dydo/reference/dydo-commands.md` + template, `dydo/reference/about-dynadocs.md` +
  template. Plus `dydo/_system/templates/agent-workflow.template.md` codex-onboarding prose:
  claim is a manual step; shell-based hosts register reads via `dydo read`. (Template has
  uncommitted working-tree edits — rebase on current content; flag mid-flight-looking edits to
  Adele.)

## Gates (exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py` — green, `CommandDocConsistencyTests` included.
- `DynaDocs.Tests/coverage/gap_check.py --force-run`
- `dydo check`

## Sequencing & ripple

- Parallel-safe with c1-3/c1-4/c1-5. **Blocks c1-2** (GuardCommand.cs + wait/read doc surfaces)
  — see the sprint record's dependency graph.
- `CompletionProvider.cs` is m0-4's file too — moot under C1-first sprint ordering.
- README-family doc-test ripple → report to Adele, don't absorb.

## Success criteria

A codex-hosted agent (or any shell) can `dydo read` its inbox items and must-reads, then
`dydo inbox clear --all` succeeds — the release wedge from the 2026-07-09 smoke is gone. Issue
0254 items (1) and (2) resolved (issue closes jointly with c1-2). Suite green.
