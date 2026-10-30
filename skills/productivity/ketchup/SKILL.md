---
name: ketchup
description: "Catch the user up on a project's purpose, progress, current work and next steps, including human decisions and actions. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Ketchup

Give Balazs a **project catch-up**: what the project is for, what changed, what is actually running and what happens next. The admiral or its commissioned reporter reconstructs the story across relevant project records and captain returns, including context from before the user's absence. A chat boundary is not the boundary of the project. For a narrower requested scope, make that scope explicit.

Use an **inverted pyramid** at three levels: the report, each recognizable problem/outcome area and each substantive item. Lead with concrete meaning; follow with useful support. Reading only the opening and item summaries should leave a coherent, shorter account. Write in the plain voice of `bro`, skipping its restating step.

## Establish the project state

Keep this turn read-only, apart from reply visuals made from available evidence. Collect the governing project intent, current delivery records, relevant returns and decisions. Verify accessible live facts, including PR, CI and agent state; reconcile newer dispositions with historical entries. Separate landed code, completed proof, observed operation and human acceptance. Name missing facts and uncertain causes instead of filling them in.

Preserve ownership: the map holder directs delivery; a commissioned reporter reports it. Asking for a catch-up does not commission implementation, restart a held lane or convene a tribunal. A walkthrough is a separate conversation for inspecting what was built against the user's understanding and taste; neither skill implicitly invokes the other.

For a frozen worked example, state its date and historical purpose once at presentation start: its questions and actions are illustrative, not current requests or permissions. Use only facts available by that freeze.

## Build the account

Open with the concrete project purpose, current focus and essential state. Group by recognizable systems, problems or outcomes rather than tickets or agents. Put consequential changes and decisions first. If nothing changed and nothing waits on the user, say so and stop.

Each area uses four plain headings, without emojis, in this order:

### Purpose

Start the actual purpose item with **🎯**. Name the concrete system or feature, the original trigger and what the work is meant to change. “Improve safety” is too vague when the issue is a guard refusing harmless agent shell commands. Explain the system before its internal vocabulary.

### Recently completed

Start each completed item with **✅** and its outcome. Report completed attempts accurately: a failed review or stopped investigation can be a completed event without being a delivered fix. Include older behavior only where it explains the recent result.

### In flight

Start each truly running item with **✈️**, naming the work, owner and actual state. Held, planned and idle work gets a plain status such as “This lane is held” or “Nothing is running now”; explain its restart route in Next. An assigned ticket or old dispatch alone does not prove activity. When activity is unknown, say what the record establishes.

### Next

Combine upcoming work and human dependencies here, once. Use explicit **AFK** for work agents can do without the user and **HITL** for work involving the user. Name the descriptive owner, action, actual dependency and whether it is commissioned, planned, conditional or still missing preparation. Do not invent a dependency to make the sequence neat.

Each distinct question starts with red **❓**. Each required human action, enablement or merge starts with **❗**. Lead with a meaningful human-readable question/action and what it changes; quietly append the verified existing Question, Enablement or PR reference in parentheses. Two questions need two items. Agent-owned preparation is AFK work, not a human action. A conversation need not be an approval request. Mark later human steps as later, without inventing readiness, commands or an execution window.

## Make decisions answerable

Before presenting a choice, establish its actual object or files, original commission and relevant evolution. Explain what evidence exists, precisely what remains unproved or disputed, and the governing policy or acceptance tension. A generic definition of a metric cannot replace the story of the maintained code whose delivery depends on it.

Under the question, give meaningful alternatives, their practical effects, costs and unknowns, a recommendation with its reason, and why the user must decide. State what each answer authorizes and what happens without one. Include any existing preparation or tribunal outcome. If preparation is missing, name the owed work and owner rather than offering a ready-looking decision. A prepared explanation can support a policy choice while its tooling or implementation contract remains unprepared; distinguish those states.

Keep open choices separate from existing authorized decisions awaiting ratification. Report what was decided, its limits and what ratification or objection changes. Unratified does not automatically stop authorized work; an exhausted conditional authorization does not renew itself. Report existing `what-would-balazs-do` records without starting new deliberation.

## Check the reading experience

Every substantive item begins with a concise outcome, state or ask, followed by genuinely useful context underneath. Separate distinct items with bullets or shallow branches; a simple item needs no invented subparts. Keep the four headings plain and put icons on the items they describe.

Each area must make sense without provenance clicks. Keep IDs and evidence links subordinate. Remove filler, dense undifferentiated blocks, repeated asks and repeated authority caveats. Let useful detail determine length. Additional visuals should clarify the work using verified facts or existing captures; never invent measurements or present a generated screen as a capture.

Read the opening and item summaries alone, then the full account without links. Can the reader explain the original problem, what got better, what is running or held, and what each next step changes? Formatting and agent approval do not establish human understanding or acceptance.

For a complete project illustration, consult the [frozen Dydo returning-user example](resources/returning-user-example.md). Its facts and authority belong to that dated scenario.
