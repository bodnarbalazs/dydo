# Spec and Plan skeleton

What Phase 1 records on the Issue before any product code is written. Keep it just in time: the
route for the work in front of you, not a design for work the Issue does not name.

```markdown
## Spec

**Scenarios** — `features/<slug>.feature`, one per criterion proved at the boundary.
**Gates** — commands verbatim, each with its pass condition and the governing static policy.

## Plan

**Approach** — one sentence: the change's shape and the alternative rejected; a `show-me` diff of
the tree or call tree when the shape is what changes.
**Pattern to copy** — `path/to/file.ext:120`, what this mirrors, and where it departs.
**Files** — every touched path and its one edit.
**Steps** — ordered; each ends on a checkable state.
**Edge cases** — inputs, states and failures, with the behaviour for each.
**Extra pass** — `none | spec review | separate hardening pass`: the one-line risk that buys it.
```

A lane with nothing observable at the boundary carries gates only; its parent's scenarios prove it.
A gates-only spec still commits — an empty commit pins the contract.

When Phase 2 or Phase 3 disproves this spec or route, stop at the choice and report the mismatch:
the Captain decides whether you revise it or a separate hand re-specifies before work resumes.
