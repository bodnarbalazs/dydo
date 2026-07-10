---
title: Dispatch vendor override needs a friendly error when the target vendor is not configured
id: 239
area: backend
type: issue
severity: low
status: resolved
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

## Resolution

Generalized into `DispatchPreflight` (`Services/DispatchPreflight.cs`), a fail-fast gate `DispatchService` runs BEFORE any reservation or launch — so a dispatch that cannot succeed fails in the dispatcher's own terminal, never as a downstream child-terminal error or a stale `Dispatched` reservation the watchdog has to reclaim. The first failing check short-circuits; a passing preflight makes no state change. The gate runs, in order:

1. **Executable resolvable** — the launch binary resolves on the dispatcher's PATH (catches the codex WindowsApps-alias failure of issue 0227), naming the binary and the install/`--no-launch` fix.
2. **Vendor configured** — an explicit `--claude`/`--codex` override's integration is enabled (`integrations.<host>`) and, when a `models` section is present, its tier is mapped (`models.tiers.<vendor>`).
3. **Synced bodies** — the override vendor's compiled agent artifacts exist (claude: `.claude/agents/*.md`, codex: `.codex/agents/*.toml`), i.e. `dydo sync` was actually run for it; otherwise the launched agent would carry no compiled role definition. Fails naming the artifact surface and `dydo sync`.
4. **Codex posture valid** — `dispatch.codex` sandbox/approval values are accepted (issue 0253), so a config typo fails here with the accepted values rather than deep in argument assembly after reservation.
5. **Windows sandbox prerequisite** — the codex Windows sandbox is present when the configured posture (workspace-write) requires it; never silently downgraded.
6. **Hook trust** — a repo `.codex/hooks.json` is trust-enabled in the user codex config, so codex does not silently run the agent UNGUARDED.

Checks 2 and 3 fire only on an explicit vendor override — the default/inherited host is the caller's own live host, already known good and already running against its own bodies. Each failure names both the missing prerequisite and the fix. Covered by `DynaDocs.Tests/Services/DispatchPreflightTests.cs` (pass/fail path plus message content for every check, including the synced-bodies leg and its injectable probe seam).