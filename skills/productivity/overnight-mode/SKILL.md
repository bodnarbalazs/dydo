---
name: overnight-mode
description: Overnight mode, the user away while admirals run on standing and night orders. Load only on the chief of staff's muster or morning-brief announcement, or when the user calls overnight mode.
---

# Overnight Mode

**Overnight mode** is any long absence of the user. At **muster**, the chief of staff verifies
`overnight: on` with `dydo mode show` before announcing the start; at the **morning brief**, it
verifies `overnight: off` before announcing the end. In between, **each admiral keeps the conn**
of its Project and the chief of staff keeps only the **night watch**. Intent, a watch schedule and
message delivery do not establish a mode transition.

Admirals obey the **standing orders** below plus the **night orders**: one Project update per
Project, set at muster. Night orders expire at the morning brief and win where the two conflict.

## Muster

The already-invoked chief of staff uses its own resources below. Resolve each resource inside the
installed `chief-of-staff` skill folder; read it directly without invoking another officer role.

1. While the user is present, complete the [approval preflight](resources/approval-preflight.md)
   for both startup and shutdown. Resolve the supported approval route or exact terminal fallback
   before proceeding; capability discovery is not a mode change or an approval-UI test.
2. Complete `chief-of-staff`'s `resources/muster.md`. Use `grilling` for
   decisions and `wizard` for terminal steps only. Proceed once every included admiral confirms
   its agreed work is runnable and every gap has the user's explicit disposition.
3. Use the preflight's route for `dydo mode set overnight on`. After actual execution, independently
   run `dydo mode show` and require `overnight: on`. On denial, failure or missing readback, report
   activation incomplete with the current observed state and exact human action; keep the user
   present and do not announce activation or say goodnight.
4. Create the 30-minute watch schedule, returning to this thread. Preserve the complete schedule
   ID exactly as returned by the host, including any prefix, for deletion at shutdown.
   If scheduling fails, report mode on with watch unavailable and resolve it while the user is present.
5. Announce the verified start to every included admiral.

Done when readiness is confirmed, the schedule ID is preserved, mode is on and every included
admiral has the announcement.

## Standing orders

1. **Keep the conn.** Project decisions are the admiral's.
2. **Decide by weight.** Easily reversible: decide and record `DECIDED`. Other calls:
   `what-would-balazs-do`, whose trigger map decides it or prepares the Question;
   irreversible calls are always a Question.
3. **Merge into the feature branch**, never into master or main.
4. **The board is the report.** Send the chief of staff no status. Contact it for cross-Project
   trouble: collisions, shared-resource problems, and heads-ups or permission before actions that
   affect other agents. It relays down admiral → captain → crew. Answer a nudge in one line.
5. **Keep moving.** One blocker never stops the night: take the next pickable work. Stop only at the
   night orders' stop line or when nothing is pickable. The walking skeleton does not wait either:
   its Walkthrough Ticket blocks nothing; post `DECIDED` on it, carry on, and treat a wrong
   direction as a revert.
6. **Write for a reader who wasn't there**, every Question and `DECIDED`. Withdraw only Questions
   made obsolete by discovered facts or completed work; keep tribunal-decided Questions awaiting
   ratification.

## Night watch

The chief of staff runs its Overnight duties watch step every 30 minutes, using its
`resources/night-survey.md`. The surveyor inspects; the chief of staff acts on its findings:

1. Idle, or waiting with nothing in flight and runnable work: send one specific nudge naming the evidence.
2. Nudge unanswered: inspect the relevant captain threads and intervene with the owner.

The board and threads carry state; never ask for routine STATE. Mediate cross-Project trouble by
chief-of-staff Method step 4, Mediate a collision, relaying heads-ups down the chain. After any gap
in the watch, rebuild from Linear and sweep everyone by Method step 5, Sweep the board.

## Morning brief

When the user says he's back, in the chief of staff's conversation:

1. Complete the [approval preflight](resources/approval-preflight.md) for shutdown while the user
   is present; recheck if host or permission mode changed overnight.
2. Delete the watch schedule using the complete preserved ID. List schedules and confirm that exact
   ID is absent before claiming the watch stopped; report a deletion or verification gap.
3. Settle any pending survey through its host lifecycle so no tick remains in flight.
4. Use the preflight's route for `dydo mode set overnight off`. After actual execution, independently
   run `dydo mode show` and require `overnight: off`, then announce the end to every included admiral.
   On denial, failure or missing readback, report shutdown incomplete with the current observed
   state and exact human action. A deleted schedule does not prove mode is off.
5. Fill `chief-of-staff`'s `resources/morning-brief.md` in the conversation, writing nothing to the
   repo. Use `wizard` for terminal unblockers only.

Done when the schedule is confirmed absent, pending surveys are settled, mode is off, admirals
have the announcement and the user has the brief.
