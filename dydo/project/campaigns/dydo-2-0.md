---
title: dydo 2.0 Native Pivot
goal: Replace the heavyweight dispatch/queue/worktree orchestration with a slim native-AOT worker tier and a bidirectional Notion PM surface, keeping repo files the single source of truth.
priority: Urgent
release: 
status: active
area: project
type: context
---

# dydo 2.0 Native Pivot
This campaign drives the native pivot defined by Decision 024 and Decision 025. The human's role shifts from operator to PM of agents: campaigns, sprints, and tasks are managed as canonical repo files and projected into a Notion PM board that dydo provisions and owns. Notion is a swappable view — delete the adapter and the repo is still whole.

## Wayfinding — bidirectional Notion body fidelity

**Destination:** Complete the Campaign's canonical-repo/swappable-view promise for authored PM
bodies: edits from either the repo or Notion converge without formatting loss, duplicated regions,
or board-derived collateral rewrites, and deleting the Notion adapter still leaves the repo whole.

**Settled outcomes**

- **W1 — Full-fidelity posture (settled 2026-08-17).** Issue
  [0309](../issues/0309-notion-watchdog-round-trip-corrupts-existing-pm-record-bodies.md) and the
  ns-8 audit prove that the prior fixed-point/corpus acceptance boundary was too narrow. The human
  explicitly rejected temporary quarantine, one-way bodies, and quick endpoint swaps. The route
  remains uniformly bidirectional under DR 025 and must address DR 035's recorded native-Markdown
  loss rather than declaring it harmless.
- **W2 — External-projection contract (settled 2026-08-17).**
  [DR 043](../decisions/043-dual-projection-format-preserving-notion-body-sync.md) stores distinct
  local-authored and last-observed Notion projections, detects changes within their own spaces,
  translates genuine edits through format-preserving syntax-tree alignment, journals body writes,
  and treats ambiguity as a real shadow conflict. Normalized text is never canonical content.

**Waypoints**

- **W3 — Fidelity delivery Sprint (Frontier).** Point to one reviewed Sprint implementing the
  settled contract across snapshot state, reconciliation, the spine adapter, migration, diagnostics,
  and offline/live regression coverage.
- **W4 — Production proof and issue closure (blocked by W3).** Accept only after a real-Notion
  existing-record push→echo→watchdog tick is byte-identical and plans `None`, a genuine Notion body
  edit imports exactly once without collateral formatting/frontmatter changes, the Notion-originated
  create control remains pristine, and the full safety/coverage gates pass.

**Fog**

- The original slice-11 before/base/Notion-echo artifacts and incident watchdog binary fingerprint
  are not present in this checkout, so the exact historical production branch (`WriteToRepo` versus a
  clean `Merged` result, possibly under a stale process) may remain unknowable and is not a delivery
  dependency; the sanitized record plus the exact full/delta route is the reproducible regression.
- DR 043's source-spanned mapping still needs implementation proof against seeded ambiguous/repeated
  structures and real Notion echoes; ambiguity must shadow rather than reopen the settled contract.
- Existing single-body snapshots, already-provisioned board pages, and interrupted first upgraded runs
  need migration/recovery proof that cannot manufacture an edit, duplicate a row, or choose a winner.

**Out of scope:** temporary/quarantine behavior, one-way authored bodies, treating normalization as
canonical content, broad docs-mirror redesign unrelated to the PM spine, and Notion presentation
metadata that DR 025 already classifies as disposable.
