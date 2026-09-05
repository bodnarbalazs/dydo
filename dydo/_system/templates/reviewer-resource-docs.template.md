# Reviewing Documentation

Target: one documentation change delivered by a Linear Issue. Two kinds arrive here: human-facing
dydo documents under `dydo/`, judged against the code they describe, and agent-facing documents
(skill templates, their resources, entry points), judged as prompt files that must fire and route.
**Drift** is the failure both kinds share: a claim the tree no longer supports, or an environment
restated in prose (directory layout, `--help`, config) where a pointer would not have gone stale.
Every item below is a FAIL when it holds.

## Method

1. **Resolve the contract.** Issue, governing commit, and the Project plan when one governs. Done
   when audience, owned paths, acceptance criteria and gates are named; an unmet criterion or an
   edit outside the owned paths is a finding.
2. **Sort every changed file** into one of the two lists below, then work that list entire.
3. **Verify from source.** Open the paths, run the commands, read the code; rerun the Issue's gates
   yourself, `dydo check` among them. Drift lives in the sentence nobody rechecked. Done when every
   claim the change makes has met its source.

## Human-facing dydo documents

- Frontmatter, naming, hub membership or a link broken against
  [writing-docs.md](../../../../dydo/reference/writing-docs.md).
- Written for a reader its folder does not name (`understand/` vs `guides/` vs `reference/`).
- A meaning another document already owns, written a second time here.
- `dydo check` or an Issue gate left failing on the touched tree.
- A reusable decision, invariant, pitfall or explanation left only in execution evidence instead of
  assimilated into dydo.

## Agent-facing documents

`writing-for-agents` governs this writing; the list below is that method turned into verdicts.

- **Description that is not a trigger.** Model-invoked: leading word front-loaded, one trigger per
  branch, no identity the body already carries. Explicit-only: one punchy human-facing line. A
  worker spawned by name may state its job, in trigger form.
- **Anchor missing or doubled, or a tagline that changes nothing.** The no-op test grades against
  the model's default, and a failing line is deleted rather than softened.
- **Shape broken.** Hats and workers: H1 → one-line job → Must-Reads → Boundary → Method with a
  completion criterion on every step → Return or Handoff. Methods keep their upstream shape.
- **Off the map.** Must-Reads that do not name what the sender hands over, a Return that does not
  name its receiver, or a sentence narrating what a neighbour does.
- **A cross-reference missing or extra** against the exact set its brief binds.
- **Vocabulary off the dydo glossary**: a retired word anywhere, or a Linear noun away from a real
  handoff.
- **Steering by prohibition** where the positive target would carry it; a guardrail states both.
- **Sprawl**: a document longer than its reader's path needs. Reference only some branches reach
  goes behind a pointer; a meaning stated twice is stated once.
- **Upstream text altered without a binding reason.** Linear, dydo and host bindings are the whole
  licence, and the attribution comment stays.
- **A return shape its consumer cannot parse**: the review block, or a worker's return to the
  Captain.
- **A link that will not exist after regeneration.** Resource bodies are copied verbatim and climb
  from `resources/`; resolve every path from the emitted folder on both hosts.
- **Compiled output drifted from its template** when regeneration is in the candidate's scope.
  Read the source, then confirm the generated skill matches it; a source-only Issue records the
  integration owner and leaves generated parity to that gate.
