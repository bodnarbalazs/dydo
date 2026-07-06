---
id: 183
area: backend
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-05-18
resolved-date: 2026-07-04
---

# Identity hijack round 2: dydo agent role mutates other agent's record + phantom-inbox deadlock

Second incarnation of the identity-hijack bug class (related to #0108) on a different codepath: dydo agent role calls from multiple processes all wrote to Charlie's record. Plus a phantom-inbox deadlock where a guard NOTICE fires perpetually on a file neither side can clear. Reported by LC operator, reproduced 3x in one session.

## Description

A second, more severe incarnation of the dispatch identity-hijack bug surfaced during the LC `bot-defense-architecture` slice. Same root class as the Brian→Frank report attached to #0108, but with three new failure modes: a recovery-step self-hijack via `dydo agent role`, a phantom-inbox deadlock that blocks file IO, and a cascading hijack where unrelated agents' `dydo agent role` calls silently overwrite Charlie's role/task.

The bug appears to be: **`dydo agent role ... --task ...` invocations from multiple processes are all writing to Charlie's agent record instead of the calling process's.** Reproduced three times in one session.

**Note on tool version:** The LC environment was running the pre-#0108-fix build. The round-1 fix lands in `AgentSelector` (dispatch codepath), but the symptoms below are on the `dydo agent role` write codepath. The round-1 fix may or may not cover this — needs an independent check of the role-set codepath.

## Timeline (LC project, 2026-05-18 UTC)

- **~14:35** — Charlie claimed (originally) as orchestrator on `bot-defense-architecture`.
- **~22:30** — Transient terminal auto-resume. Afterward, `dydo wait` (background) starts failing with `exit code 2` on every restart. No useful stderr.
- **~22:45** — Per balazs's suggestion (recovering as Brian→Frank did), tried `dydo agent claim Zelda`. Claim succeeded; `whoami` reported Zelda; workspace switched to `dydo/agents/Zelda`.
- **~22:46** — Ran `dydo agent role co-thinker --task identity-bug-cleanup` from the Zelda process.
  - **Hijack incident #1**: the role-set mutated **Charlie's** record. Charlie flipped from `orchestrator / bot-defense-architecture` to `co-thinker / identity-bug-cleanup`. `whoami` still reported Charlie.
  - Side effect: a phantom message from Charlie to Zelda appeared at `dydo/agents/Zelda/inbox/98e06797-msg-general.md` with an empty subject and no body content of operational meaning.
- **~22:50** — Ran `dydo agent role orchestrator --task bot-defense-architecture` from the same process. Charlie's correct state restored. But the phantom Zelda inbox file remained.
- **~22:50 onward** — The dydo guard fires a perpetual NOTICE on every Read/Write/Edit/Glob/Grep/file-IO-Bash operation:
  > NOTICE: You have 1 unread message(s). From: Charlie | Subject: (none)
  > File: dydo/agents/Zelda/inbox/98e06797-msg-general.md
  > Your tool call was valid but paused to deliver this notification.
  > Read your message(s) and then clear them to continue.
  >
  > After reading, retry your previous action - it will succeed.
- **The guard's instruction is unfollowable**:
  - As Charlie, reading the Zelda file is blocked: "Agent Zelda has no role set."
  - As Charlie, `dydo inbox clear --id 98e06797` fails: ID is not in Charlie's inbox (the file is in Zelda's).
  - Re-claiming Zelda succeeds, but the next `dydo agent role` recreates hijack #1.
  - `Remove-Item` against the phantom file is blocked by the same guard NOTICE.
  - The promised retry-after-clear pathway never completes — guard NOTICE re-fires.
- **~23:00** — balazs applied `dydo guard lift Charlie 10` (10-minute RBAC suspension). Phantom NOTICE keeps firing — the lift is for Charlie's RBAC but the NOTICE re-routes the operation through the Zelda guard path, which is unaffected.
- **~23:05** — While waiting for guidance, **Hijack incident #2** happened spontaneously. `dydo whoami` against Charlie now reports:
  ```
  Role: code-writer
  Task: migration-edit-protection-tooling
  Writable paths: src/**, tests/**, ... dydo/agents/Charlie/**, dydo/project/backlog/**
  ```
  This is Adele's task area. Some other process (presumably the legitimate agent assigned to `migration-edit-protection-tooling`) ran a `dydo agent role code-writer --task migration-edit-protection-tooling` and the call wrote to **Charlie's** record, not theirs. The hijack is cross-process and cross-agent — any agent's role-set is hitting Charlie.

## Reproducible symptoms

