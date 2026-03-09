---
type: decision
status: accepted
date: 2026-03-09
area: project
---

# 004 — Terminal Grouping Over Dashboard

Same-window tab spawning and an agent tree command instead of a React GUI dashboard for managing multi-agent terminal chaos.

## Problem

As agent count scales in parallel workflows, terminal tabs become unmanageable. The human loses track of which agents belong to which slice of work, especially when agents dispatch sub-agents who dispatch reviewers, etc. The spatial organization of tabs doesn't reflect the logical structure of the work.

## Rejected: React GUI Dashboard

A self-contained React app already ships with dydo for audit visualization. It was considered for a full agent control plane — live agent graph, command buttons, terminal streaming, permission proxying.

Problems:
- Terminal content isn't accessible via API on Windows Terminal or most emulators
- Permission prompts are in-process to Claude Code — proxying them requires PTY interception or a Claude Code extension
- This is effectively building an IDE layer, far out of scope

## Chosen: Same-Window Tab Spawning + Agent Tree Command

Two small changes that address the core problem:

1. **`wt -w 0`** (Windows Terminal) / equivalent on macOS: when `dydo dispatch` spawns a new tab, it opens in the *same terminal window* as the dispatching agent, not the active/focused window. Child agents naturally group with their parent.

2. **`dydo agent tree`**: ASCII visualization of the dispatch hierarchy — who spawned whom, current role, status. Reads existing filesystem state, no new infrastructure.

## Why This Works

The filesystem-as-state-store already has all the data needed for visibility (`dydo agent list`, waiting states, inbox). The actual problem was spatial organization — tabs scattering across windows. Window-level grouping solves this with a one-line change to `TerminalLauncher.cs`, and the tree command fills the remaining gap of understanding dispatch relationships.
