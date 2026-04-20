---
type: decision
status: accepted
date: 2026-04-18
area: platform
---

# 018 — Zombie Working State: Mechanism, Fixes, and Doc Surface

## Context

Four agents (Adele, Charlie, Dexter, Emma) were observed on 2026-04-18 with
`status: working` in their `state.md` after their tabs had closed. One of
them (Emma) had been seen by the user completing a successful release in
her terminal output. The symptom is visually alarming — the system looks
like it "forgot" to update state.md after a release — but the mechanism is
a workflow gap, not a state-write bug.

This decision captures:
- Why `status: working` can persist after a tab closes (so we stop
  re-discovering this from first principles each time).
- What we're fixing now.
- What we're documenting so the next investigator has footing.
- How this relates to [017 — Stale-Free Semantics](./017-stale-free-semantics.md),
  which is the adjacent stale-dispatch story.

## Decision

**`ReleaseAgent` is the only code path that transitions `Working → Free`.
If a session ends before `dydo agent release` runs to completion, state.md
legitimately retains `working` — that's not a bug, it's missing state.
We address this on two fronts:**

1. **Close the watchdog-revival gap on release.** Extend
   `AgentLifecycleHandlers.ExecuteRelease` (or `AgentRegistry.ReleaseAgent`
   post-Free-flip) to call `WatchdogService.EnsureRunning()` best-effort.
   Today, `EnsureRunning` is only called from `DispatchService.cs:205`, so
   killing the watchdog (e.g., `taskkill /im dydo.exe /f`) leaves
   released-but-unkilled tabs stranded. One-line fix. Tracked as issue #102.
2. **Extend "effectively free" to include stale Working.** The existing
   `IsStaleDispatch` / `IsLauncherAlive` machinery (decision 017) lets us
   reclaim a stale `Dispatched`/`Queued` agent. Extend this to `Working` by
   reading the launcher/Claude PID from `.session` and checking liveness.
   A `Status==Working` agent with a dead `.session` PID, past a
   `StaleWorkingMinutes` threshold, should be treated as reservable —
   making `dydo agent claim <name>` and (critically) `claude --resume`
   just work, without the destructive `agent clean --force` path.
   Tracked as issue #103.
3. **Document the pitfall in `dydo/guides/troubleshooting.md`.** Two new
   sections: "Agent stuck in `status: working` after its tab closed" and
   "Watchdog is dead, tabs don't auto-close". Content is drafted in
   `dydo/agents/Grace/brief-troubleshooting-zombie-working.md`; needs
   docs-writer dispatch because co-thinker cannot edit `dydo/guides/`.

## Rationale

### Why the mechanism is "release never ran", not "release ran but state.md wasn't written"

Per `AgentLifecycleHandlers.cs:71-78`, the `"Agent identity released / Status:
free"` banner only prints on `ReleaseAgent` return value true, which requires
the full chain:
preconditions pass → `.session` deleted →
`LogLifecycleEvent(Release)` (synchronous `File.AppendAllText` in
`AuditService.cs:187` — not buffered, no async flush) →
`UpdateAgentState` to Free → `CleanupAfterRelease`.

So if you see "Status: free" in a terminal, release DID complete; the
banner is not capable of lying. And if the audit event was ever logged,
it's on disk, because the write is synchronous.

For the four observed zombies:
- No Release audit event exists for any of their current sessions (checked
  both main-repo `dydo/_system/audit/2026/` AND Emma's worktree's own audit
  dir, which is NOT junctioned — see issue #96 for the related watchdog
  footgun this creates).
- `.session` files still present (release deletes them at
  `AgentRegistry.cs:423-425`).
- `modes/` directories still present (release deletes them at
  `AgentRegistry.cs:520-522`).
- No leaked `.claim.lock` in any of the 4 workspaces (release-killed-mid-flight
  would leave this).

All of that is the exact fingerprint of "release was never invoked, or
`ValidateReleasePreconditions` returned false and the session ended without
resolving the blocker." It is NOT the fingerprint of a code bug in
`ReleaseAgent`.

### Why the user's observation is consistent, not contradictory

