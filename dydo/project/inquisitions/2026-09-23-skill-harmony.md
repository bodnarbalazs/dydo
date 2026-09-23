---
area: project
type: inquisition
---

# Inquisition: harmony of the 3.0 skill tree

[DYD-235](https://linear.app/bodnar-balazs/issue/DYD-235/inquisition-harmony-of-the-30-skill-tree-no-two-skills-claim-different)
— completed audit: no two skills in the 3.0 tree may claim different things about the same fact.

- **Audited SHA:** master `313088e2`
- **Audit branch:** `inquisition/skill-harmony` (pushed, never merged)
- **Date:** 2026-09-23
- **Captain:** issue-captain/model: claude-opus-5-5
- **Inquisitors:** four, each `general-purpose` + `inquisitor` (requested model claude-opus-5-5), one
  per Part below, each applying all five lenses.

## Scope

**Parts:**

1. Officers to crew — admiral, chief-of-staff, issue-captain against every crew skill.
2. Crew to crew — code-writer/reviewer, docs-writer/reviewer, inquisitor/issue-captain,
   research/scout, project-planner/admiral+reviewer.
3. Non-role skills against the roles — `engineering/` and `productivity/` skills (wayfinder,
   grilling/grill-me, co-thinker, handoff, walkthrough, to-project, prototype, wizard,
   self-improvement, writing skills, codebase-design, domain-modeling, diagnosing-bugs,
   improve-codebase-architecture, teach, bro, show-me).
4. Skills against the authority docs — status/label/Type/Mode/path/template/return-line/gate claims
   checked against the standard, working-tree contract, control-flow, glossary and DRs.

**Lenses** (applied in every Part): (a) direct contradiction, (b) producer/consumer mismatch,
(c) dangling reference, (d) vocabulary drift, (e) ownership gap or overlap.

**Counts:** 54 raw findings (P1-01..12, P2-01..12, P3-01..14, P4-01..16) → 46 confirmed, 8 refuted
(one partly) → 15 deduplicated root items: 12 Bugs + 3 Questions.

**Authority rule:** the human's recorded DYD-230 decisions outrank a DR (issue-captain precedence);
where they conflict with an accepted DR, or authority docs disagree with each other, the item went
to a Question, not a Bug.

## Root findings

| Root | Parts | Filed | Side the authority supports |
|---|---|---|---|
| Hop gate scale, and hardening vs tightening pass | P1-01, P1-10, P2-01, P2-07, P4-01, P4-14 | [DYD-236](https://linear.app/bodnar-balazs/issue/DYD-236) | code-writer's full suite per hop and "tightening" (DYD-230 decisions 1 and 3, control-flow:50) |
| Planner says the code-writer makes the Issue exact | P1-03, P2-02, P4-03 | [DYD-237](https://linear.app/bodnar-balazs/issue/DYD-237) | captain (glossary:119, standard:172) |
| Every FAIL goes to a code-writer, even on docs | P1-02 | [DYD-238](https://linear.app/bodnar-balazs/issue/DYD-238) | author of the change's kind (standard:79-80) |
| No one creates an atomic Issue's final Merge | P1-07, P4-07 | [DYD-239](https://linear.app/bodnar-balazs/issue/DYD-239) | captain creates it (control-flow:586-590, standard:130-132) |
| Inquisitor: one lens, test-only hypotheses | P1-11, P2-05, P2-12, P4-12 | [DYD-240](https://linear.app/bodnar-balazs/issue/DYD-240) | part or lens (control-flow:54, 389); quoted passages for prose |
| Prototype resources fold the winner into main | P3-02, P4-06 | [DYD-241](https://linear.app/bodnar-balazs/issue/DYD-241) | never merged (prototype SKILL, control-flow:614-615, wtc:35) |
| diagnosing-bugs asks the human, posts before code, fixes without red | P3-03, P3-04, P4-04 | [DYD-242](https://linear.app/bodnar-balazs/issue/DYD-242) | crew contract (control-flow:28, 50; coding-standards:177) |
| wayfinder role bindings | P3-01, P3-06, P3-07, P3-10, P3-11, P4-09 (part), P4-10, P4-11 | [DYD-243](https://linear.app/bodnar-balazs/issue/DYD-243) | roles and standard (control-flow:70, 222; [DR 047](../decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md):62-63; standard:71, 85) |
| Docs rubric shape and "skill template" | P2-10, P3-09, P4-15 | [DYD-244](https://linear.app/bodnar-balazs/issue/DYD-244) | DYD-230 Outcome; [DR 049](../decisions/049-skills-are-the-source-retire-the-compiler.md) |
| code-writer comments unsigned; spec-review risk named after code | P1-08, P2-08 | [DYD-245](https://linear.app/bodnar-balazs/issue/DYD-245) | standard:218-219, 39 |
| Specifying without In Review | P4-13 | [DYD-246](https://linear.app/bodnar-balazs/issue/DYD-246) | standard:43, 51; control-flow:369 |
| Glossary header missing format and ceiling | P3-14, P4-16 | [DYD-247](https://linear.app/bodnar-balazs/issue/DYD-247) | the Scaffold glossary |
| Reviewer interface: contract pin, red proof, block shape | P1-05, P2-03, P4-02 | Question [DYD-248](https://linear.app/bodnar-balazs/issue/DYD-248) | neither: the authority docs disagree |
| Tightening and proof-only hop names; pre-edit checks | P1-06, P2-04, P2-06, P4-08 | Question [DYD-249](https://linear.app/bodnar-balazs/issue/DYD-249) | neither: three hops only, no proof branch |
| Code-writer loads research | P1-04, P4-05 | Question [DYD-250](https://linear.app/bodnar-balazs/issue/DYD-250) | split: DYD-230 decision 6 vs [DR 050](../decisions/050-officers-crew-and-skills-hats-retired.md), control-flow:48, wtc:87-88 |

All 12 Bugs are AFK; all 3 Questions were filed High priority; every filed Issue is wired as a
blocker of [DYD-11](https://linear.app/bodnar-balazs/issue/DYD-11) and lives in
`dydo 3.0 / Consolidate and release`.

Shared-path overlaps: `linear-workspace-standard.md` is touched by DYD-236 (lines 79-81, 183-185),
DYD-238 (line 41), DYD-246 (line 39); `issue-captain/SKILL.md` is touched by DYD-238 (step 5),
DYD-239 (steps 6-7), DYD-246 (step 2) — sequence or take together.

## Refuted hypotheses

- **P1-09:** chief-of-staff "closes Done" / "fix the mechanical drift" read compatibly (wtc:171-173
  "clear or route with its owner named").
- **P1-12:** landing cleanup's return line is the resumed captain's ordinary
  `done <key>: merged` (issue-captain:118-120).
- **P2-09:** writer's "no worse" vs. rubric's "already bad code is a finding" is intended per
  DYD-230 decision 1 (rubric carries the quality lens via FAIL).
- **P2-11:** planner filing first Issues in `Todo` before approval matches control-flow:231.
- **P3-05:** co-thinker's admiral-commissions-atomic-captain is one alternative; the no-admiral case
  (human opens the session) covers the rest, and DYD-230 shows atomic Issues do run under a Project
  admiral.
- **P3-08:** writing-for-agents' "Use when" already names the common triggers vs.
  writing-for-humans.
- **P3-12:** grill-me is `disable-model-invocation` (slash-only) and loads grilling itself.
- **P3-13:** wizard's script is ephemeral by default, committed only on the human's word;
  control-flow:76 assigns wizard to the Enablement captain.
- **P4-09 (partly refuted):** the standard gives Prototype/Enablement "Level: any", so a captain MAY
  open those Sub-issues — refuting that part; the Enablement-scope part is confirmed and folded into
  DYD-243.

## Bugs and Questions filed

The 12 Bugs (DYD-236 through DYD-247) are listed in the root-findings table above.

- **[DYD-248](https://linear.app/bodnar-balazs/issue/DYD-248)** (Reviewer interface after DYD-230):
  **DECIDED** 2026-09-23 by the human (posted by admiral/model: claude-opus-5-5). (a) the reviewer
  brief pins `Contract: <KEY> description as of <Linear updatedAt>`; (b) the brief carries the
  code-writer's `IMPLEMENTED` line as a fifth field, reviewer reruns only on stated doubt; (c) the
  standard's one-line PASS/FAIL forms stay canonical, extended with
  `contract <ref>; base <SHA>; gates <N/N>`, glossary/control-flow point to the standard, and the
  code rubric's axis goes in the FAIL `wrong:` slot. Next: folded into the batched skill-harmony fix
  pass.
- **[DYD-249](https://linear.app/bodnar-balazs/issue/DYD-249)** (Code-writer work beyond
  implement/fix/merge): **DECIDED** 2026-09-23 by the human (posted by admiral/model:
  claude-opus-5-5). A new `tighten` hop, judged by "no behaviour changed; the named risk is closed";
  proof-only commits are `<KEY> proof: <hypothesis>`; the five pre-edit checks bind
  implement/fix/tighten in a parent Issue or lane, merge work proves the pins in `merge.md`, and
  proof-only work proves HEAD on a child branch cut from the audit SHA and named in its brief, a
  clean worktree, and only the test path touched; the working-tree contract gains a proof-branch
  row. Next: folded into the batched skill-harmony fix pass.
- **[DYD-250](https://linear.app/bodnar-balazs/issue/DYD-250)** (May the code-writer load research
  itself): still open in `Todo`, unanswered as of this record — the human has not yet decided
  between keeping DYD-230 decision 6 (code-writer loads research itself, no scouts) or reversing it
  (hand-raise to the captain).

## Record

This file is the record, delivered through the record-delivery route in the
[working-tree contract](../../guides/working-tree-contract.md). After DYD-251 merges, the admiral
resumes DYD-235 for its retention check (verify the filed Bugs and this record's exact content and
merge reachability) before DYD-235 closes Done.

## Related

- [Working-Tree Contract](../../guides/working-tree-contract.md) — record-delivery route this file
  follows.
- [DR 047 — Supersymmetry, Hop Statuses, Merge Issues, and the Release Protocol](../decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md)
- [DR 049 — Skills Are the Source: Retire the Compiler](../decisions/049-skills-are-the-source-retire-the-compiler.md)
- [DR 050 — Officers, Crew and Skills: Hats Retired](../decisions/050-officers-crew-and-skills-hats-retired.md)
- [Inquisitions](./_inquisitions.md)
