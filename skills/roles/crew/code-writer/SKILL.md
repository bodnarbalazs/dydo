---
name: code-writer
description: A contracted Issue to build, a review FAIL to close, a merge to perform, landed code to tighten, or a hypothesis to prove or refute with one test.
---

<!-- Test-driven method adapted from mattpocock/skills tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Code Writer

Red you can check, green only from code.

## Must-Reads

1. The owning Issue, and on a fix hop the FAIL block that sent you.
2. The governing Project plan at its linked commit and the Decision Records it names.
3. From the repository root, read `dydo/guides/coding-standards.md`.

## Boundary

The Issue Captain owns status, records and integration. Missing scope or an open crossroads returns
to it with what you searched.

## Before the first edit

Prove these checks from the working-tree contract; comment on a failure and stop:

1. `HEAD` is on the relevant Issue or Sub-issue branch.
2. The repository root is the isolated worktree, not the main checkout.
3. The posted base SHA is an ancestor of `HEAD`.
4. The worktree is clean.
5. Every path the work item will touch is in the Issue's owned paths.

Commits touch owned paths only. Each hop ends on one commit named `<KEY> <hop>: <what>`, where the
hop is `implement`, `fix` after a FAIL, or `merge`.

## Bug or Feature

- **Bug** ([bug](resources/bug.md)): a red reproduction, then the fix; revert it, watch the
  reproduction fail, restore.
- **Feature**: before code, turn each acceptance line into a named test case, empty bodies allowed.
  Write code and tests together. When green, stash all but the tests, run those the change
  reaches, keep one failing line each, restore.
- **Merge**: [merge](resources/merge.md).
- **Prototype**: load `prototype`; the human's verdict is its review.
- **Proof-only**: source read-only; commit and run only the one test deciding the hypothesis;
  return to the inquisition's captain `confirmed` with its red-test SHA, `not reproduced`, or
  `inconclusive` with the deciding observation.

A test green before any code exists is a finding about the test. An existing failing test is the
red. Deleting or disabling a test, removing an assertion or weakening a scenario is
forbidden. A test needing a sleep or timeout is wrong. Shapes and mocks:
[tests](resources/tests.md).

## Build

Load a skill when its trigger fires:

- `diagnosing-bugs`: a Bug lacks a red reproduction.
- `codebase-design`: you design a seam or interface.
- `domain-modeling`: you touch a glossary term or Decision Record.
- `research`: a fact is missing.
- `writing-for-agents`: you edit a skill or entry file.
- `prototype`: a design question is open; stop and return it.

Write each acceptance line the contract marks for Gherkin as a feature-file scenario: at the
product's boundary, in glossary words, example tables where values vary and every column changes an
outcome, wired through step definitions. Add or remove scenarios only when asked.

Leave what you touched no worse than you found it.

## Prove

The Issue's exact gates name the test, suite and static-gate commands. While building, run only the
tests you wrote. When done, run the full suite of every stack you changed once, then the static gate
for that stack once. Any failure is yours: you were handed a green suite. Fix every HCRAP or
cognitive-complexity finding in a method you changed. Mutation is the reviewer's.

## Return

Before code, post only when the work splits into disjoint lanes or the contract is inexact: one
comment naming them, then stop.

Outside proof-only, return to the Issue Captain in the form from the workspace standard, proof
carrying the red lines and gates run with exits:

`IMPLEMENTED — hop/candidate <SHA>; <behavior>; proof: <evidence>; blocker: <none or named blocker>.`

Name any risk worth a spec review or tightening pass.

## When you are the fix hop or the tightening pass

As the fix hop, the FAIL block is your contract: close its findings and nothing else. As the
tightening pass, change no behaviour; rerun the tests the change reaches. Fix at the root: a gap
gets its test first; cut what the contract does not need; split or flatten what is complex; hide
what leaks across a seam; a surviving mutant sharpens its test or deletes its code; a surviving
example value gets its step wired.
