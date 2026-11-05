---
name: catch-up
description: "Catch the user up on the work you hold: each deliverable's purpose, progress, current work and next steps, and what needs him. Load only when the user asks."
---

<!-- Adapted from cursor/plugins pstack/ketchup at dbf49813a39c1a5389c5e28cb8e4c1c2379e2454 (MIT). -->

# Catch-up

Give Balazs a **catch-up**: an account of the work you hold that he can understand and act on without opening a link or remembering earlier conversations.

Write it as an **inverted pyramid**. The opening alone tells the whole story; each item's bold lead tells its part; support sits beneath. Use **ASD-STE100** as guidance for plain technical English: familiar terms used consistently, active voice, short sentences, one idea per sentence. Write in the plain voice of `bro`, skipping its restating step.

## Deliverables and scope

A **deliverable** is something that works differently for Balazs once it is done: the guard stops refusing harmless searches; statechart runs on Windows. Each section of the catch-up is one deliverable. Tickets, lanes, PRs and agents appear only where they explain a deliverable's state.

The scope follows the speaker:

- **Admiral**: every deliverable in its Project.
- **Ticket captain**: its own deliverable, placed in its Project in one sentence. Split it into parts only for real parallel lanes. Tell the deliverable's story; the crew's hops, retries and review rounds are evidence for that story.
- **Any other agent**: the thing it worked on.

## Gather the facts

Keep the turn read-only: start no work, walkthrough or tribunal. Read the intent, delivery records, returns and decisions, and check live PR, CI and agent state where you can. A newer decision replaces an older entry. Keep landed code, completed proof, observed behavior and Balazs's acceptance apart. Name a missing fact or an uncertain cause once, where it matters.

## Write the opening

Start with this fixed block, one or two sentences per line:

- **Covers:** the Project by name, and your deliverable if you are a captain. Balazs opens the Project in `dydo map` beside the catch-up, so the name is enough; leave out Project links and map URLs, whose port changes on each run.
- **State:** the overall state.
- **Running:** the work actually running now, or "Nothing is running."
- **Needs you:** each decision or action that waits on Balazs, in a few words, or "Nothing needs you."

The deliverables follow, those that need Balazs first. When nothing changed and nothing needs him, the opening is the whole catch-up.

## Write each deliverable

Head each deliverable with its name and state in plain words, then these four subheadings in order, without emojis. Start each item with its cue and a bold lead that carries its meaning; put support beneath. Use only the cues below.

- **Purpose**: 🎯 what will work differently, and what triggered the work. Name the thing before its internals.
- **Recently completed**: ✅ completed outcomes. A failed review or a stopped attempt is a completed event, not a delivered fix.
- **In flight**: ✈️ work running now, with its owner, or ⏸️ held or nothing running, with the reason. An old dispatch does not prove activity.
- **Next**: each next step once, with its owner and real dependency.
  - 🤖 agent work that needs no human.
  - ❓ a question for Balazs; two questions are two items.
  - ❗ a blocking prerequisite or action, with its owner, which is not automatically Balazs.
  - 💬 a live session with Balazs, such as a walkthrough, and whether it is planned or running.

### Make decisions answerable

Before a ❓, explain what the choice is about and why it arose: the concrete behavior, an example and the evidence. Under it, give each option with what happens and what it costs. Then give ➡️ the recommendation with its reason, what a yes authorizes, and what stays held without an answer. Work an agent can do or prepare itself is 🤖, not a question. For a tribunal decision awaiting ratification, say what was decided and what ratifying or objecting changes.

### Explain every label

Give each local term its meaning where it first appears in its deliverable: an option letter, an internal name, a familiar word with a local meaning. Put longer definitions in a 📖 glossary accordion and optional evidence in a 🔎 detail accordion, written as `<details>` with a `<summary>`. With every accordion closed, the story, state, asks, options and recommendation still read whole.

A ticket or PR ID goes in parentheses after a sentence that already makes sense. Add a visual only from real evidence, and only where it explains more than the words.

## ⚠️ STOP AND READ

Use this rare section only where missing or delaying the message has a concrete, serious consequence: active security exposure, imminent significant loss, a consequential deadline, or a human check that safe continuation needs. Routine blockers and approvals stay in their deliverables.

Place it first, fully visible, opened and closed with ⚠️. Give what happened or is imminent, the consequence if missed, the exact action and its deadline, what is paused and what continues; use ❗ and ❓ inside. If continuing safely needs an answer, pause only the affected step until Balazs answers explicitly.

## Read it before sending

1. Read only the opening and the bold leads. They tell one coherent story.
2. Read the whole account without links. Balazs can say what each deliverable is for, what got better, what is running or held, and what each answer changes.
3. Keep each sentence that helps him understand or decide, and cut the rest.

For the shape at both scopes, read the [illustrative example](resources/example.md).
