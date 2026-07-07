---
id: 216
area: general
type: issue
severity: high
status: resolved
found-by: manual
date: 2026-07-06
resolved-date: 2026-07-07
---

# gap_check coverage gate red: NotionProvisioner + NotionSpineSync at CRAP 32 (>30) block ALL merges via the whole-repo gate

Two pre-existing over-complex methods fail the hard whole-repo `gap_check` gate, blocking every merge (Slice-1 sync-model + the readme-2.0 fix) regardless of scope.

## Description

`gap_check.py` is a hard whole-repo gate (reviewer/code-writer/test-writer skills: a red gap_check = automatic review FAIL, "no such thing as a pre-existing/unrelated failure"). It exits 1 on two Tier-1 methods (threshold CRAP ≤ 30):

- `Sync/Notion/Provisioning/NotionProvisioner.cs` → `BuildConfig`: CC 32, cov 96.0%, CRAP 32.1
- `Sync/Notion/NotionSpineSync.cs` → `Provision`: CC 32, cov 100.0%, CRAP 32.0

**This is COMPLEXITY debt, not a coverage gap.** CRAP = CC² × (1−cov)³ + **CC**; the trailing `+ CC` is a floor, so at CC=32 CRAP ≥ 32 no matter the coverage — `Provision` is already at 100% coverage and still 32.0. **Adding tests cannot fix it.** Tier 1 is already the loosest threshold, and `gap_check.py` has **no waiver/baseline/exception mechanism**. The only code lever is reducing cyclomatic complexity < 30 (extract-method refactor).

Pre-existing: CC was 32 on both before Slice 1 (landed with the Notion-sync feature, commits 86e0158 / 7433b8b); the Slice-1 diff touches neither `.cs` file.

Surfaces a policy contradiction: the absolute whole-repo gate forces the blocked agent to fix unrelated modules, but coding-standards say "work your slice only; flag problems elsewhere, don't fix them." A scope-disjoint slice is currently unmergeable until these two methods are refactored.

## Reproduction

`python DynaDocs.Tests/coverage/gap_check.py --inspect` (or `--methods`) on current HEAD.

## Resolution

(Filled when resolved — expected path: scoped extract-method refactor of `BuildConfig` + `Provision` to CC ≤ ~28, its own review since it's live-API-sensitive. Awaiting balazs's debt-gating decision: refactor-first vs. relax/waiver the gate.)