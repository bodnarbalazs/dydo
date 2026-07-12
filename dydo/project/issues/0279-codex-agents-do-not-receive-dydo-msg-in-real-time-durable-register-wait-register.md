---
title: Codex agents do not receive dydo msg in real time - durable --register wait registers a marker but never surfaces incoming messages into the session (release/coordination gap)
id: 279
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# Codex agents do not receive dydo msg in real time - durable --register wait registers a marker but never surfaces incoming messages into the session (release/coordination gap)

Observed twice 2026-07-11 (Leo, Henry): a codex agent told to release via 'dydo msg --to <agent>' does NOT act on it - the message sits unread in its inbox. Root cause: codex agents satisfy the guard's active-wait requirement with 'dydo wait --register' (a durable marker, c1-2), but unlike a claude agent's blocking 'dydo wait' (harness notifies + agent reads on message arrival), the marker does NOT surface/push incoming messages into the codex session's context. So a codex agent never sees a mid-task or post-task message unless it manually polls 'dydo inbox show'. Confirmed: telling Henry to release directly in its terminal worked instantly (release mechanism is fine); only the message-DELIVERY-to-codex is broken. IMPACT: blocks orchestrator coordination of a codex fleet - can't sequence/redirect/release codex agents via dydo msg, which is the whole coordination substrate. WORKAROUNDS (adopt now): (1) codex dispatch briefs must say 'report, then RELEASE yourself - do NOT wait for a confirm message, you will not receive it' (their uncommitted work persists in the tree for the CoS to sequence; re-dispatch if a fix is needed); (2) push coordination to DISPATCH TIME (self-contained task-boundary briefs) not mid-task, aligning with DR-037's task-boundary model. FIX: give the codex durable wait a real message-delivery path - poll-and-surface, or a codex-side notification hook that reads new inbox items into the session, so 'dydo msg' reaches codex like it reaches claude. Route: codex-workhorse / cross-vendor coordination, BEFORE scaling multi-codex orchestration. Found by balazs+Adele.

## Investigation (codex agent Mia, 2026-07-11) — clean design found, needs a decision

Mia (codex) investigated and confirmed: the durable wait has NO delivery path.
`WaitCommand.RegisterDurableWait` writes a marker keyed to the codex host PID and returns;
`MessageService.DeliverInboxMessage` writes the file + adds the id to UnreadMessages. A foreground
`dydo wait` prints because its poll loop stays alive; the durable marker has no live process, so
nothing surfaces the message. Codex's `notify` (config.toml) is OUTBOUND lifecycle only, not an
inbound trigger; `.codex/hooks.json` PreToolUse/Stop only fire on codex's own turn events — none
can wake an idle codex session on message arrival.

**The clean path (Mia's recommendation):** the codex CLI exposes an experimental
**app-server / remote-control JSON-RPC** (methods `thread/resume`, `turn/start`,
`thread/inject_items`). dydo already stores `AgentSession.SessionId` for codex sessions, and it
appears to be the codex thread id. Add an optional **`CodexInboxDeliveryService`** (not a
WaitCommand hack): on `dydo msg` to a working codex agent with a durable wait, keep the file/unread
write as source of truth, then — if app-server remote control is available/trusted — `thread/resume`
+ `turn/start` a bounded user turn into that thread ("dydo message from X, subject Y, body Z, id …;
run dydo read <id> after acting"). Idempotent by message id; fall back to inbox-only + sender
warning when app-server is unavailable. NOT background stdout polling (terminal-visible, not
model-visible).

**Decision needed:** adopting this means enabling/owning codex's EXPERIMENTAL app-server surface and
mapping `SessionId → threadId` across CLI/app-launched sessions. Out of a single code slice.

**REFRAME — this is NOT a swarm blocker:** the workarounds above (self-release + self-contained
task-boundary briefs, already adopted) let the multi-codex swarm run fire-and-forget TODAY. The
app-server delivery is the PROPER fix for full mid-task coordination — a SCHEDULED DECISION /
follow-on, not a 2.0.9/swarm gate. Recommend: run the swarm on the workaround; pursue the app-server
design if fire-and-forget proves limiting. Schema Mia generated: her workspace,
codex-appserver-schema-0279.

## Empirical confirmation (codex Brian, 2026-07-13) — DECISIVE

balazs asked the sharp question: can a codex agent run a background `dydo wait` that WAKES it on message arrival (the way Adele's Claude harness re-invokes on background-task completion), making #0279 a cheap onboarding fix instead of an app-server build? Brian (codex) ran the crux EMPIRICAL self-test: launched a DETACHED child (`sleep 15; exit 17`) while the parent tool call stayed alive 30s. Result — at 10s the codex infra yielded ONLY the still-running tool cell: NO child-completion event, NO inbox signal, NO new agent turn. The completion surfaced ONLY when Brian EXPLICITLY called the wait tool (returned "child-completed-silently" at 30.6s).

**CONCLUSION (settled empirically): a detached/background-process completion does NOT re-invoke a codex agent or surface output to it — a codex agent cannot end its turn and be woken by a shell child in this host model.** Codex CLI exposes interactive/exec/resume + experimental app-server, but NO watch/background-completion-wake option; `resume` needs an explicit CLI invocation + prompt; `~/.codex/config` `notify` is OUTBOUND lifecycle only. WindowsTerminalLauncher launches `codex … --inbox` with no supervisor/re-arm; `.codex/hooks.json` has only PreToolUse/Stop guards (neither fires on inbox arrival); shell-spawned wait re-arms fail the sibling-ownership gate; a foreground `dydo wait` dies at the codex tool timeout.

So **onboarding alone CANNOT solve #0279** — there is no managed background-completion→turn wake to hang a `dydo wait` loop on. This confirms Mia's design: the fix is the **app-server / remote-control PUSH** (keep the inbox write as truth; a delivery service uses the codex app-server connection, maps SessionId→threadId, then idempotently `turn/start` a bounded "run `dydo read <id>`" prompt into the thread; warn + fall back to inbox-only when unavailable). NOT stdout polling. **#0279 requires the app-server integration — a scheduled design/build, not a cheap onboarding change.** balazs's hypothesis is answered: no cheap path exists.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)