In Emma's specific case, the user saw a real release complete — on session
`a3f5693b` at 19:28:30 UTC, which was task
`investigate-printinboxitem-test-regression`. Frank then re-dispatched Emma
into a worktree at 19:30:22 UTC for
`auto-accept-edits-inquiry-ruling` (session `3f2b3b21`). That NEW session
never reached release. The user's memory ("I saw Emma release, then closed
the tab") and the current state ("status: working") are about two different
Emma tabs. No mystery, just a timing/re-dispatch gotcha worth naming.

### Per-agent "why release didn't run" (drives the doc recommendations)

The four agents had four different dead-end patterns, useful because they
cover most of the failure space:

- **Adele and Charlie** — code-writer + dispatched. The `requires-dispatch`
  constraint (`_system/roles/code-writer.role.json`, evaluated at
  `AgentRegistry.cs:491-503`) requires a reviewer dispatch before release.
  Hitting this at end-of-work is easy: `dydo inbox clear --all && dydo
  agent release` → the second command fails with
  `"Cannot release: dispatched code-writers must dispatch a reviewer before
  releasing."`. A Claude running low on context may not recover from this
  cleanly. This is the single most likely trigger for zombie-working
  scenarios, and is directly addressed in the troubleshooting doc.
- **Dexter** — co-thinker + dispatched. No `requires-dispatch` constraint.
  But co-thinker mode file explicitly says "don't release until the user
  says so." He drafted a message and went quiet — tab died waiting.
- **Emma (current)** — judge in a worktree. No constraint blocker. Tab
  simply died (last audit event was a trivial `cat .worktree`). Worktrees
  complicate recovery because `agent clean --force` doesn't tear down the
  git worktree or branch.

### Why not fix this with a watchdog orphan-detection path that transitions Working → Free

Tempting but wrong. The watchdog runs in a separate process and cannot
safely reconstruct the precondition checks that `ReleaseAgent` runs
(reply-pending, wait markers, role constraints, worktree state). A watchdog
that force-transitions a Working agent skips all of those — a Claude that
was about to send a reply, or a code-writer that hadn't dispatched a
reviewer, would be silently freed and their baton-passed task forgotten.
The reclaim-on-claim path (issue #103) instead puts the decision in the
hands of the next agent claiming the identity: they explicitly opt in to
taking over, and the preconditions become their responsibility (e.g., they
see the unread messages in inbox and can choose to reply).

This is consistent with decision 017's principle: reservation/reclaim is
strict and explicit; display is permissive.

### Why not also document this as a "pitfalls" doc

No `pitfalls.md` exists in the tree; `troubleshooting.md` already has a
"Stuck states" section with the exact shape ("symptom → cause → fix
table"). Adding to troubleshooting keeps the doc surface flat. If pitfalls
emerges later as its own axis, the troubleshooting entry can migrate or
cross-link.

## Consequences

- Issues #102 and #103 are now in the backlog. Both are medium severity —
  neither is a data-loss bug. #102 is a one-line fix; #103 is a modest
  refactor that should cite and extend decision 017's `IsReservable`.
- The troubleshooting doc grows by two sections (draft in
  `dydo/agents/Grace/brief-troubleshooting-zombie-working.md`). Requires
  docs-writer dispatch.
- Until #103 ships, the recovery playbook for zombie-working agents is:
  (1) `claude --resume` the tab if still alive; (2) `dydo agent clean
  <name> --force` otherwise, with worktree teardown first if in a
  worktree. This playbook is the core of the new troubleshooting entry.
- Decision 017's "reservation = strict, display = permissive" split now
  has a second concrete extension (Working), which validates the
  framing. Worth mentioning in 017's follow-ups (a minor inquisitor-level
  housekeeping task, not worth a dedicated dispatch).

## Related

- [017 — Stale-Free Semantics](./017-stale-free-semantics.md) — reservation
  vs. display split that #103 extends.
- [001 — Auto-Close for Dispatched Agents](./001-auto-close-dispatch.md) —
  the watchdog's main job; #102 closes a hole in its lifecycle.
- Issues #95–98 — watchdog CWD/orphan PID bugs. Separate from this
  decision but adjacent; do not conflate.
- Inquisition `stale-dydo-processes.md` — background for watchdog noise.
