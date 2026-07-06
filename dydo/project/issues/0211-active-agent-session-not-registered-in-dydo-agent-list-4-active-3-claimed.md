---
title: Active agent session not registered in dydo agent list (4 active, 3 claimed)
id: 211
area: general
type: issue
severity: high
status: open
found-by: manual
date: 2026-07-03
---

# Active agent session not registered in dydo agent list (4 active, 3 claimed)

The human has four active Tier-1 agent sessions, but 'dydo agent list' shows only three claimed working agents (Adele, Brian, Charlie) - one session is operating without a claimed dydo identity.

## Description

On 2026-07-03 balazs observed four active Tier-1 agent sessions while 'dydo agent list' reported only 3 working agents (Adele: dydo-2-sprint2-sync, Brian: dydo-2-birdseye, Charlie: notion-board-design). An unregistered session bypasses the guard's identity-based access control, audit attribution, inbox/messaging, and the dispatch tree - work done there is untraceable and unreachable by other agents. Possibly related: a runaway 'dotnet test --filter FullyQualifiedName~Notion' testhost (21 to 56+ GB RAM, sandbox dydo-test-e71d1692) launched from a Claude Code bash session could not be attributed with certainty; identity gaps make such attribution impossible. Each active session should verify its identity with 'dydo whoami' and claim via 'dydo agent claim auto' if unclaimed; separately, consider whether the guard should hard-block all work (not just dydo-path reads) from processes with no claimed identity.

## Reproduction

Root cause observed live on 2026-07-03/04 (session b4432298, initially resolving as Brian):

1. A Claude Code session was recognized by the dydo CLI as agent Brian (whoami, issue create,
   message all worked) even though Brian's claim record pointed at a DIFFERENT session id
   (a36a9b9d) - i.e., identity resolution fuzzy-matched one session's processes to another
   session's claim. This is the mixup: two live sessions, one claim.
2. In the same mixed-up session, the PreToolUse hook context (Read/Write/Glob) consistently
   reported "No agent identity assigned to this process" while CLI calls from the shell tools
   were recognized - identity resolution disagreed between hook-invoked and shell-invoked
   processes of the SAME session.
3. When the human had the other agent run 'dydo agent release', the mixed-up session lost its
   identity too (whoami: none), confirming both sessions were bound to one claim. After the
   release, 'whoami' listed Brian as free/claimable while 'agent status Brian' still showed
   working + claimed by a36a9b9d, and 'agent claim Brian' was rejected with "already claimed
   by another session" - the claim store was left self-inconsistent (orphaned claim).
4. 'dydo agent claim auto' from PowerShell failed with "No session ID available. Claim must be
   initiated via hook", but the SAME command via the Bash tool succeeded - claim/session-id
   plumbing behaves differently across the two shell hook paths (note: a chained
   'claim && whoami' in one Bash call showed claim succeeded but whoami still unbound,
   suggesting the binding lands after the hook completes, or per-invocation).

Symptoms to reproduce against: (a) two sessions resolving to one claim; (b) hook vs shell
identity disagreement within one session; (c) orphaned claim after release (status says claimed,
free-list says free, claim-by-name rejected); (d) PowerShell hook path cannot initiate claims.

## Additional observations (Brian, session a36a9b9d, 2026-07-04)

From the ORIGINAL claimant's side of the mixup, post-incident: `dydo whoami` in this session
still resolves consistently (Brian / orchestrator / dydo-2-birdseye / working), the guard,
messaging, and inbox all function, and `dydo agent list` shows a coherent 3-working roster.
So the orphaned-claim inconsistency (status=claimed-by-a36a9b9d vs free-list vs claim-rejected)
is a CLAIM-STORE inconsistency visible to OTHER sessions, while the bound session itself keeps
working normally — dangerous precisely because the legitimate holder sees no symptom.

Second-order effect worth capturing: **inboxes belong to the identity, not the session.**
When Adele's claim changed hands mid-day, messages sent to "Adele" (intended for the sprint2
implementer) were received by the NEW holder — identity-addressed messaging silently re-routes
history on handoff. Any fix should decide whether an identity handoff should archive/fence the
prior holder's inbox.

## Resolution

(Filled when resolved. Caveat from balazs: the installed CLI binary predates the current source
considerably - verify each symptom still exists in current code before fixing; the session
identity model is also in scope of the dydo 2.0 two-tier identity redesign, so some of this may
be superseded rather than fixed.)