---
area: project
type: concept
status: idea
---

# Documentation Coverage Estimation

Static analysis that estimates how well the documentation workspace covers the concepts that matter in the codebase. A third coverage axis alongside test coverage and inquisitor confidence.

---

## Problem

The codebase evolves, docs lag behind. There is no way to tell where the gaps are or how significant they are. Not everything needs documentation — code should be self-explanatory — but central concepts referenced widely deserve proportional documentation.

---

## Core Idea

A PageRank-like approach: **things that are heavily referenced should have proportionally rich documentation.**

Two complementary analyses:

### Doc Graph Analysis (existing infrastructure)

`DocGraph` already tracks incoming links between pages. A page with many incoming links but thin content (low word count, no examples, no summary) is a coverage gap.

### Code-to-Doc Gap Analysis (new)

- **Extract concepts from code**: class names, command names, service names, model names — the nouns of the system.
- **Scan docs for mentions**: does the docs workspace reference each concept? How many times? In how many pages? With how much surrounding context?
- **Score the gap**: high code centrality + low doc presence = red flag.

---

## Scoring Model

```
For each concept C:
  code_weight  = files_referencing(C) + incoming_dependencies(C)
  doc_weight   = pages_mentioning(C) * avg_depth_per_mention
  coverage     = doc_weight / code_weight
```

`avg_depth_per_mention` distinguishes a passing reference from a dedicated section with explanation.

### Coverage Buckets

| Coverage | Meaning |
|----------|---------|
| **undocumented** | code_weight > threshold, doc_weight = 0 |
| **underserved** | coverage < 0.3 — mentioned but not explained |
| **adequate** | coverage 0.3 - 1.0 |
| **well-covered** | coverage > 1.0 |

---

## Three Coverage Axes

These are orthogonal and complementary:

- **Test coverage** — "did we execute this line?" (binary, mechanical)
- **Inquisitor confidence** — "does an AI agent feel it understood this?" (subjective, per-session)
- **Doc coverage** — "are important concepts explained proportionally to their centrality?" (structural, static)

Test coverage tells you if code works. Inquisitor confidence tells you if the docs are coherent. Doc coverage tells you if the docs are complete.

---

## Implementation Notes

Existing building blocks: `DocScanner`, `MarkdownParser`, `DocGraph`, `graph stats` command.

A `dydo coverage` command would:

1. Scan the codebase for concept extraction (classes, commands, services, models)
2. Scan the docs workspace for mentions of those concepts
3. Score and rank using the model above
4. Output a report of gaps ordered by severity

PageRank twist: concepts referenced by other highly-referenced concepts inherit importance.

---

## Open Questions

- **Concept granularity**: classes? methods? commands? config keys? Start with services + models + commands to cover 80%.
- **Exclusions**: some things are intentionally undocumented (internal utilities, test helpers). Need a way to mark "no doc needed" — frontmatter annotation or config list.
- **Output format**: CLI table? Generated doc page? Trackable score over time?
- **Depth heuristic**: how to distinguish a passing mention from substantive documentation of a concept.

---

## Related

- [dydo Commands Reference](../../reference/dydo-commands.md) — Existing command patterns
