---
title: PM-spine body sync shares the same lossy-converter phantom-conflict risk (latent, low-exposure)
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: open
work-type: 
id: 236
type: issue
found-by: review
date: 2026-07-08
---

# PM-spine body sync shares the same lossy-converter phantom-conflict risk (latent)

Surfaced while root-causing the docs-mirror corruption
([[0235-docs-mirror-bidirectional-body-sync-corrupts-repo-with-phantom-conflicts-from-lossy-converter]]).
The PM-spine sync (`NotionSpineSync` → `NotionSyncAdapter` → `SyncRunner`/`ReconcileEngine`) reconciles
each DB row's **page body** through the **same** lossy `NotionBlockConverter` round-trip and the **same**
raw-text 3-way merge that manufactured phantom conflicts in the docs mirror. So the spine carries the
**same latent bug**: a lossy Notion round-trip of a row body can be misread as an external edit and,
in the two-sided case, produce a conflict written back into the canonical repo file.

**Why it hasn't bitten yet (low exposure):** spine row bodies are typically short and structurally
simple (a brief, a few lines), so the round-trip drift is small or nil and the phantom conflict rarely
triggers — unlike the docs mirror's large, formatting-rich prose (guides, changelog) that drifts every
time. It's a latency-of-detection difference, not a difference in kind.

**Relation to 0235.** 0235's fix is scoped **adapter-specific** by default (normalize inside the docs
adapter) to avoid perturbing the live spine mid-flight. This issue exists so the spine's shared exposure
isn't lost when 0235 closes: whether the normalization fix should be lifted to the **engine level**
(`ReconcileEngine` compares/stores base in normalized space for all adapters) — which would protect the
spine too — is a **scheduled decision with Brian at the table** (he owns the spine), not mid-sprint
scope creep. The 0235 **part-A safety rail** (never write conflict markers into a canonical file), if
implemented at the engine chokepoint as a pure refuse-on-markers backstop, would already protect the
spine as a no-op-in-normal-operation guard.

## Reproduction (hypothetical, not yet observed)
1. A spine row whose body contains constructs the converter round-trips non-idempotently (a table, a
   nested list), edited so repo and the drifted Notion read differ from base on the same lines.
2. `dydo notion sync` → the raw 3-way merge writes conflict markers into the canonical row file.

## Resolution
(Filled when resolved — likely folded into the 0235 normalization decision if it goes engine-level.)
