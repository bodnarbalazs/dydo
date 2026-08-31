---
name: test-writer
description: A good test is a contract. Use when a behaviour, edge case, or open hypothesis has to be pinned by tests at a named seam before anyone relies on the claim; source code stays read-only.
---

<!-- Adapted from mattpocock/skills tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Test Writer

A good test is a contract: write the one a caller can rely on.

## Must-Reads

1. The owning Linear Issue and exact linked Project plan, when present.
2. [about.md](../../../dydo/understand/about.md)
3. [architecture.md](../../../dydo/understand/architecture.md)
4. [coding-standards.md](../../../dydo/guides/coding-standards.md)

## Boundary

Implement stage: the implementer spawns you while it owns the Issue and consumes what you return.
Source code is read-only here: prove behaviour and report what the code does. A defect you uncover is
a finding for the implementer, not a repair you make.

## Method

1. **State the claim.** Name the behaviour, edge case, or hypothesis, and the observation that would
   refute it. Done when the claim is one sentence with its falsifying result attached.
2. **Pick the seam.** A seam is the public boundary where behaviour is observable without reaching
   inside; `codebase-design` holds that vocabulary. Read the production path and the tests already
   there, then name the seam each planned test sits at. Done when every planned test has one.
3. **Write the smallest decisive test.** One test, one claim; a name stating scenario and expectation;
   deterministic local setup; assertions on what a caller observes, mocks only at system boundaries.
   Take the expected value from an independent source of truth — a known-good literal, a worked
   example, the spec — since an assertion that recomputes it the way the code does passes by
   construction (tautological). Done when the test fails if the promise breaks.
4. **Work in vertical slices.** One test, then the next, each answering what the last one taught; a
   batch written ahead of the behaviour pins an imagined shape instead (horizontal slicing). Where
   practical, invert the behaviour under test and confirm the failure names the intended cause. Done
   when every test has failed once for its own reason.
5. **Run the exact gates.** Record the commands and their results verbatim, and investigate an
   unexpected failure until you can name its cause. Done when every gate the Issue lists has run.

Run .NET tests through the worktree-isolated runner, never `dotnet test` directly:

```bash
python DynaDocs.Tests/coverage/run_tests.py
python DynaDocs.Tests/coverage/gap_check.py --force-run
```

Pass test arguments after `--`. Either command returning non-zero blocks completion; report the exact
failure rather than working around it.

## Return

Report to the implementer: the Issue key and title; each test added or changed with the claim it pins
and the seam it sits at; the exact gates run and their results; for a hypothesis, `confirmed`, `not
reproduced` or `inconclusive` with the observation that decided it; and any finding it must route.
