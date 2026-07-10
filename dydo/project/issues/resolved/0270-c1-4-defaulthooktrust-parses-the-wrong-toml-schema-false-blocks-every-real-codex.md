---
title: c1-4 DefaultHookTrust parses the wrong TOML schema - false-BLOCKS every real codex dispatch (v2.0.7 headline feature non-functional)
id: 270
area: backend
type: issue
severity: high
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-10
status: resolved
---

# c1-4 DefaultHookTrust parses the wrong TOML schema - false-BLOCKS every real codex dispatch (v2.0.7 headline feature non-functional)

v2.0.7 c1-8 live acceptance smoke (2026-07-10) CONFIRMED the sprint-auditor's flagged risk: Services/DispatchPreflight.DefaultHookTrust false-BLOCKS a correctly-trusted codex hook, so NO codex dispatch can proceed against a real codex config - the release's headline feature (codex under the guard) is non-functional as shipped. Root cause: FindHookStateEntry assumes [hooks.state] holds INLINE entries ('path' = { ... }) on lines directly under a bare [hooks.state] header. Real codex writes DOTTED SUB-TABLE headers: [hooks.state.'C:\...\hooks.json:pre_tool_use:0:0'] with trusted_hash/enabled as child lines. The parser's inSection check (line is '[hooks.state]') goes false at the first sub-table header (any line starting with '['), so it never finds the entry -> returns null -> untrusted -> BLOCK. Two sibling defects: ExtractTomlString looks up key 'sha256' but the real key is 'trusted_hash' with value 'sha256:<hex>'; and HashFile returns bare UPPERCASE hex while codex stores 'sha256:'-prefixed LOWERCASE, so even a found entry would mis-compare. Fails SAFE (blocks, never runs unguarded). Fix: parse the dotted-subtable schema (match the [hooks.state.'<path>:<event>:0:0'] header by resolved path, read child trusted_hash/enabled lines until the next header); strip the 'sha256:' prefix and compare case-insensitively to lowercase SHA256 of the file bytes; enabled defaults true unless enabled=false. Interacts with 0269 (regen invalidates the pinned hash - genuine re-trust still needed once parsing works). Found live by balazs+Adele in c1-8; this is THE c1-8 hard finding. Routed to Grace (C1 sprint, on standby).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in `Services/DispatchPreflight.cs` (c1-8 slice). The trust resolver now parses codex's
REAL ledger schema — per-event dotted sub-tables `[hooks.state.'<abs-path>:<event>:0:0']` with
child `trusted_hash`/`enabled` lines — instead of the guessed inline `[hooks.state]` table:

- **Schema**: `FindPreToolUseTrustEntry`/`IsPreToolUseHeader` match the sub-table header whose
  quoted key begins with the resolved `hooks.json` abs-path AND names the `pre_tool_use` event
  specifically (a sibling `stop` entry being enabled no longer satisfies the guard requirement —
  the exact c1-8 smoke state), then read child lines until the next `[` header.
- **Key**: the trust hash is read from `trusted_hash` (value form `sha256:<hex>`), not `sha256`.
- **Hash form**: the stored `sha256:` prefix is stripped and both sides normalized to lowercase
  hex, so codex's lowercase-prefixed value compares equal to the local SHA256 of the file bytes.
- **enabled** defaults true unless an explicit `enabled = false` is present.

The block message now distinguishes (a) missing/disabled/no-config → "re-approve the pre_tool_use
guard hook in codex" from (b) present-but-stale-hash → "the hooks.json hash changed (a dydo upgrade
regenerated it); re-approve the new hash in codex" — the `HookTrust` tri-state carries the
distinction from the resolver to `CheckHookTrust`.

Tests: `DynaDocs.Tests/Services/DispatchPreflightTests.cs` hook-trust cases rebuilt from a real
codex sub-table fixture — trusted+enabled+matching → PASS; matching entry + stale hash → (b) BLOCK;
`enabled = false` → (a) BLOCK; no entry → (a) BLOCK; enabled-defaults-true (no `enabled` line) →
PASS; a sibling `stop` entry enabled does NOT satisfy pre_tool_use → (a) BLOCK. Gates green
(`run_tests.py` 4744 pass; `gap_check.py --force-run` tier 100%).

**0269 residual**: once parsing works, balazs's real config still BLOCKS via path (b) because the
regenerated `.codex/hooks.json` invalidated the pinned hash — a genuine re-trust (re-approve the new
hash in codex) is still needed and is tracked separately by issue 0269.

The seam signatures `HookTrustResolverOverride` (now `Func<string, HookTrust>`) / `CodexHomeOverride`
are retained. Two out-of-scope integration test files (`CodexDispatchPostureE2ETests.cs`,
`DispatchCommandTests.cs`) had their one-line seam-set updated to the enum as a mechanical
consequence of the richer return type (the build/gate cannot run otherwise).