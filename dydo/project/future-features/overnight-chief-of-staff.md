---
title: Overnight Chief of Staff
area: project
type: concept
status: promoted
linear-reference: https://linear.app/bodnar-balazs/issue/DYD-351
---

# Overnight Chief of Staff

**This is a proposal, not a skill.** It records what one real overnight run taught, and what a future
`overnight` mode for the `chief-of-staff` role would need. Nothing here is a current dydo capability,
and no skill should be written from it until the human decides it is ready.

## The experiment

On the night of 2026-10-18 the human went to sleep and left the chief of staff in charge of LC's four
active Projects (Test optimisation, Tools folder cleanup, Automation, TeasePlayer) from about 21:00
to 08:00 Budapest time. The arrangement:

- The chief of staff messages the admirals; admirals run issue-captains; captains run crew.
- Smaller decisions are made by the responsible agent: read the spec, the intent and the Decision
  Records, ask "what would the human choose?", record it as `DECIDED`, and go.
- Atomic PRs self-merge when they have a fresh independent PASS, green gates and a clean merge.
  Project landings, releases, security-weakening changes and anything irreversible wait for him.
- A 45-minute scheduled check-in keeps admirals awake, and a morning report closes the night.

### What happened

- **Work got done.** 19 PRs reached Ready to Merge with independent review. All 13 open CI findings
  got a verdict. Admirals recorded about 20 `DECIDED` calls, filed new Bugs, and correctly turned
  six security or irreversible calls into Questions instead of deciding them.
- **Nothing merged.** Claude Code's auto-mode classifier refused every admiral's `gh pr merge` as
  "Merge Without Review". The human's permission reached the admirals as a Linear comment written by
  the chief of staff, and the classifier does not treat that as the human's authority. Every admiral
  stopped there and did not work around it. That was the right behaviour, and it still left 19
  PRs for the human to click in the morning.
- **One session couldn't write to Linear all night** (classifier: "External System Writes"), so its
  follow-ups lived only in its thread.
- **Two long idle stretches**, both caused by plumbing rather than by judgment:
  - One admiral's thread ran in full-access mode while the chief of staff ran in auto. T3 refuses
    to message a thread whose mode is broader than the sender's, and a mode change only takes
    effect once the human sends a message in that thread. That cost about two hours.
  - A captain launched as a top-level thread finished and handed work back, but top-level threads
    don't notify anyone. The hand-back sat unread for four hours until a check-in noticed it.
- **One self-inflicted stall.** An admiral set a Linux idle-load limit of 0.35 cores without
  measuring the host, which idles at about 0.46. No timing trial could start all night. Every
  check-in saw the admiral "active", and none noticed that nothing was progressing.
- **One collision prevented.** Two Projects were about to edit the same hook file. The chief of
  staff held one Issue until the other stack finished, then lifted the hold.
- **The morning still needed a lot of the human.** Nineteen merges in stack order, a release tag, a
  tool install, hook re-trust in two Codex homes, four live probes and six Questions. The fix was a
  single wizard script, written after the fact, that did all of it with pinned SHAs and one
  confirmation per group.

## Lessons

1. **Authority has to reach the agent that acts, in a form its gate accepts.** A ruling posted in
   Linear is good evidence of intent, and it doesn't grant the permission. Before the human leaves,
   every permission the night depends on has to exist as a real permission: a settings allow rule
   (for example `gh pr merge` and the Linear comment tool), or a go message the human types in each
   admiral's own session.
2. **Preflight beats discovering the problem at midnight.** Each capability the night depends on
   should be tested once while the human is still awake. That means messaging every admiral thread,
   a Linear write from every session, and a dry run of the merge path. The chief of staff reports
   "all green" or names what's missing before the human goes to sleep.
3. **Use the same runtime mode everywhere.** Put every thread in the same permission mode, and have
   the human send one message in each after any mode change, because the change only takes effect
   then.
