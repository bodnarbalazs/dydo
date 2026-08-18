---
title: Newline Ownership and Hermetic Projection Fixtures
sprint: notion-crlf-fidelity-hotfix
seq: 1
status: done
area: backend
type: context
---

# Slice 1 — Newline Ownership and Hermetic Projection Fixtures

This completed slice established source-span newline ownership for projected body application and proved it in hermetic LF and CRLF fixtures, clean-checkout release coverage, and the authorized live scratch-child proof.

## Prior art and byte rule

DR 043 gives `MarkdownPatchPlanner` ownership of mapped body spans and `SyncDocFile` ownership of retaining the surrounding file. Outside a mapped replacement the canonical file is byte-identical; local separators/terminators stay local; external owns only replacement/insertion bytes; deletion cannot consume/synthesize neighboring terminators. `CleanForPersist` remains external LF projection. No global `SyncDocFile` newline remap is permitted.

## Implementation boundary

Own `Sync/Projection/MarkdownPatchPlanner.cs`, `Sync/SyncDocFile.cs`, the two Slice-11 dual-projection full/delta tests and fixtures, focused projection tests, `.github/workflows/release.yml`, and `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`. Do not alter unrelated transport, model, or Anthropic-plan work.

## Proof

- Reproduce in genuine `core.autocrlf=false` and `true` clean checkouts after asserting fixture bytes.
- Add real merge-to-patch LF/CRLF update, middle/start/end insertion, middle/terminal deletion, and terminal-newline cases with concrete prefix/suffix expectations.
- Prove full and delta one import, no remote write, next-tick convergence, and local-push lossy-echo quiet for both styles.
- Make setup frontmatter detection newline-agnostic without fixture pinning.
- Add mandatory Ubuntu/LF and Windows/CRLF release jobs as publish-path build dependencies.
- Run focused projection/full/delta/release gates, build, `dydo check`, and one authorized scratch-child CRLF live external-edit proof.

## Ordering, watch-outs, rollback

First make the clean-checkout test red, then add span-level tests, then implementation, then release matrix/live proof. Never repair this by global newline replacement; preserve the unresolved body in shadow if ownership is ambiguous. Rollback removes only this hotfix's changes.

## Evidence

The focused projection/full/delta/workflow gate, build, documentation check, and final coverage review passed. The 2026-08-18 live suite executed all three DR 043 body-fidelity facts against isolated scratch children: 3 passed, 0 skipped; cleanup archived the children without logging credentials or page identifiers.
