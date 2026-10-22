# Tribunal

Three members, Popper-style: each position stands only while it survives the strongest challenge.
The raising officer is a full member and also the clerk. The clerk is mechanical: it forwards text
verbatim, tracks turns and claims, and tallies ballots; its view enters only through its own turns.

## Phases

Set every phase's deadline when you open the case file. Append each finished phase to the log before the next.

1. **Case file.** Research the case law, sending `research` or `scout` sparingly where a source is
   out of reach. Write the case file below. Done when it holds every option, unranked.
2. **Seal.** Write and log your own sealed position before any judge starts.
3. **Dispatch.** Start two fresh judges as T3 `delegate_task` children, one Claude and one Codex,
   each with the strongest model and effort set explicitly. Send each the judge brief with the case
   file. Done when both return sealed positions and their results show the model and effort asked
   for.
4. **Rounds.** Send every member the same bundle: the sealed positions and the last round's turns,
   always queued, never steered. Write your own turn before reading theirs. Each turn raises the
   strongest material objection to another member's position, or says why none exists, and answers
   the strongest outstanding challenge to its own. A concession quotes the evidence or restates the argument it yields to.
5. **Claims.** A factual claim names its check; the other two members run it independently and
   report confirmed, refuted or inconclusive. A reframe states exactly what it changes, and the
   judges may object. No class is lowered over an unresolved objection: the strictest class stands.
6. **Close.** Stop when a round changes nothing and no claim is pending, and after 4 rounds at most,
   evidence rounds and reframes included.
7. **Ballot.** List every option still standing, each as one exact action quoted from the final
   positions. Each member names one of them, verbatim. Tally by the verdict table in the skill.

A missing or stalled member gets one retry. After that the tribunal is incomplete and ends without a
decision; never swap a vendor silently. An experiment goes to a proof-only `code-writer` in an
isolated worktree, its effects contained; members otherwise only read.

## Case file

Judges get this and nothing else from your session:

- the call, in one sentence; the objective; the constraints
- the options, unranked
- the primary evidence, quoted with its source
- each premise, stated as a claim with its source
- your class for the call, and whether he is present or away

## Sealed position

- the member's own class for the call
- the call, in its own words
- an option the file did not list, or why none exists
- the premises it checked, and what it found
- its answer: the action he would take
- what would change its mind

## Judge brief

> You are a judge on a `what-would-balazs-do` tribunal. Load that skill and follow its
> `resources/tribunal.md`. Answer as Balazs would, from his own words only, quoting each source. You
> may send `research` or `scout` subagents after evidence. Return your sealed position in the
> skill's form by <deadline>. Later rounds arrive as queued messages; answer each with one turn. As
> a judge, use the Sealed position form and phases 4-7 as a member; phases 1-3, the clerk duties and
> writing the ballot list are the clerk's. Never dispatch judges.
>
> Case file: <case file>

## Log

A Linear Document `Tribunal log - <KEY>`, attached to the Question. Checkpoint `Dispatch` on every
dispatch and retry, and append each round once complete, so a fresh clerk resumes without
dispatching a judge twice or resetting a retry.

```markdown
## Case file
## Dispatch
<member>: task <id>; deadline <time>; retries <n>; pending: <turn or none>
## Sealed positions
<member> - <role/model and effort, as the result shows them>: <position>
## Round <n>
<member>: <turn, verbatim>
## Claims
<claim> | <check> | <member>: confirmed, refuted or inconclusive
## Ballot
<option, exact text>
<member>: <option, exact text>
## Verdict
result: <decision, prepared Question, or no decision>; stop: converged, cap or incomplete; undo: <steps>
```
