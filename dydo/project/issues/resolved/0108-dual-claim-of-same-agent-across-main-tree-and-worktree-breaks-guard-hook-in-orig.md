---
id: 108
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-04-26
resolved-date: 2026-05-18
---

# Dual-claim of same agent across main tree and worktree breaks guard hook in original shell

Open high-severity bug: the reviewer auto-selector in a `code-writer-in-worktree → reviewer` dispatch chain can route the reviewer claim back to the originating orchestrator, and the second claim silently overwrites the live session record. The original shell ends up in an asymmetric half-broken state — `dydo whoami` still reports the agent identity, but the PreToolUse guard hook rejects all file reads and writes. Likely root causes: missing dispatch-origin exclusion in reviewer auto-selection, plus the claim path overwriting a non-stale, alive session.

## Description

When a code-writer dispatched into a worktree fulfils its `requires-dispatch`-of-reviewer release constraint by running `dydo dispatch --no-wait --auto-close --role reviewer --task <task> --brief "..."` without an explicit `--agent`, the auto-selector can route the reviewer-claim to the **same human-named agent** that originated the dispatch (i.e. the orchestrator). The dispatched-reviewer process then claims that agent inside the worktree workspace, replacing the live session record. The original orchestrator's shell — still running, same agent name in `dydo whoami` — drifts into a half-broken state where:

- `dydo whoami`, `dydo agent status`, and other read-only `dydo` subcommands keep returning the original Brian identity correctly.
- The `PreToolUse` guard hook (`dydo guard`) responds with `BLOCKED: No agent identity assigned to this process` for **both** the `Read` tool and any bash file-read (`cat`, `head`, etc.).
- `dydo issue resolve`, `dydo issue create`, `dydo agent list`, etc. continue to work because they do not pass through the hook check.
- Dispatching, messaging, and waiting from the original shell continue to work.

The net effect is an orchestrator who can drive the orchestration commands but cannot read or write a single file — an asymmetric breakage where some surfaces reject the identity and others accept it.

In the observed session the agent name was Brian, the orchestrator was on `co-think-session`, and the dispatched code-writer Grace ran `dydo dispatch --no-wait --auto-close --role reviewer --task fix-worktree-merge-self-merge --brief "..."` from inside `worktree/fix-worktree-merge-self-merge`. The reviewer dispatch landed in Brian's inbox (origin: Brian); shortly after, the original Brian shell's `Read`/`cat` operations began failing with the no-identity error. The active session record now showed session ID `941f1399-145d-4804-8787-33e9d724a719`, claimed at `2026-04-24T20:50:50Z` — matching the worktree-side dispatch timestamp, not the original Brian-claim from 2 hours earlier.

`dydo agent claim Brian` from the original shell errored with `Agent Brian is already claimed by another session`, confirming the worktree-side process held the live claim.

## Reproduction

1. As Brian (orchestrator, main tree), dispatch a code-writer into a worktree:
   `dydo dispatch --no-wait --auto-close --worktree --role code-writer --task <task> --brief-file <path>`
