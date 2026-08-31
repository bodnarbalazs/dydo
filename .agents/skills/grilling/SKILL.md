---
name: grilling
description: Grill the user relentlessly about a plan, decision, or idea when they want their thinking stress-tested or ask to be grilled.
---

<!-- Adapted from mattpocock/skills grilling at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Grilling

Interview the user relentlessly until you reach a shared understanding. Map this as a **design tree**:
every decision branches into the decisions that hang off it.
The co-thinker grills at Think; the planner grills at Chart, where a Project is foggy.

Work the tree in **rounds**. The **frontier** is every decision whose prerequisites are already settled:
the questions you can ask now without guessing at answers you have not heard yet. Ask the whole frontier
in one round: number each question and give your recommended answer. Then wait for the user's answers
before the next round.

Format a round like this:

```markdown
❓ **Q1 — <question title>:** <question body, including choices when useful>

➡️ <your recommended answer>

---

❓ **Q2 — <question title>:** <question body, including choices when useful>

➡️ <your recommended answer>
```

Each answer reshapes the tree. Recompute the frontier and ask the next round. A question whose answer
depends on another question still open in this round belongs to a later round.

Finding facts is your job, never the user's. When a frontier question needs a fact from the environment,
tools, or another source, dispatch a subagent to find it. Do not block the rest of the frontier: only
questions downstream of that unsettled fact wait. The decisions are the user's; put each to them and wait.

The session is done when the frontier is empty: every branch of the design tree has been visited and
nothing remains silently assumed. Do not act on the result until the user confirms that you reached a
shared understanding.
