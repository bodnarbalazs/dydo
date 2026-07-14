---
title: Swarm 0258
area: general
name: swarm-0258
status: in-progress
created: 2026-07-12T15:41:28.6875166Z
assigned: Charlie
needs-human: false
---

# Task: swarm-0258

CODEX swarm fix ROUND 2 — issue 0258. Your round-1 PREDICATE change is CORRECT and a Claude reviewer VERIFIED it: ContainsConflictMarkers now ORs the endpoint constants OursLabel (`<<<<<<< repo`) and TheirsLabel (`>>>>>>> external`), deliberately excluding MidLabel (`=======`) to avoid false-positives on Markdown setext underlines — the reviewer agreed this is the RIGHT tradeoff (either endpoint catches every single-endpoint-deleted half-resolved case; the rare both-endpoints-deleted case fails VISIBLE not wedged). KEEP the predicate logic EXACTLY as-is — do NOT change `ContainsConflictMarkers`'s boolean logic. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

THE PROBLEM (test coverage, not logic): the regression test does NOT walk the actual 0258 bug path. The 0258 bug is in `Sync/Notion/DocsTreeSync.cs` `PromoteResolvedShadows` (~line 142): a half-resolved shadow was treated as resolved and its `=======`/`>>>>>>> external` residue PROMOTED onto the canonical repo doc, then pushed to Notion. Your added test `PartiallyResolvedShadow_WithEitherEndpointMarker_IsNotPromoted` drives `SyncRunner.Run`, which NEVER calls `PromoteResolvedShadows` — so it exercises the SyncRunner safety-rail gates (154/163), not promotion. The literal regressed path (DocsTreeSync promoting endpoint-deleted residue onto canonical + pushing to Notion) has NO direct regression test. If someone later swaps the `DocsTreeSync.cs:142` predicate call for a local check, nothing end-to-end catches it.

FIX (build ON your round-1 diff — do NOT revert the predicate):
1. Add a DocsTreeSync-LEVEL regression test that runs the PROMOTION path directly. Find the existing happy-path promotion test (something like `ResolvedShadowFile_IsPromotedToCanonical...` in the shadow/DocsTree tests) and CLONE its harness. Make it a theory with BOTH endpoint-deleted variants:
   - Variant A: shadow with the opening `<<<<<<< repo` deleted (leaves `=======` + `>>>>>>> external`).
   - Variant B: shadow with the closing `>>>>>>> external` deleted (leaves `<<<<<<< repo` + `=======`).
   For each, run the promotion path (DocsTreeSync.Run / PromoteResolvedShadows as the happy-path test does) and ASSERT: (a) the canonical repo doc is left CLEAN — no `=======`/`<<<<<<< `/`>>>>>>> ` residue written into it; (b) the shadow file is left INTACT (NOT promoted/consumed — still awaiting human resolution); (c) NO Notion push / repo-wins edit happens for it. This is the test that would have been RED under the old AND predicate and is GREEN under your OR fix — exactly what a 0258 regression test must be.
   - Keep your existing SyncRunner-level test too (it validates gates 154/163 — still valuable); just fix its coverage claim or leave it.
2. Update the doc comment on `ContainsConflictMarkers` (`Sync/ThreeWayTextMerge.cs` ~lines 22-26) to state WHY MidLabel (`=======`) is deliberately excluded: a bare `=======` line is byte-identical to a Markdown setext H1 underline / legit content, so matching it would false-positive and permanently wedge fully-resolved shadows that contain a setext heading; either endpoint sentinel suffices to catch every single-endpoint-deleted half-resolved case; the both-endpoints-deleted stray-`=======` case intentionally promotes (fail-visible, human-fixable — beats fail-wedged). Per coding-standards: comments explain WHY.

VERIFY: `dotnet build DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore` passes; run `dotnet test` filtered to the new DocsTree/shadow promotion test + existing DocsShadowConflictTests. To prove the new test reproduces 0258: you may TEMPORARILY flip the predicate `||` back to `&&`, confirm the new test goes RED, then RESTORE the `||` (leave the fix in place). Do NOT run the python coverage gate (0282) — the reviewer re-runs it.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0258-r2` with: the new DocsTreeSync-level test (both variants, what it asserts), confirmation it was RED under `&&` and GREEN under `||`, the doc-comment update, build/test results, ~time. THEN release yourself.

CONSTRAINTS: touch ONLY `Sync/ThreeWayTextMerge.cs` (doc comment ONLY — predicate logic unchanged) and the shadow/DocsTree test file(s) under `DynaDocs.Tests/Sync/`. Do NOT touch other swarm agents' files (GuardCommand.cs, WorktreeCommand.cs, AgentRegistry.cs, Rules/, gap_check.py). Do NOT change the predicate's boolean logic or the sync data model.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)