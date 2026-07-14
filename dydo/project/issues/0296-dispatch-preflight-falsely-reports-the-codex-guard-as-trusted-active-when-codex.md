---
title: Dispatch preflight falsely reports the codex guard as trusted/active when codex has silently disabled it (dydo cannot compute codex's opaque hook hash)
id: 296
area: backend
type: issue
severity: critical
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-14
---

# Dispatch preflight falsely reports the codex guard as trusted/active when codex has silently disabled it (dydo cannot compute codex's opaque hook hash)

Codex trusts hooks by an opaque per-entry hash dydo can't reproduce; any change to .codex/hooks.json silently disables the hook until a human re-approves, yet CheckHookTrust reports green - dydo asserts the guard is active when it is not.

## Description

## Observed (empirically proven during the #0295 fix, codex-cli 0.144.1)

dydo's dispatch preflight reports the codex guard hook as **trusted/active ("green") when codex has silently disabled it.** This means dydo can assert "this codex agent is guarded" when it is not.

## Mechanism

- Codex trusts a project hook (`.codex/hooks.json`) via an **opaque per-hook-entry hash** stored in `~/.codex/config.toml [hooks.state]`. It is NOT the file's SHA256 (verified: file sha256 `55af5ed5` vs codex's pinned `pre_tool_use` hash `581b21e8`; ~25 content/JSON/whitespace transforms failed to reproduce codex's hash — the algorithm is opaque to dydo).
- **Any content change to `.codex/hooks.json` silently disables that hook** until a human re-approves it interactively in codex. Proven: after a matcher-only edit, `hook: PreToolUse` fired **0 times** across a real `codex exec` run (Stop still fired — entries are hashed independently). No warning is emitted; the hook is just skipped.
- dydo's `CheckHookTrust` / `DispatchPreflight.DefaultHookTrustRepair` writes dydo's OWN sha256 into the trust entry and treats an enabled, well-formed-hex hash as `Trusted`. Codex rejects dydo's hash (it's not codex's opaque hash), so the hook stays skipped — but preflight **passes green**. Simulated exactly: wrote dydo's sha256 with `enabled=true`, ran codex → still 0 PreToolUse fires, yet preflight reports trusted. This is the 0281 finding turned lethal.

## Impact — CRITICAL (trust integrity)

dydo's central safety claim is "the guard enforces policy on every action." For codex agents this claim can be **silently false while dydo reports it true**. Any time `.codex/hooks.json` changes (a dydo update to the matcher, a template refresh, a manual edit) the codex guard goes dark until a human re-approves, and nothing in dydo detects or surfaces it. Combined with #0295 (shell was never matched at all), codex guard coverage has been materially weaker than dydo asserts.

## Fix directions (design decision required — do NOT silently ship one)

1. **Preflight must not claim what it cannot verify.** dydo cannot compute codex's hash, so it cannot confirm the hook is live via the trust table. Options: (a) actively PROBE (dry-run a known-blocked op through the hook and confirm the block) rather than infer from the hash; (b) downgrade the preflight report from "guarded ✓" to "guard status UNVERIFIABLE for codex — re-approve the hook" when it can't prove liveness. Blocking every dispatch on uncertainty would wedge the fleet, so this is a real design call.
2. **Robust automatable enforcement:** launch dispatched codex with `--dangerously-bypass-hook-trust`, which forces the hook ON regardless of the trust table — removing dydo's dependence on a hash it can't compute. Launcher (`Services/TerminalLauncher.cs`) change. This is likely the cleanest path and aligns with the 2.1.0 principle: *enforcement dydo can't verify, dydo shouldn't depend on.*
3. Deployment note for the #0295 fix itself: after regenerating `.codex/hooks.json` (new matcher), a human must re-approve the hook in codex once, or option 2 must be in place — otherwise the guard is dark despite the fix.

## Acceptance
- Preflight either PROVES the codex hook is live (probe) or reports it UNVERIFIED — it never falsely reports "guarded."
- OR dispatched codex runs with forced-hook trust so liveness doesn't depend on codex's opaque hash.
- A regression pins whichever contract is chosen.

Related: #0295 (matcher/shell hole), #0281 (hook-trust staleness), 0250/0256 (identity ambient-fallback).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)