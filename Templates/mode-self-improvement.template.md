---
mode: self-improvement
description: Turns recurring friction into one small, durable harness improvement without expanding Issue scope.
emit: skill
---

# Self-Improvement

Your job: turn recurring friction into one small, durable improvement to the harness.

Here, the harness means agent prompts, skills, nudges, hooks, agent-workflow documentation and
process surfaces, and harness implementation code. It excludes the product being built.

## Mindset

Kaizen is continuous improvement through small changes. `1.01^365 ≈ 37.8` illustrates
compounding; it is not a promise, a metric, or a reason to manufacture changes.

## Trigger

Use this skill when the same agent-harness failure, correction, workaround, or avoidable friction
appears at least twice in the available evidence, or an existing canonical harness record already
identifies it as recurring. Product behavior never triggers this skill. A one-off harness
inconvenience is not enough. A single severe harness defect follows the ordinary Linear Issue path only
when the current Issue authorizes that record; otherwise report it to the human.

## Method

1. **Establish evidence** — Name the repeated symptom, occurrences, affected workflow, and likely
   root cause. If recurrence is unsupported, stop.
2. **Deduplicate** — Search existing Linear Issues, decisions, guides, pitfalls, prompts,
   skills, nudges, and hooks. Prefer an existing canonical surface.
3. **Choose one lever** — Select exactly one smallest durable change in this order: canonical prompt or skill wording; a warn-level nudge for a recognizable risky action; a hook only when action-time guidance or enforcement is demonstrably required; harness implementation code only when the earlier layers cannot express the behavior.
4. **Classify, then check authority** — Choose the narrowest destination below. Create or modify
   it only when the current Issue explicitly includes that edit and the current role, plan, and
   normal reviewed workflow permit it. Otherwise create or modify nothing: report the evidence
   and suggest exactly one destination/change to the human.
5. **Define verification and rollback** — State what recurrence should stop, how to test that
   outcome, and how to remove the change if it creates noise or unintended constraints.

## Destinations

These are harness classifications, not standing authorization. None routes product work.

- Observed defect → Linear Issue.
- Schedulable improvement not yet accepted → Linear Issue in the team's appropriate unstarted state.
- Accepted, non-obvious policy → decision.
- Stable operational guidance → the narrowest existing guide or pitfall.
- Authorized trivial prompt or nudge repair → its canonical source, then its normal compiler or
  sync gate.
- Project facts, incident state, and temporary workarounds → never memory; route or retire them
  only when authorized.

## Boundaries

- This skill grants no authority. The current role, human request, Issue, Project plan, and reviewed workflow
  still govern every edit, including record creation or modification.
- Do not propose or perform product-feature or product-code changes, including benevolent or otherwise authorized adjacent product work; kaizen here applies only to the agent harness and its documentation and process surfaces.
- Do not widen the current Issue, fix adjacent problems, create a generic doctrine record, or make
  more than one proposal for the same pattern.
- Do not edit generated artifacts; edit their canonical template and compile normally.
- Do not change global or user-level prompts, hooks, settings, memories, or policy without
  explicit Issue authority and the normal review or approval path.
- Do not escalate a reminder into enforcement, or a warning into a block, without recurrence
  evidence, proportionality, and the normal review or approval path.
- Do not create a recursive self-improvement loop. Apply this method once to the observed pattern,
  then return to the Issue.
- If no small credible improvement survives these checks, report the pattern and stop rather than
  inventing machinery.
