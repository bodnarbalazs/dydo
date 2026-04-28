---
id: 130
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-28
---

# Stale-working reclaim silently archives in-flight work

## Description

**Mechanism.** When an agent dies (per findings #1/#2) it remains in `status: working` past the stale-working threshold with a dead session PID. The next claim hits `AgentRegistry.HandleExistingSession` (Services/AgentRegistry.cs:311-317), which allows the reclaim, then `SetupAgentWorkspace` (Services/AgentRegistry.cs:330-363) calls `ArchiveWorkspace` (Services/WorkspaceArchiver.cs:18-46) at line 335. `ArchiveWorkspace` MOVES every non-system file into `archive/{timestamp}/` and re-scaffolds the workspace. The reclaiming agent therefore loses any in-flight notes/drafts/findings the prior session left in their root.

`PruneArchive` (Services/WorkspaceArchiver.cs:52-) caps total archive files at 30 — old snapshots are eventually deleted. So forensic recovery is bounded.

**Impact.** This is a *symptom-masker*, not a root cause: it hides the death (because the stderr message says 'reclaimed agent ... from an interrupted session') and discards in-flight context. The co-thinker's brief in this very inquisition was recovered from `archive/20260428-144500/` exactly because of this path.

**Suggested fix (after #1/#2 land).** Surface dead-claim recovery more loudly — print the archive path to stderr at reclaim time (currently only says 'check git status'), and consider raising the prune threshold for archives that contain Markdown drafts. Lower priority until the deaths themselves stop.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — stale-working reclaim).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)