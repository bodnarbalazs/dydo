---
area: guides
type: guide
---

# Troubleshooting

Common errors, guard blocks, and recovery patterns.

---

## Guard blocks

### "No agent identity assigned"

```
BLOCKED: No agent identity assigned to this process. Run 'dydo agent claim auto'...
```

**Cause:** You tried to read or write a file without claiming an agent identity first.

**Fix:**

```bash
dydo agent claim auto         # Claim first available agent
dydo agent claim Adele        # Or claim a specific agent
```

### "Read access denied"

```
BLOCKED: Read access denied...
```

**Cause:** Staged access control. At stage 0 (no identity), you can only read bootstrap files. At stage 1 (claimed, no role), you can read your mode files. Full read access requires stage 2 (role set).

**Fix:** Follow the onboarding steps in order — claim identity, then set your role:

```bash
dydo agent claim auto
dydo agent role code-writer --task my-task
```

### "Cannot edit path"

```
Agent Emma (docs-writer) cannot edit src/AuthService.cs.
```

**Cause:** Your current role doesn't have write permissions for that file path.

**Fix:** Check your permissions and switch roles if needed:

```bash
dydo agent status              # See current role and writable paths
dydo agent role code-writer --task my-task   # Switch to appropriate role
```

### "DYDO_HUMAN not set"

```
BLOCKED: DYDO_HUMAN not set.
```

**Cause:** The `DYDO_HUMAN` environment variable is missing.

**Fix:** Set it in your shell profile:

```bash
# Bash/Zsh
export DYDO_HUMAN="your_name"

# PowerShell
$env:DYDO_HUMAN = "your_name"
```

Then restart your terminal session.

### "Already claimed"

```
Already claimed.
```

**Cause:** Another terminal session has already claimed this agent.

**Fix:**

```bash
dydo agent claim auto          # Claim a different available agent
dydo agent list --free         # See which agents are free
```

### "Must-read files not read"

```
BLOCKED: You have not read the required files for the docs-writer mode: - about.md - writing-docs.md
```

**Cause:** Your role's mode file links to files with `must-read: true` in their frontmatter. You must read them before performing writes.

**Fix:** Read the listed files, then retry your operation.

---

## Stuck states

### Agent won't release

```
Cannot release: 2 unprocessed inbox item(s).
Cannot release: waiting for response on: auth-login.
Cannot release: pending reply on: 'my-task' to Adele.
```

**Causes and fixes:**

| Blocker | Fix |
|---------|-----|
| Unprocessed inbox | Read the items, then `dydo inbox clear --all` |
| Active wait markers | `dydo wait --task <name> --cancel` or `dydo wait --cancel` |
| Pending reply | `dydo msg --to <agent> --subject <task> --body "Done."` |
| Review not dispatched | `dydo dispatch --no-wait --auto-close --role reviewer --task <task> --brief "..."` |

### Dispatch fails

```
Brian is already working on task 'auth-login'.
```

**Cause:** Double-dispatch protection — another agent is already working on that task.

**Fix:** Either wait for the other agent to finish, or have them release first:

```bash
dydo agent status Brian        # Check their status
```

### Wait never resolves

**Cause:** The `dydo wait` command polls every 10 seconds with no timeout. If the dispatched agent never sends a message back, the wait blocks indefinitely.

**Fix:**

```bash
dydo wait --task my-task --cancel    # Cancel the specific wait
dydo wait --cancel                   # Cancel all active waits
```

Then investigate why the dispatched agent didn't respond.

### Agent stuck in `status: working` after its tab closed

**Symptom:** `dydo agent list` shows an agent as `working`, but no live terminal for it exists. Its `state.md` still has `status: working`, task populated, writable/readonly paths populated. The workspace still contains `.session` and `modes/`.

**Cause:** The agent's Claude process ended (context exhaustion, user closed the tab, crash) before `dydo agent release` ran to completion. `ReleaseAgent` is the only code path that transitions Working → Free — nothing else (not the watchdog, not dispatch, not guard) touches that transition. When the session dies mid-work, state.md legitimately reflects "was working when last alive."

Common triggers:

- Context limit reached mid-task.
- `dispatched code-writers must dispatch a reviewer before releasing` error hit at end-of-work, not resolved before tab closed.
- Tab was closed before the agent finished.
- `taskkill /im dydo.exe /f` during an in-flight `dydo agent release` (rare — leaves a `.claim.lock` in the workspace).

**Verify release really didn't run** (before force-cleaning):

```bash
grep -l '"Claim","agent":"<Name>"' dydo/_system/audit/2026/*.events    # find sessions
grep '"Release"' dydo/_system/audit/2026/<session-id>.events           # confirm absence
```

