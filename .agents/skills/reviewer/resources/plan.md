# Reviewing a Plan

Target: a sprint in `plan-review` — a root record (specification + slice map) plus one slice
file per row. Fresh eyes are the point: you receive only the artifacts, never the planning
conversation. Your verdict block goes into the sprint root; pass flips it `active`, fail sends
findings back to the planner.

## Method

1. **Check the structure first** — a plan missing root sections or slice files FAILS before
   content review begins.
2. **Verify claims against the codebase** — read the cited files and patterns. A plan that
   misdescribes the code it plans to change is the highest-value catch this review makes.
3. **Read every slice as its implementer** — with only that file and the standards, could you
   execute it without a single decision or question? Any interpretive latitude is a finding.
4. **Interrogate the specification** — every question answered, acceptance criteria testable,
   out-of-scope binding.

## Checklist

- [ ] Format complete: root has all six sections (Specification, Prior art, Design, Slice map,
      Ordering & isolation, Watch-outs); every slice-map row has its slice file
- [ ] Specification closed: intent, binding in/out of scope, testable acceptance criteria,
      **zero open questions** — one unanswered question is an automatic FAIL
- [ ] Mechanical: implementation needs no architectural decisions; concrete examples included
- [ ] Patterns named with paths — verified to exist and say what the plan claims
- [ ] Prior art evidenced: search performed and recorded, even where rejected — verify the
      evidence, don't repeat the search
- [ ] Slices disjoint by file and atomic — each reviewable in one round; oversized slices FAIL
- [ ] Ordering and gates explicit: dependency order, exact per-slice test/check commands
- [ ] Isolation holds: parallel-worktree vs serial lanes declared, shared hot files identified,
      slices genuinely cannot collide
- [ ] Hazards handled: data-shape/migration risks named, rollback story stated
- [ ] Each slice self-contained: a fresh implementer with only that file and the standards
      could execute it — no interpretive latitude, no model names in plan text
