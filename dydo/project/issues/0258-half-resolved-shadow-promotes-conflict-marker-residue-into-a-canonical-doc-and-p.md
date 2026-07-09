---
title: Half-resolved shadow promotes conflict-marker residue into a canonical doc and pushes it to Notion - resolved-shadow gate requires BOTH sentinels
id: 258
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Half-resolved shadow promotes conflict-marker residue into a canonical doc and pushes it to Notion - resolved-shadow gate requires BOTH sentinels

ContainsConflictMarkers ANDs the two sentinel labels, so a shadow with one label hand-deleted counts as resolved: promotion writes the ======= / >>>>>>> external residue into the canonical repo doc and the next reconcile pushes it to Notion repo-wins, violating the 0235 invariant; ~33 shadows currently pending.

## Description

`ThreeWayTextMerge.ContainsConflictMarkers` (`Sync/ThreeWayTextMerge.cs:27-29`) returns true only when the text contains BOTH `<<<<<<< repo` AND `>>>>>>> external`. `PromoteResolvedShadows` (`Sync/Notion/DocsTreeSync.cs:139-165`) treats any shadow failing this check as human-resolved.

**Failure:** a half-resolved shadow — human deleted only the opening `<<<<<<< repo` label, or resolved one hunk of several such that one label is gone file-wide — passes the gate and is promoted onto the canonical repo file. `CleanForPersist` (`DocsMarkdownNormalizer.cs:85-89`) strips only child-page tags and signing params, so the `=======` / `>>>>>>> external` residue lands in the canonical doc verbatim. The base is then aligned to the current external body (`DocsTreeSync.cs:167-186`), so the next reconcile pushes the residue to the Notion page as a repo-wins edit.

This violates the 0235 invariant the shadow flow exists to enforce ("the sync can never corrupt a canonical doc with conflict markers", `SyncRunner.cs:148`) — and the 0235 safety-rail backstop (`SyncRunner.cs:154`) uses the SAME AND predicate, so the residue bypasses it too. The weak predicate is even test-pinned: `DocsShadowConflictTests.cs:103` asserts False for a single-sentinel body.

**Fix direction:** the completion gate should require the ABSENCE of every sentinel (OR-presence check), not both.

**Urgency context:** ~33 shadow files currently sit awaiting human resolution in `dydo/_system/notion_sync/` — partial hand-resolution is an imminent, realistic failure mode.

Found by the v2.0.6 campaign inquisition; adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)