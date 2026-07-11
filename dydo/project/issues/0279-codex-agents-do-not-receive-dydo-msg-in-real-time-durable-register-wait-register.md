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

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)