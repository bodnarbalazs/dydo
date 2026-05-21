---
id: 198
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# Dispatch-prompt name and process-claim name can diverge silently (live-incident corroboration)

An obedient onboarding pass with diverging prompt-name and process-claim names exercises the hijack primitive itself: the agent reads the wrong workflow file and either re-claims onto a busy slot or processes someone else's inbox under the misnamed claim.

## Description

The dispatch carrier (prompt text "Name --flag") and the process's actual claim can name two different agents at session start. An obedient onboarding pass — per `dydo/index.md` lines 13–22 ("Check your prompt for an agent name … Open `agents/<your-name>/workflow.md`") — would then read the wrong agent's workflow file and either (a) issue `dydo agent claim <prompt-name>` from a process already claimed under a different name, exercising the hijack primitive itself, or (b) process the wrong agent's inbox under the misnamed claim.

The guard currently has no check that detects "prompt names X, process claimed as Y" as a class — it only blocks on missing-role symptoms, which mis-attributes the cause.

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F14.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

User's opening prompt: literal string `Emma --inbox`. `dydo whoami` reported the process identity as **Dexter** at that moment:

```
Agent identity for this process: Dexter
  Assigned human: balazs
  Role: (none set)
  Status: working
  Workspace: ...\dydo\agents\Dexter
```

At the same moment Emma was NOT in the "Free agents" list — Emma was claimed by some other session. The dispatch carrier (prompt text) was addressed to a real, currently-claimed agent, but the actual process claim was a third agent (Dexter).

The guard *did* eventually block an Emma-workflow read, with the message `Agent Zelda has no role set` — but the diagnostic was misleading: it referred to Zelda (claimed later) and to a missing role, not to the name mismatch.

## Relation to Brian's surfaces

This finding lives one layer above S0–S13. Brian's map covers what happens once a command runs; this covers how the wrong-name prompt arrives at the process in the first place. Candidate fix space includes: a startup-time check that the prompt-named agent matches `DYDO_AGENT`/`whoami`, or a refusal in the onboarding doc itself ("if your prompt says X but `dydo whoami` says Y, stop and report"). Currently neither exists.

## Suggested follow-up

Trace the dispatch entry-point that produces the `Name --flag` prompt text and identify where the claim drifts from that name. A reviewer scout (proposed in the inquisition addendum) should look at where the prompt text is generated vs. where the registry claim is written — those two writes can clearly diverge.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)