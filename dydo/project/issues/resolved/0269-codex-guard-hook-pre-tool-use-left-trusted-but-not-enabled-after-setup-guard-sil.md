---
title: Codex guard hook (pre_tool_use) left trusted-but-not-enabled after setup - guard silently would not fire; only the stop hook gets enabled
id: 269
area: backend
type: issue
severity: high
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-10
---

# Codex guard hook (pre_tool_use) left trusted-but-not-enabled after setup - guard silently would not fire; only the stop hook gets enabled

v2.0.7 acceptance smoke (2026-07-10): the c1-4 dispatch preflight correctly BLOCKED a codex dispatch as unguarded. Ground-truth cause in ~/.codex/config.toml [hooks.state]: the pre_tool_use hook (THE dydo guard) had a trusted_hash recorded but NO 'enabled = true', while the stop hook of the same hooks.json had both trusted_hash and enabled=true. So codex would silently skip the guard hook while running the stop hook - the exact 'guard never fires' risk, manifesting via the per-hook enabled flag rather than the hash. c1-4's preflight caught it (feature works, fails safe), but the SETUP left the guard in that state. Root cause candidates: (a) codex's interactive hook-approval UX enables hooks per-event and the pre_tool_use approval did not write enabled=true; (b) dydo init/sync's .codex/hooks.json generation or a hash change from a regen left the guard entry stale. Fix directions: dydo init/sync should write/repair the [hooks.state] trust entry for the pre_tool_use guard hook directly (the Codex guard adapter follow-up from DR-037), OR the codex-host setup docs must call out enabling the pre_tool_use hook explicitly. Adjacent to 0254's codex-guard-adapter scope. Found live by balazs+Adele during c1-8.

## Description

**Root cause CONFIRMED (2026-07-10, deeper than the title):** the `.codex/hooks.json` file was
**regenerated** (v2.0.7 install/sync) *after* the human trusted it, so the recorded `trusted_hash`
is stale. Live hash of `.codex/hooks.json` = `55af5ed514e3d93e787309182c9a1837c3023a616508b8f441ae5cc0dd997382`;
recorded `trusted_hash` in `[hooks.state]` = `sha256:581b21e8c4248575822b243e3470d04a1fab9dbdd242d0da869a240782db84d7`
— **mismatch**. Codex SHA256-pins hook trust (Noah's probe finding), so a regenerated hooks.json
silently invalidates the existing trust and the guard stops firing. The missing `enabled = true`
on pre_tool_use was a *second*, independent defect on top of the stale hash.

**The upgrade-path severity:** any existing codex user who upgrades dydo has their `.codex/hooks.json`
regenerated, which silently un-trusts their guard hook — on a release whose headline is
"codex under the guard." Without the c1-4 preflight (which caught it and fails safe), they would
dispatch codex sessions the guard never watches, with no signal. The manual fix is re-running
`codex` in the repo to re-approve the new hash.

**Fix directions (sharpened):**
1. `dydo init`/`sync` (or a dedicated `dydo codex trust` step) should write/repair the
   `[hooks.state]` entry for BOTH the current hash AND `enabled = true` directly, so a dydo upgrade
   re-establishes its own guard trust rather than depending on a manual codex re-approval. This is
   the DR-037 Codex-guard-adapter follow-up, now with a proven upgrade-invalidation case.
2. Until (1) lands: codex-host setup/upgrade docs must state that any dydo upgrade requires
   re-approving the `.codex/hooks.json` hook in codex.
3. Consider making `.codex/hooks.json` generation deterministic/stable so a no-op sync does not
   change its hash and needlessly invalidate trust.

Adjacent to 0254's codex-guard-adapter scope; route to sprint C1 follow-on or the guard-adapter work.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in `Services/DispatchPreflight.cs` (c1 slice `c1-fix-0269-hooktrust-selfrepair`) as a
DISPATCH-PATH SELF-REPAIR, building on the 0270 schema parser. When the preflight's hook-trust check
finds the `pre_tool_use` guard entry MISSING, STALE-HASH, or not-enabled, dydo now repairs the
`[hooks.state]` entry itself instead of blocking on a manual codex re-approval:

- `CheckHookTrust` resolves trust; if not `Trusted`, it calls the repair seam, then re-evaluates
  against the 0270 schema and PROCEEDS. It only BLOCKS if the repair itself cannot be written.
- `DefaultHookTrustRepair` writes the dotted sub-table
  `[hooks.state.'<resolved-abs-hooks.json>:pre_tool_use:0:0']` with
  `trusted_hash = 'sha256:<lowercase-hex-of-current-hooks.json-bytes>'` and `enabled = true` — the
  EXACT schema 0270 reads. `UpsertPreToolUseEntry` drops only the existing `pre_tool_use` sub-table
  for that path and appends a fresh one, preserving every other entry/sub-table (the sibling `stop`
  hook and any foreign-repo state survive verbatim).
- The write sits behind an injectable seam `HookTrustRepairOverride` (mirroring
  `HookTrustResolverOverride`/`CodexHomeOverride`), so tests never touch a real `~/.codex`.

**Premise not resolved here (c1-8 live check):** whether codex HONORS an externally-written trust
entry (vs re-validating/overwriting it on next run) is verified LIVE in c1-8 — this slice only WRITES
a correct entry; it does not assume codex accepts it. If c1-8 finds codex re-validates, the 0269
direction-2 doc fallback applies: codex-host upgrade docs must state that a dydo upgrade requires
re-approving the `.codex/hooks.json` hook in codex (the self-repair still leaves the config in the
correct end-state either way).

Tests (`DynaDocs.Tests/Services/DispatchPreflightTests.cs`, real-schema TOML fixture): stale-hash
entry → repaired to live hash + `enabled = true` → PASS; missing entry (no config file / foreign
entry only) → written → PASS; `enabled = false` → set true → PASS; a sibling `stop` sub-table and an
unrelated foreign-repo `[hooks.state.*]` entry PRESERVED across the repair; unwritable config
(config.toml path is a directory) → BLOCK with the actionable manual-re-approval message. Seam-level:
untrusted + repair fails → BLOCK; untrusted + repair succeeds + re-eval trusted → PASS; a trusted
entry never triggers a repair. Gates green (`run_tests.py` 4745 pass; `gap_check.py --force-run` tier
100%).

Scope was fenced to `Services/DispatchPreflight.cs`, `DispatchPreflightTests.cs`, and this issue
file — nothing in `SyncCommand.cs` (0271 owns the deterministic-generation angle).

**PREMISE PROVEN — live c1-8 acceptance (2026-07-11, Sam @ codex/Gpt 5.5, HEAD build):** the first
`dydo dispatch --codex` triggered the self-repair (no manual codex re-approval), and inside the
launched codex session the dydo guard hook **FIRED and BLOCKED an off-limits read**
(`dydo read dydo.json` → `BLOCKED: off-limits`, no leak). Codex therefore HONORS dydo's
externally-written `[hooks.state]` trust entry — the direction-2 doc fallback is NOT needed.
Self-repair closes the upgrade-invalidation footgun end-to-end with zero human steps. Follow-on
hardening (apostrophe-path repair refusal + per-cause diagnostics) landed in
`c1-fix-0269-audit-followups`. Later hardening (0269 §3 idea): make `.codex/hooks.json` generation
deterministic so a no-op sync does not needlessly change the hash — tracked separately.