---
title: Swarm 0192
area: general
name: swarm-0192
status: stale
created: 2026-07-12T18:48:53.6270109Z
assigned: Charlie
needs-human: false
---

# Task: swarm-0192

CODEX swarm fix ROUND 2 — issue 0192 (HIGH; SECURITY-sensitive). Your round-1 added the `--force --file` escape + a recovery hint + tests. The escape ARCHIVES (not deletes) and leaves normal clear unchanged — a reviewer confirmed those are good, KEEP them. But the review FAILED on real issues; fix all of them. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

DECISION (from balazs): the force-clear must be BOUND to genuinely-orphaned inboxes — it is a stuck-inbox RECOVERY tool, not a general cross-agent clear.

FIX 1 — BOUND the force-clear to orphaned inboxes (closes cross-agent tampering). Currently `--force --file` clears ANY named inbox file with no precondition, so any Tier-1 agent can force-archive ANOTHER live agent's unread inbox item — defeating guaranteed delivery, an action the guard explicitly blocks elsewhere (`BlockIfCrossAgentWorkspace`, "cross-agent tampering — plans/notes/inbox"). Add a precondition in `Services/InboxService.cs` ExecuteForceClear: resolve the OWNER agent (segments[0] of the path) and REFUSE unless that owner has NO valid live session (genuinely orphaned/stuck) — use AgentSessionManager (or the same liveness check the guard/registry use for "is this agent's session alive") to determine the owner has no resolvable live `.session`. If the owner has a live session, refuse with an actionable error ("agent <owner> has a live session; its inbox is not orphaned — it must clear its own item").

FIX 2 — PATH VALIDATION (security: traversal / cross-drive escape). `Path.GetRelativePath(WorkspacePath, inboxFile)` currently accepts `<agentsPath>/../inbox/x.md` (segments `["..","inbox","x.md"]` passes the length-3 + `segments[1]=="inbox"` check) and Windows cross-drive `D:\inbox\x.md`, moving files OUTSIDE the agents tree. Fix: validate `segments[0]` IS a real agent via `registry.IsValidAgentName(segments[0])`, AND confirm the resolved absolute path is genuinely inside `<agentsPath>/<owner>/inbox/`. Reject `..`, cross-drive, and any path not inside a real agent's inbox with a friendly error.

FIX 3 — REWORK THE RECOVERY HINT so it fires in the REAL deadlock (currently dead code + a harmful false trigger). `GuardCommand.TryPrintUnreachableInboxRecovery` compares `registry.GetCurrentAgent(sessionId)` to `agent` — but BOTH come from the same `GetCurrentAgent(sessionId)` call, so they're always equal and the hint NEVER fires in the real 0192 deadlock. The real deadlock is ASYMMETRIC resolution: the guard hook resolves the agent fine via its `session_id`, but `dydo inbox clear` resolves via `GetSessionContext()` / `.session-context` and CANNOT. Also the current mismatch trigger fires on `GetCurrentAgent` returning null under a 5s scan TIMEOUT/race — telling an agent to force-archive its OWN readable messages UNREAD (weakening read-before-clear).
FIX: emit the forced-clear hint only when the CLEAR-SIDE resolution path (the `.session-context` / GetSessionContext path that `dydo inbox clear` uses) cannot resolve the agent for the unread item — i.e. the item's inbox is genuinely unreachable by the normal clear path (owner orphaned / clear-side can't resolve). Do NOT emit on a transient GetCurrentAgent-null timeout. Point the hint at the bounded FIX-1 command form. It must NEVER instruct an agent to force-clear its own live, readable messages.

TESTS (each RED before the corresponding fix; use a PRODUCTION call path for the hint, not an artificial direct invocation):
1. FORCE-CLEAR ORPHANED: `--force --file` on an inbox whose owner has NO live session → archives it; owner WITH a live session → REFUSED with the actionable error.
2. PATH VALIDATION: `--force --file` with a `..`-traversal path and a cross-drive path → REJECTED (no move outside the agents tree); a valid `<agent>/inbox/x.md` → accepted.
3. FLAG ERRORS: `--force` without `--file` → friendly error; `--force --file` with `--all`/`--id` → friendly error.
4. HINT via production path: simulate the asymmetric-resolution deadlock (clear-side can't resolve the agent for an unread item) through a real `NotifyUnreadMessages` call site → the forced-clear hint IS emitted; a normal reachable-inbox unread → NOT emitted, normal notice shows.
5. REGRESSION: normal (non-force) `dydo inbox clear` still requires the item be read + is byte-unchanged; the guard's 0155 dydo-chain analysis is untouched; the hint never auto-clears.

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` (0 errors); `dotnet test` filtered to Inbox + Guard/notice tests — all green. Do NOT run the python coverage gate.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0192-r2` with: the orphaned-precondition (how you check owner liveness), the path validation, how the reworked hint detects the real clear-side failure + that it can't fire on transient timeout, tests added, build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch ONLY `Services/InboxService.cs`, `Commands/InboxCommand.cs`, `Commands/GuardCommand.cs` (NotifyUnreadMessages / TryPrintUnreachableInboxRecovery only — do NOT alter the 0155 dydo-chain security analysis), and their test files. Do NOT touch AgentRegistry.cs, AgentSessionManager.cs (you may CALL its liveness check, not modify it), Sync/, or peer files.

--- STANDING INSTRUCTIONS ---
1. TEST VECTORS: cover the exact vectors above (each RED before the fix). Security-sensitive — the orphaned-precondition + path-validation tests are the point.
2. COMPLEXITY: keep any method <= CC 30 (extract helpers in GuardCommand).
3. NO DESTRUCTIVE GIT: never git checkout/reset/stash/clean.
4. CONCURRENCY: other agents may edit OTHER files; build errors only outside your files = a peer mid-edit — wait + rebuild.
5. REPORT+RELEASE as above; do NOT run the python gate.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)

> Mass-closed 2026-07-16 (DR-041 campaign wrap-up): pre-campaign roster-era task; the work either landed before the pivot or was abandoned with the roster. See git history.
