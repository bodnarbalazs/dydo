# Reviewing a Plan

Target: a sprint in `plan-review` — a root record (specification + slice map) plus one slice
file per row. Fresh eyes are the point: you receive only the artifacts, never the planning
conversation. Your verdict block goes into the sprint root; pass flips it `active`, fail sends
findings back to the planner.

A plan PASSES only if:

- **The specification is closed** — intent, binding in/out of scope, testable acceptance
  criteria, and **zero open questions**. An unanswered question is an automatic FAIL.
- **It is mechanical** — detailed enough that implementation needs no architectural decisions;
  concrete examples included.
- **It names the patterns to follow** — with the paths where they live. Verify they exist and
  say what the plan claims.
- **Prior art is evidenced** — the search was performed and recorded, even where rejected.
  Verify the evidence; don't repeat the search.
- **Slices are disjoint and atomic** — parallelizable by file, each reviewable in one round.
  Oversized slices let bugs through: FAIL them.
- **Ordering and gates are explicit** — dependency order, exact per-slice test/check commands.
- **Isolation holds** — shared hot files identified; the slices genuinely cannot collide.
- **Hazards are handled** — data-shape/migration risks named, rollback story stated.
- **Each slice is self-contained** — a fresh implementer with only that file and the standards
  could execute it. No interpretive latitude. No model names in plan text.

Verify claims against the codebase — read the cited files. A plan that misdescribes the code it
plans to change is the highest-value catch this review makes.