1. **`dydo agent role ... --task ...` writes to Charlie's record regardless of the calling process's identity.** Reproduced 3× in one session, including the unrelated cross-agent case. `whoami` and the role-set codepath disagree on "current agent" resolution.
2. **Phantom-inbox deadlock**: a message sent during a hijack window survives in the recipient's filesystem inbox but cannot be cleared by either side. The dydo guard fires NOTICE on every file-IO operation forever after.
3. **`dydo wait` background failure post-hijack**: exit code 2, no stderr. May be downstream of the role/identity confusion.
4. **`dydo guard lift <agent>` does not help with the deadlock**: the lift is RBAC-scoped; the NOTICE rerouting happens at a different layer.
5. **Pure dydo CLI commands that don't touch file IO continue to work**: `dydo issue create`, `dydo inbox list`, `dydo task list`, `dydo agent list`, `dydo agent status`, `dydo whoami`, `dydo msg --to ...`. This is what lets a deadlocked operator file the bug report at all.

## Hypothesis

"Current agent" resolution inside dydo uses two different mechanisms:

- **whoami / claim semantics** — process-scoped, per-session.
- **`dydo agent role` write target** — somehow resolved via a different path (per-human's primary orchestrator? a global "active agent" pointer? the task's `assigned:` field?) that diverges from whoami after a re-claim.

If that path resolves to Charlie's record for *every* role-set in this human's space, the cross-agent hijack at ~23:05 is fully explained. Worth a quick git-blame on the role-set codepath to find where the target agent is loaded.

The phantom inbox deadlock is likely a separate bug — the cross-agent leak that put the file in Zelda's inbox during hijack #1 should not, by itself, cause an infinite guard loop. The NOTICE handler appears to retain pending state across tool calls without ever discharging it.

## Suggested investigation order

1. **Find the agent-resolution divergence in `dydo agent role`.** Log which record is being mutated; compare to `whoami`. The bug should reproduce immediately on any role-set.
2. **`dydo inbox clear --force --file <path>`** as an escape hatch for phantom files that survive cross-agent leaks. Today there is no operator-level way to clear one.
3. **NOTICE state machine fix**: the NOTICE about an unread message should either auto-discharge if the file is unreadable by the current agent, or escalate to BLOCK so the operator gets a deterministic error instead of an infinite loop.
4. **Bug-class root cause**: the dispatch identity-hijack (Brian→Frank, closed in #0108). The round-1 fix in `AgentSelector` probably eliminates the dispatch surface but the role-set codepath needs an independent check.

## Workarounds for stuck operators today

- Pure dydo CLI keeps working through the deadlock: file issues, list state, send messages.
- To break the deadlock, manually delete the phantom inbox file from the filesystem (outside any dydo-guarded process). In this LC repro: `dydo/agents/Zelda/inbox/98e06797-msg-general.md`.
- After deletion, the NOTICE stops firing and Read/Write/Edit/Glob/Grep operations resume.
- The `dydo wait` exit-code-2 failure may need a separate fix; manual restart after each clean state works.
- **Do NOT call `dydo agent role` while another agent is doing the same** — the race seems to amplify the hijack.

## Related files (LC-side)

- `LC/dydo/project/issues/0042-identity-hijack-bug-charlie-self-hijacked-zelda-phantom-inbox-deadlock.md` — short LC-side stub filed via `dydo issue create` (body could not be expanded due to the deadlock).
- `LC/dydo/agents/Frank/bug-report-self-dispatch-identity-hijack.md` — Frank's round-1 report (Brian→Frank dispatch identity hijack).
- `LC/dydo/agents/Charlie/log-bot-defense-architecture.md` — Charlie's orchestrator log; last clean state captured before the deadlock.

## DynaDocs-side context

- Resolved issue #0108 — round-1 fix in `Services/AgentSelector.cs` (commit 87db971). Filters `senderName` from `TryReserveFromPool` and rejects `to == senderName` in `SelectExplicit`. Round-2 symptoms are on a different codepath (`dydo agent role`) and a different layer (guard NOTICE state machine), so this issue needs independent investigation.

## Severity rationale

Operationally **high**. An agent in a deadlocked state can still file issues and read state via CLI, but cannot edit files, run reviewers, write decision docs, or unblock pending dispatches. A long-running orchestration slice (like `bot-defense-architecture`) can survive on already-in-flight dispatches but cannot progress until manually unblocked. Cross-agent hijack potential means *any* agent's work can be silently disrupted by an unrelated agent's normal commands.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD (F1): identity resolution now requires the caller to own the claimed agent — IsOwnedByCaller gate on the env-var fast paths (AgentRegistry.cs:984-997, 'Closes #0183 (F1)'; spot-verified). Goes live with the 2.0 install. Triage sweep 2026-07-04 (Brian, CoS).