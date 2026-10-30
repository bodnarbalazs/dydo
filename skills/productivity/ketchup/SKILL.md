---
name: ketchup
description: "Catch the user up on a project's purpose, progress, current work and next steps, including human decisions and actions. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Ketchup

Give Balazs a **project catch-up**: its purpose, progress, current work and next steps. The admiral or commissioned reporter builds the account from project records and captain returns. Include earlier context where it explains the work. A chat boundary does not define the project; state a narrower scope when requested.

Use an **inverted pyramid** for the report, each problem/outcome area and each substantive item. Put the outcome, state or ask first. Put useful support underneath. The opening and item summaries alone should tell a coherent story.

Use **ASD-STE100** as practical guidance for plain technical English: familiar, consistent terms, active voice, short direct sentences and one main idea at a time. Apply these principles without claiming formal compliance. Explain an unfamiliar term briefly where it matters. Write in the plain voice of `bro`, skipping its restating step.

## Establish the project state

Keep this turn read-only, apart from reply visuals made from available evidence. Read the project intent, delivery records, returns and decisions. Verify accessible live facts, including PR, CI and agent state. Resolve older entries against newer decisions. Distinguish landed code, completed proof, observed behavior and human acceptance. State missing facts and uncertain causes.

The map holder directs delivery; a commissioned reporter reports it. A catch-up does not commission implementation, restart held work or convene a tribunal. A walkthrough is a separate conversation to inspect what was built against the user's intent and taste. Neither skill invokes the other implicitly.

For a frozen example, state its date and historical purpose once at the start. Its questions and actions illustrate that scenario; they are not current requests or permissions. Keep later facts outside the frozen account.

## Build the account

Open with the project purpose, current focus and essential state. Group by recognizable systems, problems or outcomes. Keep ticket IDs and agent rosters out of the organizing structure. Put consequential changes and decisions first. If nothing changed and nothing waits on the user, say so and stop.

Mark every first-order substantive item with a meaningful emoji, including held and future work. Give it a short lead that carries its meaning. Supporting paragraphs or option bullets belong beneath that lead; a simple item needs no invented branches. Use the same cue for the same state throughout the account.

Each area uses four plain headings, without emojis, in this order:

### Purpose

Use **🎯** for the concrete system or feature, its original trigger and intended change. Name the guard refusing harmless shell commands before discussing its internal rules.

### Recently completed

Use **✅** for completed outcomes. A failed review or stopped investigation is a completed event, not a delivered fix. Include older behavior only where it explains the result.

### In flight

Use **✈️** only for work that is running. Name its owner and state. Mark held work with **⏸️**, finished or idle work with **⏹️**, and planned work with **🗓️**. Explain the next route in Next. An assignment or old dispatch does not prove activity. If activity is unknown, say what the record establishes.

### Next

Combine future work and human needs here, once. Name the descriptive owner, action, real dependency and commissioning state. Distinguish commissioned, planned and conditional work from preparation still owed.

- **🤖 AFK** marks work agents can do without the user. Include agent-owned preparation here.
- **❓** marks each direct question. A simple answer does not require a HITL session, new agent or thread. Two questions get two items.
- **❗** marks each required human action, such as login, setup or merge. Say what the action enables.
- **🗓️ HITL** marks a planned human-facing discussion or session, such as a walkthrough or grilling conversation. Use HITL only where that conversation is actually part of the route; say whether it is commissioned or conditional. An action to take in that session can use **❗ HITL**.

Put the concrete ask first, followed by its verified Question, Enablement or PR reference quietly in parentheses. Mark later steps as later. Do not invent readiness, commands, dependencies or an execution window.

## Make decisions answerable

Before a choice, explain what it concerns, why the work began and how it changed. Name the relevant files or system, actual evidence, missing proof and acceptance rule. A metric's definition does not explain why this delivery needs it.

Under the question, explain the alternatives, effects, costs and unknowns. Show why the user must decide, what each answer permits and what happens without one. Mark the recommendation with **➡️** and give its reason. Include existing preparation or tribunal outcomes where they affect the choice.

Name missing preparation and its owner. A prepared explanation may support a policy choice while a tooling or implementation contract is still owed. Keep that distinction visible.

Separate open choices from authorized decisions awaiting ratification. Say what was decided, its limits and what ratification or objection changes. Authorized work can continue before ratification; exhausted conditional authority does not renew itself. Report existing `what-would-balazs-do` records without starting new deliberation.

## Check the reading experience

Each area must make sense without opening links. Keep IDs and evidence subordinate. Cut filler, repeated asks and repeated authority caveats. Split dense sentences. Let useful detail determine length.

Use existing factual media or relationship visuals when they clarify the work. A screenshot or video needs real evidence behind it; a catch-up does not need a duplicate project board or a new evidence campaign.

Read the opening and item summaries alone, then the full account without links. Can the reader explain the original problem, what got better, what is running or held, and what each next step changes? Formatting and agent approval do not establish human understanding or acceptance.

For a complete project illustration, consult the [frozen Dydo returning-user example](resources/returning-user-example.md). Its facts and authority belong to that dated scenario.
