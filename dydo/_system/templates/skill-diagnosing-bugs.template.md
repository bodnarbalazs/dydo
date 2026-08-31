---
mode: diagnosing-bugs
description: The tight loop that goes red. Use when a defect needs diagnosing — a failing behaviour, a flaky test, a regression, a performance drop — or when a fix is about to be written on a cause nobody has reproduced.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills diagnosing-bugs at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Diagnosing Bugs

A discipline for hard bugs. Skip a phase only when you can say why. Implement stage: the implementer
reaches for this when its Issue is a defect.

**Redact every secret** before you show a command, an output or a captured artifact: write `<REDACTED>`
in its place, and build loops against env vars so the credential stays in the environment. Captured
artifacts carry auth headers: quote only the lines that carry the signal. If the redacted output is not
enough to diagnose the bug, say so on the Issue.

## Phase 1: Build a feedback loop

**This is the skill.** Everything else is mechanical. With a **tight** pass/fail signal that goes _red_
on _this_ bug you will find the cause; bisection, hypothesis-testing and instrumentation all just
consume it. Without one, no amount of staring at code will save you. Spend disproportionate effort
here. **Be aggressive. Be creative. Refuse to give up.**

Ways to construct one, in roughly this order:

1. **Failing test** at whatever seam reaches the bug — unit, integration, e2e.
2. **Curl / HTTP script** against a running dev server.
3. **CLI invocation** on a fixture input, diffing stdout against a known-good snapshot.
4. **Headless browser script** driving the UI, asserting on DOM, console or network.
5. **Replay a captured trace** — a real request, payload or event log — through the code path in isolation.
6. **Throwaway harness**: a minimal subset of the system, mocked deps, one function call.
7. **Property / fuzz loop** for "sometimes wrong output": 1000 random inputs, hunt the failure mode.
8. **Bisection or differential loop** when the bug appeared between two known states: automate "boot at
   state X, check, repeat" for `git bisect run`, or diff one input through two versions or configs.

### Tighten the loop

Treat the loop as a product: faster (cache setup, skip unrelated init), sharper (assert the specific
symptom, not "didn't crash"), more deterministic (pin time, seed RNG, isolate the filesystem, freeze
the network). A 30-second flaky loop is barely better than no loop; a 2-second deterministic one is a
debugging superpower. For a non-deterministic bug chase a **higher reproduction rate** rather than a
clean repro — loop the trigger 100×, parallelise, add stress, inject sleeps — until it is debuggable.

When you genuinely cannot build a loop, stop and say so on the Issue, listing what you tried and what
would unblock you: access to an environment that reproduces it, a redacted artifact (HAR file, log
dump, core dump, screen recording), or permission to instrument production. Ask, rather than waiting
silently or theorising without a loop.

### Completion criterion: a tight loop that goes red

Name **one command** — a test invocation, a script path, a curl — that you have **already run at least
once**, showing the invocation and its redacted output, and that is:

- [ ] **Red-capable**: it drives the actual bug code path and asserts the **exact reported symptom**, so
      it goes red on this bug and green once fixed. Not "runs without erroring".
- [ ] **Deterministic**: same verdict every run (flaky bugs: a pinned, high reproduction rate).
- [ ] **Fast**: seconds, not minutes.
- [ ] **Agent-runnable**: you can run it unattended.

If you catch yourself reading code to build a theory before this command exists, **stop: jumping
straight to a hypothesis is the exact failure this skill prevents.** No red-capable command, no Phase 2.

## Phase 2: Reproduce and minimise

Run the loop and watch it go red. Confirm it produces the failure that was **reported** rather than a
different one nearby — wrong bug, wrong fix — that it reproduces across runs, and that you have captured
the exact symptom (error message, wrong output, slow timing).

Then shrink the repro to the **smallest scenario that still goes red**: cut inputs, callers, config,
data and steps one at a time, re-running the loop after each cut. That shrinks the hypothesis space in
Phase 3 and becomes the clean regression test in Phase 5. Done when every remaining element is
load-bearing — removing any one makes the loop go green.

## Phase 3: Hypothesise

Generate **3–5 ranked hypotheses** before testing any of them; single-hypothesis generation anchors on
the first plausible idea. Each must be falsifiable, stating its prediction: "if <X> is the cause, then
<changing Y> makes the bug disappear." A hypothesis with no prediction is a vibe — sharpen it or drop
it. Post the ranked list on the Issue before you test, because domain knowledge re-ranks it instantly
("we just deployed a change to #3"), then proceed on your own ranking rather than blocking on an answer.

## Phase 4: Instrument

Each probe maps to one prediction from Phase 3, and you **change one variable at a time**. Prefer a
debugger or REPL where the environment supports it — one breakpoint beats ten logs — then targeted logs
at the boundaries that distinguish hypotheses; keep to those two rather than logging everything and
grepping. Tag every debug log with a unique prefix, e.g. `[DEBUG-a4f2]`, so cleanup is a single grep.
For a performance regression logs are usually wrong: establish a baseline (timing harness, profiler,
query plan), then bisect. Measure first, fix second.

## Phase 5: Fix and regression test

Write the regression test **before the fix**, but only where there is a **correct seam** for it: one
where the test exercises the real bug pattern as it occurs at the call site. A seam too shallow to
replicate the chain that triggered the bug gives false confidence. **If no correct seam exists, that is
itself the finding** — the architecture is keeping the bug from being locked down; record it as one.

Where the seam exists, turn the minimised repro into a failing test there — the test-writer's work when
you delegate it — watch it fail, apply the fix, watch it pass, then re-run the Phase 1 loop against the
original un-minimised scenario.

## Phase 6: Cleanup

Declare the bug done when:

- [ ] The original repro no longer reproduces (re-run the Phase 1 loop).
- [ ] The regression test passes, or the absence of a correct seam is recorded as a finding.
- [ ] Every `[DEBUG-...]` probe is gone (grep the prefix).
- [ ] Throwaway harnesses and prototypes are deleted, or moved to a clearly marked debug location.
- [ ] The hypothesis that held is stated in the commit or PR message, so the next debugger learns it.

## Return

The bug's evidence: the red command with its redacted output, the minimised repro, the hypothesis that
held, the regression test or the missing-seam finding, and the Phase 6 checklist. The implementer posts
it on the Issue and carries it into review.
