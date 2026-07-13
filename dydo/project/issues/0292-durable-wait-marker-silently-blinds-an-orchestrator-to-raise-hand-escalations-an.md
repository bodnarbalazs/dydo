---
title: Durable wait marker silently blinds an orchestrator to raise-hand escalations (and dydo wait refuses to arm over it)
id: 292
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Durable wait marker silently blinds an orchestrator to raise-hand escalations (and dydo wait refuses to arm over it)

dydo wait --register satisfies the guard but never notifies; dydo wait then refuses to arm over it - so an idle orchestrator goes permanently deaf and a blocked worker's raised hand is never heard.

## Description

## Observed

An orchestrating agent (Adele, claude) sat blind to a subordinate's **raised hand** for ~20 minutes: a blocked codex code-writer (Brian) messaged twice reporting a spec contradiction, and neither message surfaced. The human had to ask "what about your inbox?" to expose it.

## Root cause — a trap the guard walks you into

Two mechanisms interact badly:

1. **`dydo wait --register` (durable marker) delivers NO notifications.** It satisfies the guard's "must keep a general wait active" requirement, but it is a passive marker — the holder is never woken when mail arrives. Incoming messages only surface incidentally, via the guard's `NOTICE: You have N unread message(s)` on the agent's *next tool call*. An agent that is idle-waiting (exactly the state an orchestrator is in while subordinates work) makes no tool calls, so it never sees them.

2. **`dydo wait` (the notifying, blocking form) REFUSES to arm while a durable marker exists:** `A general wait is already active for Adele (PID N). Refusing to register a duplicate.`

So once an agent registers a durable marker — which the guard *pushes* you toward, and which is mandatory on a codex host — it becomes **permanently deaf** until it explicitly runs `dydo wait --cancel` first. Nothing tells you this. The guard's own error message (`Run: dydo wait (in background), or 'dydo wait --register' ...`) presents the two as interchangeable alternatives. They are not: one hears, one does not.

## Impact

This silently defeats the raise-hand escalation path, which is the system's primary safety valve for a worker that hits an ambiguity rather than guessing. A blocked worker's escalation lands in a mailbox nobody is listening to. It is the same failure class as #0279 (codex agents don't wake on inbox delivery) but on the *orchestrator* side, and it affects claude hosts too.

## Fix direction

- The durable marker should either (a) deliver notifications, or (b) be clearly documented as non-notifying, with the guard's hint distinguishing them (e.g. "`dydo wait` — blocks and notifies; `--register` — durable marker, does NOT notify, you must poll `dydo inbox show`").
- Better: make `dydo wait` supersede/upgrade an existing durable marker for the same agent instead of refusing, so the notifying form is always reachable.
- Consider surfacing unread-inbox state to a waiting orchestrator proactively (the `*` unread marker already exists in `dydo agent list`).

## Acceptance

- An agent holding a wait (either form) is reliably woken, or is explicitly told it must poll.
- `dydo wait` after `dydo wait --register` does not dead-end with a refusal.
- A subordinate's raise-hand reaches its dispatcher without human intervention.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)