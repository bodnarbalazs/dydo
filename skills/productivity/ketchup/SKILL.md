---
name: ketchup
description: "Catch the user up on what changed in this chat with a self-contained explanation of progress, unfinished work and decisions only they can make. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Ketchup

Give a returning user the project story: progress, current work, what starts next and where they are needed. Use an **inverted pyramid** throughout the report, each outcome area and each substantive item: essential meaning first, then useful supporting detail. Reading only the opening and item headlines should leave a coherent, less detailed account.

The update covers this chat since the user's last message before `/ketchup`; notifications and subagent results are not their messages. Reconstruct enough earlier context for someone who was not following closely. Write in the plain voice of `bro`, skipping its restating step. A catch-up reports state and involvement; a walkthrough lets the user inspect what was built against their understanding and taste. Neither implicitly invokes the other.

## Establish the facts and route

Keep this turn read-only, apart from reply visuals made from available evidence. Do not change code, start an app or job, or post elsewhere. Collect the work and relevant reports, then verify accessible live facts yourself, including PR, CI and agent state. Check numbers against their sources and acknowledge corrections. Distinguish merged changes, completed checks and proven live operation; name uncertain causes and unchecked state.

Find the descriptive owners, dependencies and actual commissions. Separate work already running from planned, parked or blocked work; missing assignment or dispatch evidence stays unknown. Establish where agents can proceed without the user and where they need participation. Existing holds and authority govern the route; reporting it grants no new permission.

## Lead with the project story

Open with the goal, what the project stands for, its current focus and essential state: what got better, what remains unfinished and what needs attention. Group supporting detail by recognizable problem or outcome; one area may span several tickets or captains. Order areas by consequence and attention, with headlines that reveal their state. If nothing changed and nothing waits on the user, say so and stop.

For a historical worked example, state once at the presentation start that it is an old scenario used for review, not a current catch-up or request to answer its questions. Keep the rest of the example inside that clear frame rather than repeating disclaimers in each area.

## Use the selected chronology

Every area uses the same five selected cues and labels below, separated by blank lines. Within each section, lead each substantive item with a concise meaningful headline, one-line summary or bullet carrying its outcome, state or ask. Put useful explanation immediately underneath. Separate genuine multiple items with bullets or shallow branches; a simple item needs only one line unless context adds meaning. A label alone is not an item summary.

**🎯 Purpose** — What this area is and why it exists. Keep current status in the area headline and In flight, rather than adding a second “now” duty to Purpose.

**⏮️ Recently completed** — What got done recently, including the outcome of completed attempts. Put old behavior or defect history beneath a completed change only when it explains why that change matters. A failed attempt remains failed. If no recent completion is established, say so briefly.

**🛠️ In flight** — What is being worked on now: current state or outcome, descriptive owner, relevant decisions and blockers. Identify held or parked work accurately instead of making it sound active. When activity or commissioning is unestablished, report the known state and that limit.

**➡️ Next** — What will start next and what it depends on. Name the descriptive owner, action and supported commissioning state; make clear which branches agents can handle alone and which need the user. Use plain language instead of relying on AFK/HITL abbreviations. Completed actions belong in Recently completed; ongoing work belongs in In flight. Show genuine branches without manufacturing assignments or readiness.

**🙋 Your attention** — Concrete questions, actions and blockers for that route. Each distinct question starts with red **❓**; each distinct required action or blocker starts with **❗**. Two questions get two marked items. Put the human-readable ask first, including its purpose or what it unblocks; quietly append the corresponding existing Question or Enablement reference in parentheses after the headline. Use only verified records, never an illustrative identifier or an inferred new ticket.

Under each attention headline, give the context that makes the response usable: alternatives and consequences for a choice, the recommendation and reason, or necessary readiness and execution details for an action. Explain why the user is needed, what their response changes and what continues or waits without one. A wizard run or login is an action, not a disguised question. Mark a later step as later; do not invent its command or window. Name the owner of an agent or external blocker so it does not become an accidental user ask. When nothing needs the user, one clear line is enough.

Distinguish open decisions from decisions already made under authority but awaiting ratification, including existing recorded `what-would-balazs-do` decisions. Say what ratification or objection changes; work already authorized to proceed does not automatically stop while ratification waits. A conversation with the user need not be an approval request. Report only witnessed decisions; this catch-up commissions no new tribunal. Routine checks and preparation stay with agents.

## Check the reading experience

Each area must stand alone without provenance clicks. Keep essential context in the reply, with IDs, PRs and source links subordinate. Let useful detail determine length; remove filler, dense undifferentiated paragraphs and repeated no-action or authority caveats. Repeating an essential fact in a headline and its explanation is useful when it supports the inverted pyramid.

Additional visuals should clarify the work: existing captures for visible changes, diagrams and charts from verified facts. Never invent numbers or present a generated screen as a capture. Links point to existing artifacts and do not carry essential meaning alone.

Read just the opening and item headlines, then the full areas without their links or extra visuals. Can the reader distinguish purpose, recent outcomes, current work and next starts, and explain what each ask changes? Repair missing meaning or weak hierarchy. Formatting, selected icons and agent approval do not substitute for the user's understanding and taste.

For a complete illustration, consult the [historical returning-user example](resources/returning-user-example.md). Its facts, decisions and permissions belong only to that scenario.
