---
name: overnight-mode
description: Overnight mode, the user away while admirals run on standing and night orders. Load only on the chief of staff's muster or morning-brief announcement, or when the user calls overnight mode.
---

# Overnight Mode

**Overnight mode** is any long absence of the user. The chief of staff's announcement at the
**muster** opens it and its announcement at the **morning brief** closes it. In between, **each
admiral keeps the conn** of its Project and the chief of staff keeps only the **night watch**; any
agent can confirm it with `dydo mode show` (`overnight` is `on`).

Admirals obey the **standing orders** below plus the **night orders**: one Project update per
Project, set at muster. Night orders expire at the morning brief and win where the two conflict.

## Muster

The chief of staff, with the user present:

1. Have every admiral report its likely Questions, human-only unblockers and needed permissions.
2. Interview the user per Project with `grilling`: what gets done, the stop line, what stays his,
   answers now, unblockers now. Use `wizard` for terminal steps only.
3. Post the night orders.
4. Preflight: every admiral thread can be messaged, every session can write to Linear, every
   admiral's harness runs `dydo merge <pr> --match-head-commit <sha> --dry-run`: exit 0, or a
   `refused:` line with exit 1, proves the path; a denial or a prompt is the gap. Report all green,
   or name each gap.
5. Create the 30-minute watch schedule, returning to this thread.
6. Run `dydo mode set overnight on`: rerun it after the guard's nudge, and where the guard denies,
   the user runs it in his own terminal.
7. Announce the start to every admiral.

Done when every admiral has the announcement.

## Standing orders

1. **Keep the conn.** Project decisions are the admiral's.
2. **Decide by weight.** Easily reversible: decide and record `DECIDED`. Other calls:
   `what-would-balazs-do`, whose trigger map decides it or prepares the Question;
   irreversible calls are always a Question.
3. **Merge into the feature branch** through your captain's Merge chain, which runs `dydo merge`;
   never into master or main.
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

The chief of staff, every 30 minutes:

1. Diff each Project's board. Changed: the Project is alive; move on.
2. Unchanged: read the admiral's thread. Idle, or waiting with nothing in flight: send one specific
   nudge naming what the watch saw.
3. Nudge unanswered: go to its captain threads.

The board and threads carry state; never ask for routine STATE. Mediate cross-Project trouble by
chief-of-staff Method step 4, Mediate a collision, relaying heads-ups down the chain. After any gap
in the watch, rebuild from Linear and sweep everyone by Method step 5, Sweep the board.

## Morning brief

When the user says he's back, in the chief of staff's conversation: delete the watch schedule first,
then run `dydo mode set overnight off` as at muster, then announce the end to every admiral. Then
brief him, writing nothing to the repo:

1. A recap in ketchup's recap shape: one real visual of everything the night touched, at most three
   result lines.
2. PRs into master, linked.
3. Questions, decided ones first, each with context for an outsider, and the decision with its
   justification where decided.
4. Optionally, `wizard` for terminal unblockers only.
5. What needs him next.