2. Wait for the code-writer to finish their work and reach release. Their mode (per `code-writer.role.json`'s `requires-dispatch` constraint) requires they dispatch a reviewer for `<task>` before release. Allow the standard `dydo dispatch --no-wait --auto-close --role reviewer --task <task> --brief "..."` (no `--agent`) to run.
3. Observe: the auto-selector picks Brian as the reviewer target (likely because Brian was the dispatch-origin for `<task>` and other agents are filtered out by reviewer constraints / load).
4. Inside the worktree, the reviewer-claim writes a new session record for Brian, replacing the orchestrator's live session.
5. In the original orchestrator shell, attempt:
   - `Read <any file>` → BLOCKED, "No agent identity assigned to this process"
   - `cat <any file>` → BLOCKED, same error
   - `dydo whoami` → still reports Brian, role orchestrator, task co-think-session
   - `dydo agent status` → also reports Brian, but `Session ID` and `Claimed` timestamp are the worktree-shell's, not the original
6. Attempt `dydo agent claim Brian` from the original shell: errors with "already claimed by another session".

## Likely root cause

Two compounding issues:

1. **Auto-selector for `dispatch --role reviewer`** does not exclude the originating orchestrator. When the dispatch chain is `orchestrator → code-writer → reviewer`, the reviewer auto-selection is allowed to land back on the originating orchestrator. Conceptually this mirrors the "code-writer cannot review own work on same task" constraint: an orchestrator who chose the code-writer (and likely owns the merge decision) should not be the reviewer of that code-writer's output. Either add a `dispatch-restriction` constraint excluding the dispatch-origin from reviewer auto-selection, or surface a more deliberate error so the code-writer must pick someone else explicitly.

2. **Agent claim across sessions silently overwrites the live session record** rather than detecting the conflict. The reclaim path added in resolved issue #0103 (Services/AgentRegistry.cs `HandleExistingSession` :308-317) is gated on `IsStaleWorking` (>5 min) and `!IsSessionPidAlive` — but in this case the original Brian process **was alive** and **was not stale**. The new claim should not have succeeded, or should have surfaced a conflict to the dispatcher, or at minimum should have left the original session readable by its hook checks. Today the new claim wins and the original session ends up in an asymmetric state — a session ID still exists, the agent state file still says working, but the hook's PID-based identity lookup no longer resolves it (the session ID belongs to the worktree-side process now).

It is also worth investigating whether the worktree's separate workspace junction — recall that `dydo/agents/` is junction-shared across worktrees per `architecture.md#worktree-dispatch` — means *both* shells write to the same `Brian/state.md`, and the second writer simply wins. If so, the locking story for the shared `state.md` across worktrees is the underlying issue rather than the dispatch flow specifically.

## Suggested fix

Pick at least one of:

1. **Exclude dispatch-origin from reviewer auto-selection.** Add a constraint to `reviewer.role.json` (or to the auto-selector itself) that filters out the originating orchestrator/dispatcher when the reviewer is dispatched as part of a `code-writer` release flow.
2. **Detect concurrent-session conflict on claim.** When a claim arrives for an agent whose session is already alive and not stale, refuse the claim (the reclaim path should remain stale-only as designed by #0103). Currently the new claim appears to win silently.
3. **Make the guard hook robust to mid-session re-claims.** If a reclaim does happen, the hook should still recognize the original PID's identity until that process explicitly releases — or it should fail with a clearer message ("This shell's claim was superseded by ...") rather than the misleading "No agent identity assigned to this process" which suggests the user never claimed at all.
4. **Document the cross-worktree `dydo/agents/` shared state** as a known constraint in `architecture.md#worktree-dispatch` — agents should never legitimately claim the same name from two worktrees simultaneously.

Add a regression test that simulates `orchestrator → code-writer-in-worktree → reviewer-auto-dispatch-back-to-orchestrator` and asserts: (a) reviewer is *not* the orchestrator, OR (b) the orchestrator's hook lookup keeps resolving identity until release.

## Impact

- Observed in this session (2026-04-24/26) on agent Brian during issue-housekeeping orchestration. The orchestrator could continue to drive `dydo` subcommands but could not read any file, write the housekeeping log to its own workspace, or fill in the body of this very issue without first taking the workaround path below.
- Workaround used: original shell `dydo agent claim auto` (claimed Adele), `dydo inbox clear --all`, `dydo agent release` (released the orphan Brian session), `dydo agent release` (released Adele), `dydo agent claim Brian`, then `dydo agent role orchestrator --task co-think-session`. After that, hook checks resumed working. None of this is documented or discoverable from the error message.
- Risk: any orchestrator that dispatches a code-writer in a worktree is exposed to this if the reviewer auto-selector picks them. The dispatch chain in question (orchestrator → code-writer → reviewer) is the canonical pattern in `modes/code-writer.md` and `modes/orchestrator.md`, so this is not an edge case.
- Compounding: the workaround above relied on the `dydo guard lift Brian 10` command granted by the human, plus knowledge of the `claim auto`/`release`/`reclaim` sequence. An agent without the lift and without that procedural knowledge would be stuck.
- **Secondary blast-radius observed in this session:** the worktree containing the dispatched code-writer's uncommitted work was emptied during the dual-claim cleanup chain (worktree directory at `dydo/_system/.local/worktrees/fix-worktree-merge-self-merge/` is now empty; branch reflog shows zero commits; `git fsck` has no recoverable dangling commits from the affected session). The dispatched code-writer's uncommitted #0107 fix was destroyed. This may be a separate cleanup-on-double-release bug (filed separately) but is exposed here because the dual-claim's release path probably ran `dydo worktree cleanup` twice — once for each claim of Brian.

## Related context

- `dydo/_system/roles/reviewer.role.json` — current reviewer constraints (`fromRole=code-writer` only, no dispatch-origin exclusion).
- `dydo/_system/roles/code-writer.role.json` — `requires-dispatch` constraint forces the auto-selector path.
- `Services/AgentRegistry.cs:308-317` — `HandleExistingSession`, the reclaim path from #0103. Investigate why a non-stale, alive original session can be superseded.
- `dydo/understand/architecture.md#worktree-dispatch` — `dydo/agents/` is junction-shared across worktrees; this is the surface where two shells can race on `Brian/state.md`.
- The dispatch that triggered this: code-writer Grace → reviewer dispatch for task `fix-worktree-merge-self-merge` (worktree branch `worktree/fix-worktree-merge-self-merge`), 2026-04-24T22:50:50Z.
- `dydo/agents/Brian/inbox/c696a046-fix-worktree-merge-self-merge.md` — the misrouted reviewer brief that landed in the orchestrator's inbox.

## Second observation (2026-05-18, Frank via Adele)

Independently reproduced by Frank in the LC project's auth-marathon-1.0 session — a different trigger surface for the same underlying bug. Orchestrator Brian fired five `dydo dispatch --wait --auto-close --new-window` calls in succession. By the fifth (`email-service-investigation`), the other free agents (Adele, Charlie, Dexter, Emma) had all just been reserved by the prior dispatches, so the auto-selector's pool was effectively exhausted — and Brian himself became the only "free-looking" candidate. The new terminal claimed Brian, the original orchestrator's session was orphaned, `dydo whoami` returned no identity, and re-claim failed with "already claimed by another session."

This confirms the suspected root cause from suggestion 1 and points the fix precisely at `Services/AgentSelector.cs`:

- **`TryReserveFromPool` (line 66-89)** does not exclude the sender. Its sibling **`TryReserveOrigin` (line 50)** already has the matching guard (`origin == senderName` → null) — the pool path is just missing the symmetric filter. `senderName` is already in scope.
- **`SelectExplicit` (line 9-26)** also accepts `--to <self>`; a user-passed self-dispatch hijacks the same way.

Mechanism in this variant: Brian was stale-working (Since > 5 min into the marathon), and `IsSessionPidAlive(Brian)` returned false — likely a watchdog auto-resume episode had left `.session.ClaimedPid` pointing at the dead pre-resume Claude (see `AgentRegistry.cs:342-346` comment #0143). With both conditions met, `IsReservable` returned true → `ReserveAgent` flipped Brian to `Dispatched` → new terminal's `ClaimAgent` short-circuited on `Status == Dispatched` (`AgentRegistry.cs:337`) → identity transferred. Process A's session id matches no `.session` file afterward, so `GetCurrentAgent` returns null.

Frank's full bug report: `(external) LC/dydo/agents/Frank/bug-report-self-dispatch-identity-hijack.md`.

## Resolution

AgentSelector.SelectAutomatic.TryReserveFromPool now filters senderName from the eligible pool, and SelectExplicit rejects to==senderName. Closes both observed triggers (worktree reviewer auto-dispatch + pool-exhaustion sequential dispatches). Regression tests added: SelectAutomatic_DoesNotPickSenderFromPool, SelectExplicit_RejectsSelfDispatch.
