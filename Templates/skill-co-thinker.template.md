---
name: co-thinker
description: An idea not ripe yet. Use when a thought, a doubt or a preference is still open, when a question surfaces that is not yet an Issue, or when a trade-off needs testing before intent hardens into a plan or a Decision Record.
emit: skill
invocation: automatic
---

# Co-Thinker

Think alongside the human until the choices in front of them are visible and the thinking has a home.

## Must-Reads

1. [about.md](../../../understand/about.md)
2. [architecture.md](../../../understand/architecture.md)
3. [glossary.md](../../../glossary.md)

{{include:extra-must-reads}}

## Boundary

Think is the stage: a raw idea arrives from the human and leaves ripe, leaves as a Decision Record,
or waits; the route is the planners' to draw. Every fact the repository, the environment or a
primary source can supply is yours to fetch before you ask the human anything; what reaches them is
what they alone have the authority to settle.

## Method

1. **Do the homework first.** Read the code, the Decision Records, the plan and the glossary that
   already cover this ground; send `research` after anything outside the repository, naming the
   Issue as its destination once one exists, and take the one-line answer it reports back. Done when
   every question still standing is one only the human can answer.
2. **Name the real choice.** Separate what is known from what is assumed, then frame the live options
   with what each one buys and what it costs, drawn by `show-me` when a shape says it faster than
   prose. Done when every option carries its trade-off and nothing assumed is dressed as settled.
3. **Put the open choices to the human.** Use `grilling`: a round at a time, each question carrying
   your recommendation; a question prose cannot settle goes to `prototype`. Done when no branch of
   the idea is left unvisited and nothing rests on a silent assumption.
4. **Fix the words.** When the exchange keeps sliding on a term, run `domain-modeling`: one name per
   concept, written into `dydo/glossary.md` where the next session finds it. Done when both sides use
   one word for one thing.
5. **Test the edges.** Try the idea against a concrete example and a counterexample, and ask what can
   be removed. Done when it survives both, or has changed shape to survive them.
6. **Recommend, then close.** Give a reasoned preference instead of handing the choice back as a list,
   and say what is settled, what is still open, and where each piece lands. Done when the human can
   see all three.

## Handoff

| What leaves | Where it lands |
|---|---|
| Hard to reverse, surprising later, a real trade-off | a Decision Record in `dydo/project/decisions/` |
| An unscheduled strategic possibility worth preserving | a Linear Issue in `FutureFeature` |
| Project intent after the homework | the human invokes `to-project`, creating a Project in `Backlog` |
| Ripe atomic Issue intent | file the Issue in `Todo` with outcome, owned paths, blockers, exact gates and base branch; then `issue-captain` |

A FutureFeature stays one until the human promotes it. Hand over what is written down, the Linear
Issue or Project, the Decision Record, the glossary entry, never this conversation.
