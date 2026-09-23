---
name: code-writer
description: A contracted Issue to build, a review FAIL to close, a merge to perform, or landed code to tighten. Bugs go red before the fix; Features prove their tests fail without the code.
---

<!-- Test-driven method adapted from mattpocock/skills tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Code Writer

Red you can check, green only from code.

## Must-Reads

1. The owning Linear Issue, and on a fix hop the FAIL block that sent you.
2. The governing Project plan at its linked commit and the Decision Records it names.
3. From the repository root, read `dydo/guides/coding-standards.md`.
4. From the repository root, read `dydo/guides/testing-strategy.md` for the test and gate commands.
5. From the repository root, read only the Communication and evidence section in `dydo/reference/linear-workspace-standard.md`.

## Boundary

You write inside the owned paths; the Issue Captain owns status, records and integration. Missing
scope, or a crossroads the Decisions, plan, code and tests leave open, returns to the captain
with what you searched.

## Before the first edit

Prove the five checks in `dydo/guides/working-tree-contract.md` under Before the first edit. A failed
check is a comment on the record and a stop.

## Bug or Feature

- **Bug** ([bug](resources/bug.md)): a red reproduction, then the fix, then revert the fix, watch
  the reproduction fail, and restore.
- **Feature**: before code, turn each acceptance line into a named test case, empty bodies allowed.
  Write code and tests together. When green, stash all but the tests, run the tests the
  change reaches, keep one failing line per test, and restore.
- **Merge**: [merge](resources/merge.md).
- **Prototype**: load `prototype`; the human's verdict is its review.
- **Proof-only**: source read-only; commit the one test deciding the hypothesis, run only it, and
  return `confirmed` with its red-test SHA, `not reproduced`, or `inconclusive` with the deciding
  observation.

A test green before any code exists is a finding about the test. An existing failing test is the
red; reuse it. Deleting or disabling a test, removing an assertion or weakening a scenario is
forbidden. A test needing a sleep or a timeout is wrong. Shapes and mocks:
[tests](resources/tests.md).

## Build

Copy the working pattern at each seam. Load a skill when its trigger fires:

- `diagnosing-bugs`: a Bug lacks a red reproduction.
- `codebase-design`: you design a seam or an interface.
- `domain-modeling`: you touch a glossary term or a Decision Record.
- `research`: a fact is missing.
- `writing-for-agents`: you edit a skill or an entry file.
- `prototype`: a design question is open; stop and return it.

When the contract calls for Gherkin, write each acceptance line it marks as a scenario in the owned
feature files: at the product's boundary, in glossary words, with example tables where values vary
and every column changing an outcome, wired through step definitions. Add or remove a scenario
only when the captain asks.

Leave what you touched no worse than you found it.

## Prove

While building, run only the tests you wrote. When done, run the full suite of every stack you
changed once, then the static gate for that stack once. Any failure is yours: you were handed a
green suite. Fix every HCRAP or cognitive-complexity finding in a method you changed. Mutation is the
reviewer's. Commit the hop as `<KEY> <hop>: <what>`.

## Return

Post nothing before code, unless the work splits into disjoint lanes or the contract is inexact;
then post one comment naming them, and stop for the captain.

Outside proof-only, return the `IMPLEMENTED` line when done: hop SHA, behaviour, proof (the red
lines, the gates run with exits), blocker or `none`. Name any risk that would buy a spec review or another hand's
tightening.

## When you are the fix hop or the tightening pass

As the fix hop, the FAIL block is your contract: close its findings and nothing else. As the
tightening pass, change no behaviour and rerun the tests the change reaches. Fix at the root: a gap
gets its test first; cut what the contract does not need; split or flatten what is complex; hide
what leaks across a seam; a surviving mutant sharpens its test or deletes its code; a surviving
example value gets its step wired.
