---
title: Dispatch vendor override needs a friendly error when the target vendor is not configured
id: 239
area: backend
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Henry
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# Dispatch vendor override needs a friendly error when the target vendor is not configured

dispatch --codex / --claude against an unconfigured vendor should fail fast with an actionable message (missing binary, tier mapping, or synced bodies) instead of a downstream launch failure or stale reservation.

## Description

When a dispatch carries an explicit vendor override (`--codex` / `--claude`) but that vendor is not configured or not available in the project (CLI not installed / not on PATH, no tier mapping for the vendor in `dydo.json`, no compiled agent bodies for it), the failure should be a clear, actionable error at dispatch time — not a launch that dies downstream (cf. issue 0227's bare-binary launch failure) or a stale `Dispatched` reservation the watchdog has to reclaim.

Expected shape: `dydo dispatch --codex ...` in a project with no codex configuration fails fast with a message that names (1) the missing prerequisite (binary / tier mapping / synced bodies), and (2) the fix (install the CLI, add the vendor tier mapping, run `dydo sync`). Same symmetrically for `--claude` in a codex-only project.

Context: per DR 037, cross-vendor dispatch is an explicit dispatch-time override on top of the same-vendor default — the override is the one place a human routinely types a vendor by hand, so it is the surface that deserves the guardrail. Tri-modal support (claude-only, codex-only, both) makes "override targets an absent vendor" a normal user mistake, not an edge case.

## Reproduction

In a project with only one vendor configured, run `dydo dispatch` with the other vendor's flag and observe the current failure mode (launch error or stale reservation instead of an upfront validation error).