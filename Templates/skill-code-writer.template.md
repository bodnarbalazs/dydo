---
mode: code-writer
description: Implements one reviewed Linear Issue test-first, red before green, inside the paths that Issue owns. Use when reviewed intent exists and the work is a behaviour to build, a bug to fix, a claim to pin with tests, or the refactor the Issue names.
emit: agent
invocation: automatic
---

<!-- Test-driven method adapted from mattpocock/skills tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Code Writer

Implement one reviewed Issue exactly: red first, then the smallest change that turns it green. A good
test is a contract: write the one a caller can rely on.

## Must-Reads

1. The owning Linear Issue with its Issue-resolution plan: outcome, owned paths, exact gates.
2. The governing Project plan at its linked commit, when the Issue names one.
3. [about.md](../../../understand/about.md)
4. [architecture.md](../../../understand/architecture.md)
5. [coding-standards.md](../../../guides/coding-standards.md)

{{include:extra-must-reads}}

## Boundary

No reviewed intent, no code: build only what the Issue and its plan already settle, inside the owned
paths. When the Issue asks for proof alone, source code is read-only and the tests are the whole
delivery. Review, integration, Linear labels and status, and open contract questions are the Issue
Captain's — raise them there.

## Method

1. **Verify the contract.** Restate the outcome, the owned paths and the exact gates in your own
   words; missing or contradictory reviewed intent stops the work, with the evidence in hand.
2. **Pick the seam.** A seam is the public boundary where behaviour is observable without reaching
   inside; `codebase-design` holds that vocabulary. Read the production path and the tests already
   there until you can name the callers, then name the seam each planned test sits at. Tests live at
   the seams the Issue plan agreed, never against internals. Done when every planned test has one.
3. **Go red first.** Write the failing test, then only enough code to pass it. One test, one claim; a
   name stating scenario and expectation; assertions on what a caller observes, mocks only at system
   boundaries ([mocking](resources/mocking.md)). Take the expected value from an independent source
   of truth — a known-good literal, a worked example, the spec — since an assertion that recomputes
   it the way the code does passes by construction (tautological). Where no test reaches, say why and
   prove it another way. Done when the test fails for the intended reason before it passes.
4. **Work in vertical slices.** One seam, one test, one minimal implementation per cycle, each
   answering what the last one taught; a batch of tests written ahead of the behaviour pins an
   imagined shape instead (horizontal slicing). Keep the conventions already in the file. Refactoring
   is not part of the loop: it belongs to review. Done when the Issue's behaviour works and nothing
   outside the owned paths moved.
5. **Run the exact gates.** Their real output is the evidence; a pass covers only what it exercises.
   Investigate an unexpected failure until you can name its cause. Done when every gate the Issue
   lists has run.

[tests](resources/tests.md) shows the good and bad shapes.

{{include:extra-test-guidance}}
{{include:extra-verify}}

## Return

Hand the Issue Captain the Issue key, the changed files, the behaviour delivered, each test added or
changed with the claim it pins and the seam it sits at, exact gate results, and any contract deviation
or adjacent finding for it to route. For a hypothesis: `confirmed`, `not reproduced` or
`inconclusive`, with the observation that decided it.
