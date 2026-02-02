I started rewriting the @README.md, and I realized something. It's a given that I said that the core files have to be refined and
prompt engineered masterfully. But it dawned on me that there even though we have some explicitly defined workflows like --feature,
--task, --inbox, they are not thoroughly specific. Take feature for example: start as interviewer, gather context (which is missing
form the interviewer mode file by the way, so is the fact that it should note down noteworthy decisions, but not everything...), ask
the user to nail down intent. Okay. What's next? Why is it open that the specification gets passed onto another agent or the same agent
may switch modes to write the plan as well. This entire thing is about context management. Including all the relevant details and none
irrelevant. It's a superficial definition. These workflows should be rock-solid, well-defined. Now they're too hand wavy. The most
important rule, what we already have implemented I think is that the same agent doesn't review it's own code. But can you help me
better investigate this part of the project? I think it's pretty important and maybe among the last things we need to get right.
⎿  Read README.md (173 lines)

● You're hitting on something critical. The workflows are the heart of the system - without well-defined, specific guidance, agents will flounder. Let me investigate what we actually have.

● Explore(Investigate workflow templates)
⎿  Done (13 tool uses · 42.8k tokens · 1m 7s)

● Excellent analysis. Here's the summary of what we found:

Workflow Definition Quality: 6/10 - "Structurally Sound, Operationally Hand-Wavy"

Critical Gaps (Affect Correctness)
┌──────────────────────────────┬─────────────────────┬──────────────────────────────────────────────────────────────────────┐
│            Issue             │      Location       │                               Problem                                │
├──────────────────────────────┼─────────────────────┼──────────────────────────────────────────────────────────────────────┤
│ No mode auto-detection       │ agent-workflow.md   │ "Try to figure out" is not a procedure                               │
├──────────────────────────────┼─────────────────────┼──────────────────────────────────────────────────────────────────────┤
│ Plan not mandated            │ mode-code-writer.md │ Doesn't say "READ THE PLAN FIRST"                                    │
├──────────────────────────────┼─────────────────────┼──────────────────────────────────────────────────────────────────────┤
│ Agent tracking undocumented  │ everywhere          │ How does system know who was code-writer for self-review prevention? │
├──────────────────────────────┼─────────────────────┼──────────────────────────────────────────────────────────────────────┤
│ "2 failed reviews" undefined │ process files       │ Consecutive? Per task? Lifetime?                                     │
├──────────────────────────────┼─────────────────────┼──────────────────────────────────────────────────────────────────────┤
│ Phase re-entry unclear       │ process files       │ Can you go back to interviewer mid-coding?                           │
└──────────────────────────────┴─────────────────────┴──────────────────────────────────────────────────────────────────────┘
Your Specific Concerns Validated

1. Context gathering in interviewer - The 5-question framework exists but lacks:
   - How deep to explore docs/code before asking questions
   - When context is "enough"
   - What makes requirements complete
2. Decision documentation - Only co-thinker mentions it! Missing from:
   - Interviewer (should note key decisions from requirements)
   - Planner (architectural choices not documented)
   - No traceability from decision → implementation → review
3. Same agent vs dispatch - Interviewer says "Option A: Dispatch" vs "Option B: Transition yourself" with zero guidance on which to choose

The Handoff Problem
┌─────────────────────────┬───────────────────┬────────────────────────────────┐
│         Handoff         │  Format Defined?  │       Criteria Defined?        │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ Interviewer → Planner   │ ✓ Brief template  │ ✗ "When complete enough"       │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ Planner → Code-Writer   │ ✓ Plan template   │ ✗ No validation checklist      │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ Code-Writer → Reviewer  │ ✗ No format       │ ✓ "dydo task ready-for-review" │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ Investigation → Planner │ ✗ Never specified │ ✗ Never specified              │
└─────────────────────────┴───────────────────┴────────────────────────────────┘
  ---
This is a big refinement job. Would you like to:
1. Define the core principles first (what makes a handoff "ready"?)
2. Pick one workflow (e.g., --feature) and make it rock-solid as a template for the others?
3. Something else?
   Decision Documentation Criteria

You've nailed it: Document the reasoning, not the conclusion. If the reasoning was trivial, there's nothing to preserve.

A useful heuristic: "Would future-me (or another agent) wonder why we did it this way?"

- "We chose PostgreSQL over MongoDB because our data is highly relational and we need ACID transactions for payment processing" → Worth documenting
- "We put the UserService in the Services folder" → Not worth documenting (obvious from conventions)

This could be a simple rule in the templates: "If you explored multiple options or the human pushed back on your initial suggestion, document the final decision and the reasoning."

  ---
Saying It Out Loud

