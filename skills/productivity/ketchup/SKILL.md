---
name: ketchup
description: "Catch the user up on what changed in this chat with a self-contained explanation of progress, unfinished work and decisions only they can make. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Ketchup

Catch the user up on what changed in this chat since the last message they wrote before `/ketchup`. Notifications and subagent results are not their messages. That message bounds the update, not the context they need: reconstruct the goal and relevant background for someone who was absent and may not have followed closely beforehand. Write in the plain voice of `bro`, and skip its restating step. Use familiar names and concrete examples; ticket IDs and links are optional provenance.

## Gather what changed

Keep this turn read-only, except for visuals you make for the reply from what you already have. Do not change code, start an app or a job, or post anything to make them. Collect your own work since that message, reports from subagents, and messages from anyone else in this chat. Verify accessible live facts yourself, including the current state of relevant PRs, CI runs and agents. Label historical observations and anything not checked; check each number against its source and acknowledge corrections. A report that a run finished does not establish that a deployed system works.

An action item is a decision or step only the user can take, such as a reserved product choice, an approval or access you lack. Include genuine outstanding items even if raised before their last message. Keep fact checks, diagnosis and preparation with their agent owner. Where approval will be needed later, explain the remaining preparation and bring a concrete proposal when ready; this catch-up grants no new execution authority.

## Write the reply

Write the shortest reply that stands alone. Let useful detail determine length and layout. The user must understand it without opening links, reading a separate visual or remembering ticket IDs.

Start with the goal, useful outcomes and overall state. Explain what got better, what remains unfinished and why it matters. Distinguish merged implementation from proven live operation, and ready work from parked work. Give established causes where known and uncertainty where evidence stops. Group by user outcome rather than inventorying every agent or run. If nothing changed and nothing waits on the user, say so and stop.

Name the agent-owned next work and what it can resolve. Separate decisions ready now from later human steps and optional parked choices; routine checks and internal holds are not user blockers.

For each real user decision, explain the concrete problem, the alternatives and their practical consequences, your recommendation with its reason, and what answering would authorize. Say why the agent cannot settle it and what happens without an answer, including which work can continue. If nothing needs the user, make that clear.

## Visuals

Use a visual when it makes a change, comparison or causal relationship easier to understand. It may reinforce essential text; avoid repeating detail that adds no clarity. Rendering a visual is not proof of comprehension.

Use existing screenshots or video for visible changes, or draw diagrams and charts from verified facts. Never invent a number or present a generated screen as a capture. Link only artifacts that exist.

## Check comprehension

Read the reply without its links or visual. Can a returning reader explain the goal, what got better, what still does not work and why, what the agents will do next, and what each answer authorizes? Repair missing context or unexplained jargon before delivering. Length, formatting and a completed checklist alone do not establish usefulness.

For a worked example of a long absence with mixed blockers and decisions, consult the [historical returning-user example](resources/returning-user-example.md). Its project facts and permissions belong only to that scenario.