4. **Children must be able to report back.** Start captains as delegated tasks that notify their
   parent, not as top-level threads. If a top-level thread is unavoidable, the hand-off names who
   reads it and the check-in sweep reads every open hand-off.
5. **Check for progress, not activity.** A check-in should ask "what changed since last time?" A
   busy admiral with a frozen measurement, an unanswered hand-back or a PR that hasn't moved is as
   stuck as an idle one. A one-line STATE per admiral (last change, what it waits on, next action)
   makes that easy to compare between check-ins.
6. **Measure before you set a limit.** A threshold on a shared resource gets a baseline measurement
   first. Otherwise the limit quietly stops all work.
7. **Shared hosts need rules from the start.** The Linux CI host needed an amendment midway:
   targeted tests only while a CI job runs, never touch the CI service or its state, work in
   `~/work/<issue>`, and record every sudo. Rules like these belong in the original rulings.
8. **One merger per base branch.** Each PR was checked mergeable against master on its own. Four
   admirals merging into master at the same time would race. A single merge queue per base branch,
   owned by one agent, should re-check each PR after the one before it lands.
9. **The morning deliverable is a command, not a list.** The report should come with a generated
   landing wizard: PRs pinned to their reviewed head SHAs, stack order, retargeting after each
   parent merges, releases behind a confirmation, and the open Questions asked with the
   recommendation as the Enter default. The chief of staff keeps it current through the night.
10. **Withdraw Questions that the night already answered.** One Question was solved by a PR the
    same night without weakening anything. Admirals should withdraw such Questions themselves so the
    morning queue only holds real decisions.
11. **Staff, not line.** The chief of staff never merged, implemented or decided anything for a
    Project. It routed, nudged, mediated collisions and fixed Linear bookkeeping (state, labels).
    That boundary held and should stay.
12. **Questions to the human must carry their context.** Each Question says what it blocks, the
    facts, and the options with a recommendation. Ask it in the conversation, not as a bare terminal
    prompt.

## What the mode would look like

These are the phases a future skill could take. They are only a sketch.

- **Agree.** Restate the human's intent and get explicit choices: merge scope, which Projects,
  what stays his, report time.
- **Rulings.** Post the same numbered rulings on every Project: keep working AFK, decide like the
  human and record `DECIDED`, the merge conditions, what stays his, a stop rule, wind-down times,
  and shared-host rules.
- **Preflight.** Run lesson 2's checklist. The human leaves only after it is green, or after he has
  accepted named gaps.
- **Watch.** Run a scheduled sweep. Each pass reads every admiral's STATE and the hand-offs, wakes
  idle or stuck admirals with specific work, mediates file collisions across Projects, and keeps the
  landing wizard current.
- **Wind down.** Stop new merges at the agreed time, collect STATE lines, delete the schedule.
- **Report.** Write the morning report: what merged, every `DECIDED` call, the Questions with
  recommendations, who sat idle and why, anything paused, and the one command that lands the rest.

## Open questions for the human

- Should self-merge authority be a standing settings rule, or granted per night?
- Should the merge queue belong to the chief of staff (staff touching line work) or to a dedicated
  merge captain per base branch?
- How far should the generated morning wizard go: merges only, or releases and installs too?

## Rationale

The run showed that agents decide well overnight when the rulings are clear. What stopped the night
from merging anything was plumbing: permissions, thread modes and notifications. A preflight and a
generated landing command would turn that night's 19 waiting PRs into a morning that needs one
command.

## Related

- [Routine Admiral](./routine-admiral.md) - The cadence-driven admiral this mode would supervise
- [DR 050 - Officers, Crew and Skills: Hats Retired](../decisions/050-officers-crew-and-skills-hats-retired.md) - Where officer roles such as the chief of staff sit
- [DR 051 - Captains by Default, the Admiral on Invocation](../decisions/051-captains-by-default-admiral-on-invocation.md) - How admirals and captains are started
- [Working-Tree Contract](../../guides/working-tree-contract.md) - Branch and worktree rules that every overnight agent still follows
