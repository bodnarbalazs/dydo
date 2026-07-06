---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Adele]
---

# 032 — Attention Ledger and Housekeeping Nudge: Artifact-Derived Coverage, Folder-Anchored Areas, Human-Gated Escalation

Inquisition coverage is replaced by an **attention ledger**: a computed view — not maintained
state — of when each area of the project was last looked at and how much it has changed since. A
visit is a **document**, not an observed runtime event: every attention-shaped workflow (patrol,
directed inquisition, sprint audit) ends with a required report artifact carrying the areas it
covered in frontmatter. Areas are anchored to the **folder structure** — in a feature-grouped
codebase the directory tree is the taxonomy — with a small reviewed **registry recording only the
deviations** from "folder = area" (splits of oversized areas, merges, cross-cutting concerns).
A deterministic **nudge** (staleness combined with spare agent capacity) is embedded in the
chief-of-staff methodology and suggests — never dispatches — housekeeping patrols or directed
inquisitions. A dedicated **housekeeper role is explicitly deferred** until the nudge loop proves
the demand exists.

## Context

The old inquisition coverage table degenerated from a map of the system into a log of one-off
directed inquisitions: its rows are incident names (`identity-hijack-slice-a-verification`,
`wait-rearm-flood-deadlock`), not places, so its staleness signal is meaningless — and as of this
decision every row is stale, the newest from 2026-05-23. Undirected inquisitions died by neglect,
not by decision; the dydo 2.0 inquisition evolved into a directed, campaign-end QA gate, leaving
"go find what needs attention" with no owner and no map.

The audit log cannot be that map: it tried to observe runtime behavior, and dydo 2.0 no longer
owns the runtime — subagents are invisible to it, orchestration is Claude-native. It is
deprecated. Doc drift, meanwhile, is a constant background cost with no standing mechanism.

## Decision

1. **A visit is a document.** Patrols, directed inquisitions, and sprint audits each end with a
   report artifact (frontmatter: covered areas + date) in a known location. Skills enforce this as
   the final checklist item. This needs cooperation at exactly one choke point per workflow — the
   end — not continuous observation, which is what killed the audit log.
2. **Folders are the taxonomy; the registry records deviations.** Area names derive from the
   feature-grouped directory structure (`Sync/Notion`, `Templates`, `Utils`, ...). One small
   registry file — closed vocabulary, reviewed like any doc — records only exceptions: oversized
   areas split into sub-areas (the guard alone spans identity, hooks, policy, onboarding, nudges),
   folders merged into one feature, cross-cutting concerns mapped to globs across folders.
   Overlap is allowed. `dydo check` rejects area names not in the vocabulary.
3. **Area-level resolution, files only in the plumbing.** Coverage claims, reports, and the ledger
   all speak folder/area language. File paths appear only where they are mechanically derived
   (git diff → resolver → areas); nobody hand-writes file inventories.
4. **The ledger is computed.** A `dydo attention`-style view scans visit artifacts for
   last-visit-per-area and computes churn per area from git history via the registry's path
   mappings. Attention score: staleness, churn since last visit, and historical finding density.
   No maintained state — if housekeeping never runs, the ledger truthfully shows everything stale
   instead of rotting into lies.
5. **The unmapped bucket is first-class.** Paths with churn that no area matches are surfaced as
   `unmapped`, with their churn. High-churn unmapped paths are themselves a top-priority nudge
   ("someone built a subsystem nobody registered"). Initial seed: generate the registry from the
   directory tree plus existing doc `area:` frontmatter, human-reviewed once; the unmapped bucket
   keeps it honest afterward.
6. **The nudge suggests; the human dispatches.** Deterministic check: top attention score above
   threshold AND active agents below threshold AND last housekeeping older than N days → suggest
   a patrol or a directed inquisition at the top-scoring areas. First implementation is a standing
   item in the chief-of-staff methodology (runs the check whenever reporting status or triaging
   the funnel); a scheduled heartbeat comes later only if the habit sticks. Neither the nudge nor
   any patrol autonomously triggers an inquisition — inquisition-scale spend is raised to the
   human (by the CoS, or directly).
7. **Housekeeper deferred.** No dedicated housekeeper role yet. The ledger + nudge are useful
   without one (a nudge can equally suggest a directed inquisition). If nudges keep firing and
   acting on them keeps paying off, that is the evidence the role deserves to exist — as the
   standing owner of patrol work: doc-drift sweeps and trivial fixes itself, smells escalated as
   issues or inquisition requests into the funnel, plus a periodic state-of-the-estate digest.
   Doc-drift (cheap, verifiable by reading) and bug hunting (expensive, open-ended — inquisition
   territory) stay separated: the patrol detects, the inquisition hunts.

## Consequences

- Graceful decay is accepted by design: read-only coverage claims are self-reported at area level;
  a padded claim means an area is revisited late — degraded, not broken. Making such claims
  provable is explicitly a non-goal (the audit log died of that attempt).
- The `dydo inquisition coverage` table is superseded; existing rows are historical visit records
  at best.
- Concrete build order: registry seed → visit-artifact convention in the patrol/inquisition/
  sprint-audit skills → computed ledger view → CoS nudge item. Each step is independently useful.
- Relates to [Decision 026](./026-tier1-managers-doctrine.md) (CoS as the funnel/triage point) and
  [Decision 031](./031-sprint-auditor-charter-rewrite.md) (the tier ladder this slots above).
