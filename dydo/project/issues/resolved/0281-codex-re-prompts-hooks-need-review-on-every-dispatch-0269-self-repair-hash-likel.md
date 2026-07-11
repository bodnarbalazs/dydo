---
title: Codex re-prompts 'Hooks need review' on every dispatch - 0269 self-repair hash likely conflicts with codex trust hash (swarm-blocker)
id: 281
area: backend
type: issue
severity: high
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
resolved-date: 2026-07-11
---

# Codex re-prompts 'Hooks need review' on every dispatch - 0269 self-repair hash likely conflicts with codex trust hash (swarm-blocker)

Observed 2026-07-11 (2x): every dispatched codex session shows codex's interactive 'Hooks need review - 1 hook is new or changed' prompt requiring a manual 'Trust all and continue' click before running. Auto-approve works AFTER the click, but the per-session click does not scale to a codex swarm (human clicks Trust on every agent). HYPOTHESIS: dydo's 0269 self-repair writes the pre_tool_use [hooks.state] entry using DYDO's SHA256 of .codex/hooks.json; codex computes the hash its own way (line-ending/encoding difference), sees a mismatch -> 'changed' -> prompts; human clicks Trust -> codex writes ITS hash -> next dispatch's 0269 recomputes dydo's hash, sees mismatch, OVERWRITES -> prompts again (perpetual tug-of-war). The self-repair meant to REMOVE manual trust may be CAUSING the recurring prompt. FIX: (1) dydo computes the hook hash EXACTLY as codex does so the written entry satisfies codex; OR (2) self-repair writes-once-if-missing / reconciles to codex's hash not dydo's; OR (3) a codex config that pre-trusts hooks non-interactively. Trust cluster with 0269/0270/0273. Must fix before the multi-codex swarm. 2.0.9 wave. Found by balazs+Adele.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-11: fix landed 3524d57e. DispatchPreflight now treats an enabled, well-formed codex pre_tool_use trusted_hash as codex-authoritative and PRESERVES it (does not overwrite); missing/malformed/disabled still self-repair; unwritable/apostrophe still BLOCK. Stops the 0269/0281 trust-thrash: dydo's raw-file SHA256 differs from codex's own hash, and the old overwrite-on-mismatch made codex restore its hash after each human Trust and re-prompt every dispatch. Implemented by CODEX (Quinn), cross-vendor Claude-reviewed (security-verified: fail-closed intact, no 0270 regression, 4752/4752 + gap_check green; the review's comments-only findings - stale trust-model narrative in this thrice-burned path - applied by CoS per exact reviewer prescription, no logic change). Live-verify on next dispatch after install: after one clean codex Trust, subsequent dispatches should NOT re-prompt 'Hooks need review'.