---
area: reference
type: reference
---

# Audit System (removed in 2.0)

dydo's own audit trail — per-session JSON logs, baseline+delta compaction, and the HTML replay visualization — was **removed in 2.0** ([Decision 024](../project/decisions/024-dydo-2-native-pivot.md)). Claude Code's native session transcripts are the record now, so dydo no longer maintains a parallel one.

---

## What Changed

| Was (pre-2.0) | Now (2.0) |
|---|---|
| `dydo audit` command (list, session view, HTML replay) | Removed — read Claude Code's transcripts |
| Per-session JSON in `dydo/_system/audit/YYYY/` + `.events` | Legacy; not written by the 2.0 guard |
| `dydo audit compact` (baseline+delta compression) | Removed |
| Audit-derived "inquisition coverage" | Replaced by the artifact-derived attention ledger ([Decision 032](../project/decisions/032-attention-ledger-and-housekeeping-nudge.md)) |
| Audit-derived Files-Changed at `dydo task approve` | Re-planned as git-derived (`git diff --name-only` vs the task base ref) |

---

## Why

Native subagents and workflows share the parent session's transcript, so Claude Code already captures every tool call, block, and result. Maintaining a second, parallel audit store was duplicated effort with no unique signal once the runtime went native ([Decision 024](../project/decisions/024-dydo-2-native-pivot.md)). The one thing dydo still needs — crash-resume bookkeeping — lives in the watchdog log, not an audit trail.

---

## Related

- [Architecture Overview](../understand/architecture.md) — where audit fit, and the watchdog that remains
- [Decision 024](../project/decisions/024-dydo-2-native-pivot.md) — the native pivot that removed the audit trail
- [CLI Commands Reference](./dydo-commands.md) — the current command surface