Yes. This is important and missing. The agent shouldn't silently set state - it should announce:

"I'm interpreting this as a feature implementation workflow (interview → plan → code → review) because you're asking for new functionality that will touch multiple files. Starting in interviewer mode. Let me know if you had something simpler in mind."

This gives the human a chance to course-correct before the agent goes down a path. Simple addition to agent-workflow.md.

  ---
Fresh Eyes vs Context Pollution

This is a genuinely interesting insight. There are two types of "context":

1. Signal - Relevant architecture, requirements, decisions, the plan
2. Noise - Dead ends explored, irrelevant files read, abandoned approaches

When an agent has accumulated a lot of noise, a handoff with a clean summary is better than continuing with polluted context.

Proposed rule: If your exploration/investigation took more than N steps or read more than M files, produce a handoff summary:
- Key findings (what matters)
- Relevant files (what to read)
- Dead ends (what NOT to pursue - this is valuable too!)

The receiving agent starts fresh but with curated signal, not raw noise.

  ---
Identity Lifecycle - The Hard Questions

This is where the current system has real gaps. Let me think through the scenarios:

Normal flow:
1. Agent claims identity → dydo agent claim Adele
2. Agent works (state tracked in agent-state.md, session in .session)
3. Agent releases → dydo agent release
4. Session file deleted, state cleared

But what about:

Scenario A: Context compaction happens mid-session
- Claude Code compacts the conversation
- Agent loses memory of claiming identity
- Tries to edit a file
- dydo guard blocks because... wait, does it?

Actually, let me think. The guard checks the process ID in .session files. If the terminal is still the same process, guard should still work. The agent might not remember it claimed, but the session file proves it.

BUT - the agent won't know its role, current task, or what it was doing. It's like waking up with amnesia but your ID card still works.

Scenario B: Terminal closes unexpectedly
- Session file becomes stale (PID no longer running)
- dydo check warns about stale sessions
- dydo clean can clear them
- But no automatic recovery

What's missing:
1. Post-compaction hook - Does Claude Code have one? I don't think so. But...
2. Guard as re-orientation - When guard runs, it could output not just "allowed/blocked" but also "By the way, you are Adele, role: code-writer, task: auth-feature"
3. State recovery prompt - The agent-workflow.md could say "If you're disoriented, run dydo whoami to recover your identity and state"

Your question about hooks: I don't believe Claude Code has a post-compaction hook. But the guard hook runs on every tool use. So the first time a confused agent tries to do anything, guard runs and could provide orientation.

Should guard output identity info on success, not just silence? That might help.

  ---
Guard as Guardrail for Lost Agents

Currently guard outputs nothing on success (exit 0, silent). On block, it explains why.

Proposal: Guard could always output a brief status line:
[dydo] Adele (code-writer) on task 'auth-feature' - action allowed

This way, every time the agent does something, it's reminded of who it is. If compaction happened, the agent sees this and knows its identity without explicitly asking.

Too noisy? Or useful orientation?

  ---
Parallel Agents / Review Panel

This is a fascinating idea. Let me think about it seriously:

The pattern:
- Task ready for review
- Dispatch to 2-3 reviewers simultaneously
- Each reviews independently (avoids groupthink)
- They share findings via inbox
- One agent (or human) synthesizes into final verdict

Why this could work:
- Different agents catch different things (coverage)
- Forces explicit reasoning (you have to explain to peers, not just decide)
- Mimics real code review (multiple approvers)
- Inbox already exists as communication channel

Challenges:
- Cost (3x the review tokens)
- Coordination (who synthesizes? when is consensus reached?)
- Conflicting feedback (reviewer A says "too abstract", reviewer B says "not abstract enough")

When it might be worth it:
- Critical code (auth, payments, security)
- Large changes (architectural shifts)
- When single reviewer has failed twice (escalation path)

A lighter version: Instead of full parallel review, do sequential review with handoff notes. Reviewer 1 reviews, documents findings in inbox. Reviewer 2 reads Reviewer 1's notes, then reviews with that context. Catches what Reviewer 1 missed, avoids redundant comments.

This is cheaper and still gets "multiple eyes" benefit.

  ---
Synthesis: Emerging Principles

From this discussion, I'm seeing some principles emerge:

1. Document reasoning, not conclusions - Decisions worth preserving have non-trivial reasoning
2. Announce, don't assume - Agent should verbalize its interpretation of ambiguous situations
3. Clean handoffs over polluted continuity - If context is noisy, summarize and hand off
4. Guard as orientation, not just enforcement - Help lost agents find themselves
5. Parallel review for critical paths - Multiple eyes when stakes are high
6. Identity survives compaction, memory doesn't - Build recovery into the workflow

Does this feel right? What would you add or challenge?