If the agent was running in a **worktree**, audit lives under the worktree's own `dydo/_system/audit/` — only `dydo/agents`, `dydo/_system/roles`, `dydo/project/issues`, `dydo/project/inquisitions` are junctioned across worktrees. Check `dydo/_system/.local/worktrees/<id>/dydo/_system/audit/2026/` too.

**Recovery options, least destructive first:**

1. **Resume the tab.** If the agent's tab is still open, run `claude --resume` in it. The Claude process reconnects, you can finish the work and release normally. This preserves task-role-history, inbox, and uncommitted code edits.

2. **Force-clean the workspace.** Destructive — wipes inbox, modes, state.md:

    ```bash
    dydo agent clean <name> --force
    ```

    Code-writers' uncommitted source edits survive (they live outside the agent workspace), but anything inside `dydo/agents/<name>/` is gone. Rescue drafts (`msg-*.md`, notes) with `cp` first.

3. **Worktree zombies need worktree teardown first.** If the agent has `.worktree*` markers in its workspace, the branch and worktree dir persist even after `agent clean`:

    ```bash
    git worktree remove dydo/_system/.local/worktrees/<id> --force
    git branch -D worktree/<id>
    dydo worktree prune
    dydo agent clean <name> --force
    ```

See [decision 018](../project/decisions/018-zombie-working-state-recovery.md) for the mechanism and the planned reclaim-on-claim fix (issue #103).

### Watchdog is dead, tabs don't auto-close

**Symptom:** Released agents with `auto-close: true` still have their terminals open after release. `dydo/_system/.local/watchdog.pid` is stale or missing.

**Cause:** The watchdog is only (re)started by `dydo dispatch` when `--auto-close` is set. Release does not revive it. So if `taskkill /im dydo.exe /f` (or a crash) kills the watchdog, it stays dead until the next auto-close dispatch.

**Fix:**

```bash
dydo watchdog start              # idempotent — no-ops if already running
```

The next poll (every 10 seconds) will find hung tabs via process-pattern match (`<Agent> --inbox`) and kill them. `window-id: null` in state.md is expected for root dispatches (MRU window targeting) and does NOT prevent auto-close — the kill uses command-line pattern matching.

Planned fix: `dydo agent release` should best-effort revive the watchdog (issue #102).

---

## Recovery commands

When you're lost or something is wrong, start here:

```bash
dydo whoami              # Who am I? What's my role? What's my task?
dydo agent status        # What are my permissions? What's blocking me?
dydo inbox show          # Do I have unread messages?
```

### Reset options

```bash
dydo wait --cancel               # Clear all stuck waits
dydo inbox clear --all           # Archive all inbox items
dydo clean Adele                 # Clean an agent's workspace
dydo clean --all --force         # Nuclear option: clean all workspaces
# For zombie working agents specifically, see "Agent stuck in status: working after its tab closed" above.
```

---

## Validation errors

### Running dydo check

```bash
dydo check                       # Check all docs
dydo check dydo/guides/          # Check specific folder
```

Common violations:

| Error | Meaning | Fix |
|-------|---------|-----|
| Missing frontmatter | File lacks `area` or `type` fields | Add YAML frontmatter block |
| Bad naming | File not in kebab-case | Rename to `kebab-case.md` |
| Broken link | Relative link points to non-existent file | Fix the path or remove the link |
| Missing hub file | Folder lacks `_index.md` | Run `dydo fix` to generate it |
| Orphan document | File not linked from anywhere | Add a link from a parent or hub file |
| Missing summary | No paragraph after the title | Add a 1-3 sentence summary |

### Auto-fixing

```bash
dydo fix                         # Auto-fix what's possible
```

Auto-fixes: kebab-case renaming, wikilink conversion, missing hub files, missing folder meta files.

---

## Platform issues

### Windows terminal launch errors

If `dydo dispatch` fails to open a new terminal window, check that your terminal emulator is supported. The dispatch command attempts to spawn a new window or tab for the dispatched agent.

### Bash command blocks

```
BLOCKED: Don't chain cd with other commands — it breaks auto-approval.
```

**Fix:** Run `cd` and your command separately, or use absolute paths.

```
BLOCKED: 'dydo wait' must run in background.
```

**Fix:** Run `dydo wait` with `run_in_background: true` (or the equivalent in your tool).

---

## Related

- [Guard System](../understand/guard-system.md) — How the guard hook works end-to-end
- [Agent Lifecycle](../understand/agent-lifecycle.md) — Claim, role, work, release
- [CLI Commands Reference](../reference/dydo-commands.md) — Full command documentation
