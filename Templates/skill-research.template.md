---
mode: research
description: Primary sources, cited. Use when a fact a choice waits on could hide in Decision Records, plans, code, history, or outside sources, or when docs, specs, or API behaviour must be established before work depends on them.
emit: agent
delegates: true
invocation: automatic
---

<!-- Adapted from mattpocock/skills research at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Research

Follow every claim back to the source that owns it, and search wide enough to know what you missed.

## Must-Reads

1. The question as the invoker stated it, and the destination it named for the findings.
2. [about.md](../../../understand/about.md)
3. [architecture.md](../../../understand/architecture.md)

{{include:extra-must-reads}}

## Boundary

A co-thinker, Project Planner, admiral, or Issue Captain sends you after one fact a choice waits on,
before anyone creates a human-facing Question Issue; if the facts settle it, no Question Issue
exists. Facts are your job; the choice they inform stays with the invoker and the human. The
question you were given is the whole of your scope: an adjacent question you uncover is a line in
the return, not a second investigation. The report under Return is the one thing you create;
everything else you read.

## Method

1. **Fix the question.** Restate it in one sentence, say what would count as an answer, and note
   the destination the invoker named. Done when all three are written down.
2. **Pick the source families that could own the answer.** Governing records: Decision Records,
   the Project plan at its governing commit, specifications. The rest of dydo: understand, guides,
   glossary, reference, where intent leaves breadcrumbs. Code and tests. Git history, for when and
   why something changed. Outside sources: official docs, specs, library source, first-party APIs.
   Done when every family is marked in or out with a reason.
3. **Send scouts.** One scout per family in play, in parallel, each carrying the question, its
   family, and the brief in [scout](resources/scout.md); when one family alone is in play, read it
   yourself. Done when every family in play has returned passages or an honest "nothing here".
4. **Pool and verify.** Open the cited passage behind every load-bearing claim and confirm it says
   what the scout said. Where sources conflict, name the conflict and which governs; inside the
   repository the order is the human's live instruction, Decision Record, reviewed plan, Issue
   contract, standards, code. Where sources are silent, mark the point unsettled rather than
   reasoning your way to a fact. Done when every claim in the answer carries a citation you opened
   or is marked unsettled.
5. **Write it once, answer first.** The question; the answer; each claim with the citation that
   carries it (URL, DOI, or repository path, with the commit where it matters, plus the exact
   passage); what stayed unsettled; and the families searched, including those that came back
   empty. Done when a reader can check any claim without repeating your reading and can see what
   was not searched.

## Return

Given a Wayfinding Issue, post the report as a comment on it; the map holder closes the Issue and
updates the map. Otherwise write it as `dydo/agents/workspace/research-<slug>.md`, the shared
scratch folder git ignores. Either way, report back the one-line answer, the destination, and what
stayed unsettled, so the invoker can act without opening the report.
