---
type: decision
status: accepted
date: 2026-03-09
area: project
---

# 006 — Drop Interviewer Role (Absorbed by Co-Thinker)

Remove the interviewer role since the co-thinker already covers all its responsibilities plus deeper exploration.

## Problem

The interviewer role exists to gather requirements from the human before planning begins. In practice, this never happens as a standalone step. The human always starts with a co-thinker session where requirements naturally emerge through exploration.

## Observation

The interviewer and co-thinker have overlapping jobs:

| Activity | Interviewer | Co-Thinker |
|----------|------------|------------|
| Ask clarifying questions | Yes | Yes |
| Surface ambiguity | Yes | Yes |
| Propose concrete examples | Yes | Yes |
| Explore tradeoffs | No | Yes |
| Challenge assumptions | No | Yes |
| Document conclusions | No | Yes |
| Produce requirements brief | Yes | As needed |

The co-thinker does everything the interviewer does, plus deeper exploration. The interviewer is a strict subset.

## Decision

Drop the interviewer role. The co-thinker absorbs its responsibilities.

When requirements refinement is needed, the co-thinker naturally handles it as part of exploration. The co-thinker mode file should include guidance on scoping and requirement refinement — not as a formal subprocess, but as one of its exploration techniques.

## Implications

- Remove `modes/interviewer.md` from agent templates
- Remove `--feature` flag mapping (flags are being dropped entirely per the workflow simplification)
- Update co-thinker mode file to include scoping/requirements techniques
- No code changes needed — the guard doesn't hardcode role names, roles are defined by their permission profiles
