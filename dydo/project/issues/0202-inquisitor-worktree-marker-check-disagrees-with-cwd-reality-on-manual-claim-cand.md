---
title: Inquisitor worktree-marker check disagrees with cwd reality on manual claim (candidate surface S14)
id: 202
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-19
---

# Inquisitor worktree-marker check disagrees with cwd reality on manual claim (candidate surface S14)

The inquisitor workflow verifies worktree presence via a per-agent marker file rather than the process cwd, so an agent claimed manually inside a worktree fails the check and loops; same split-brain shape as the hijack class, smaller blast radius.

## Description

The inquisitor workflow's worktree-presence check keys off a per-agent marker file (`dydo/agents/<self>/.worktree`) instead of the process's actual cwd. When an agent is claimed manually (`dydo agent claim <name>`) inside an existing worktree — rather than via `dydo dispatch --worktree` — the marker is absent even though the process cwd is unambiguously inside a worktree directory. A strict-following agent then loops: the workflow says "Do not proceed without a worktree", but inbox-recovery and re-dispatch both reproduce the same missing-marker state.

This is the same split-brain shape as the identity-hijack class (two subsystems answering the same question inconsistently, with the workflow doc treating the weaker one as authoritative), with a smaller blast radius. Candidate addition to Brian's surface map as **S14** — "context-presence checks that key off per-agent markers instead of process state."

Source: `dydo/project/inquisitions/identity-hijack-bug-class.md` §"2026-05-19 — Zelda" finding F18.

Same bug class as #0183 (root primitive) — out of scope for the F1 fix slice; tracked here for future investigation.

## Evidence

`dydo/agents/Zelda/modes/inquisitor.md` lines 60–74 instruct the inquisitor to verify worktree presence via:

```bash
ls dydo/agents/<self>/.worktree 2>/dev/null && echo "OK" || echo "NO_WORKTREE"
```

In the live-incident session the process cwd was unambiguously a worktree:

```
…\dydo\_system\.local\worktrees\identity-hijack-bug-class-inquisition\
```

…but the per-agent marker `dydo/agents/Zelda/.worktree` did not exist, because Zelda was claimed manually (`dydo agent claim Zelda`), not via `dydo dispatch --worktree`. The check returned `NO_WORKTREE`. By the letter of the workflow ("Do not proceed without a worktree. … Read your inbox to recover the original brief …") the agent would loop indefinitely: the inbox was empty (no brief to recover), and re-dispatching would just create another manual-claim with the same missing marker.

## Relation to Brian's surfaces

Same split-brain pattern as S0. Two subsystems (the marker file vs. the process cwd) answer the same question — "am I in a worktree?" — inconsistently, and the workflow doc treats the marker as authoritative. A strict-following agent stalls. Candidate **S14** in Brian's surface map: "context-presence checks that key off per-agent markers instead of process state".

## Suggested follow-up

Either (a) update the inquisitor workflow doc to check process cwd against worktree paths rather than the per-agent marker file, or (b) make manual claims that happen inside a worktree write the per-agent marker. Operational severity is low (a single workflow doc step), but signal severity is high — this is a clean, isolated reproduction of the bug class's shape in a different subsystem.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)