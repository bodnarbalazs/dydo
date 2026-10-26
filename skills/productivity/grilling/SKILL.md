---
name: grilling
description: Grill the user relentlessly about a plan, decision, or idea. Use when the user wants to stress-test his thinking, or uses any 'grill' trigger phrases.
---

<!-- Adapted from mattpocock/skills grilling at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Grilling

Interview the user relentlessly until you reach a shared understanding. Map this as a **design
tree**: every decision branches into the decisions that hang off it.

Work the tree in **rounds**. The **open decisions** are those whose prerequisites are already
settled: the questions you can ask _now_ without guessing at answers you haven't heard yet. Ask all the
open decisions in one round: number each question and give your recommended answer. Then wait for
the user's answers before the next round.

Format a round like so:

```
❓ **Q1** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>

---

❓ **Q2** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>
```

Each round the user answers reshapes the tree: settled decisions open new ones and
unblock questions that depended on them. Recompute the open decisions and ask the next round. A question
whose answer depends on another question still open in this round belongs to a _later_ round, not
this one.

Finding _facts_ is your job, never the user's. When an open question needs a fact from the
environment (filesystem, tools, etc.), dispatch a sub-agent to find it; don't ask the user for
anything you could look up yourself. Don't block on it: a running exploration is an unsettled
prerequisite, so only the questions downstream of it wait for the sub-agent to report; ask the rest
of the open decisions now. The _decisions_ are the user's: put each to him and wait.

The session is done when no open decision is left: every branch of the design tree visited, nothing
left silently assumed. Do not implement the plan until the user confirms you have reached
a shared understanding. Record settled terms and qualifying decisions inline through domain-modeling.
