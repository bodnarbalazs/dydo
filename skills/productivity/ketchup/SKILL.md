---
name: ketchup
description: "Catch the user up on what changed in this chat with a self-contained explanation of progress, unfinished work and decisions only they can make. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Ketchup

Give a returning user the project story: its state, progress, blockers and when they are needed. Use an **inverted pyramid** at both report and area level: essential meaning first, then progressively more detail, so stopping early leaves a coherent understanding.

The update covers this chat since the user's last message before `/ketchup`; notifications and subagent results are not their messages. Reconstruct enough earlier context for someone who was not following closely. Write in the plain voice of `bro`, skipping its restating step. A catch-up reports the work; a walkthrough lets the user inspect what was built against their understanding and taste. Neither implicitly invokes the other.

## Establish the state

Keep this turn read-only, apart from reply visuals made from available evidence. Do not change code, start an app or job, or post elsewhere. Collect the work and relevant reports, then verify accessible live facts yourself, including PR, CI and agent state. Check numbers against their sources and acknowledge corrections. Mark historical observations, unchecked state and uncertain causes. Merged code, completed runs and proven live operation are different claims.

Identify what only the user can decide or do, including genuine outstanding items from before their last message. Keep readbacks, diagnosis and preparation with the agents. Existing holds and authority still govern; a catch-up grants no new permission.

## Lead with the project story

Open with the goal, what the project stands for, its current focus and the essential state: what got better, what remains unfinished, and where attention is needed. Distinguish a decision ready now from a later human step or optional parked choice. If nothing changed and nothing waits on the user, say so and stop.

Then group the detail into user-recognizable problems or outcomes. One area may span several captains or tickets. Order areas by consequence and attention, so the user reaches important decisions and blockers early. Use a plain headline that names the outcome and its attention state, such as “Test evidence — your decision is ready” or “Installation — human step later.”

## Give every area the same shape

Use these five labels and emoji cues in this order, separated by blank lines. Each area is its own inverted pyramid: the first section carries its purpose, result and attention state; later sections explain them. Write connected prose, using lists only where alternatives or several changes are easier to scan.

**🎯 Purpose & now** — What this area is, why it matters, and its short current summary. A reader who stops here should understand the outcome and whether it needs them.

**⏮️ Before** — The concrete previous behavior or problem, with an example where useful.

**🛠️ Work & decisions** — What changed and why, which decisions were made, and the resulting state. Explain established causes and where the evidence stops; distinguish merged changes from proof still owed.

**➡️ Next** — The agent-owned next steps and what they will resolve, including preparation for any later human action. If the area is complete, say what, if anything, remains.

**🙋 Attention & blockers** — Say explicitly when nothing is needed from the user. For a real decision or human-only step, explain the concrete choice, alternatives, practical consequences, recommendation and reason, why the agent cannot decide or act, what an answer authorizes, and what happens without one. Label later steps and optional parked choices accordingly. For an external or agent-owned blocker, name its owner and the missing prerequisite; routine preparation is not a user ask.

## Make it readable on its own

Keep essential context inside each area, even when a short reminder repeats the opening. Ticket IDs, PRs and links are subordinate provenance; the user should not need them to understand the story or choose. Let useful detail set the length, without a word quota. The repeated labels provide navigation, not a roster to fill with bookkeeping.

Use additional visuals when they clarify a change or relationship. Existing captures show visible changes; diagrams and charts use verified facts. Never invent numbers or present a generated screen as a capture. Essential meaning stays in the reply and links point only to existing artifacts.

Read just the opening and area summaries, then each area without its links or extra visuals. Can the reader explain the goal, improvements, unfinished work and causes, agent next steps, and what each answer authorizes? Repair missing meaning or weak hierarchy before delivering. Formatting and agent approval do not substitute for the user's understanding and taste.

For a complete illustration, consult the [historical returning-user example](resources/returning-user-example.md). Its project facts, decisions and permissions belong only to that scenario.
