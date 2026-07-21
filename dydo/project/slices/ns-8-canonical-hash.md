---
title: ns-8 Canonical-Render Hashing
blocked-by: ns-7-converter-hardening
due:
needs-human: false
priority: High
sprint: notion-stabilization
status: done
work-type: bug
area: backend
type: context
---

# ns-8 Canonical-Render Hashing

Kills the phantom-conflict class for spine bodies (issue 0236; root-caused for docs in 0235). Spine body drift is currently detected by comparing the lossy `NotionBlockConverter` round-trip against the local body — dialect differences (escapes, whitespace, list markers) manufacture diffs where no one edited anything, and with ns-4 those become shadow conflicts; without it, marker writes. The survey's stability recipe: compare hashes of the **normalized canonical rendering**, using the base snapshot's stored canonical form as the reference — a body only counts as remotely changed when its canonical re-render differs from the canonical form at last sync.

## Task

1. Define one canonical normalization for spine bodies (reuse/extract from `DocsMarkdownNormalizer` where the rules match: escapes, blank-line collapse, list markers, trailing whitespace) and apply it symmetrically to both the local body and the Notion-side re-render before comparison.
2. In `NotionSyncAdapter.ReadExternalState` (~:90-103) and the snapshot write path: store the canonical form (or its hash) of the body as-synced; on subsequent reads, compute remote-changed = canonical(remote re-render) != canonical(snapshot body); local-changed analogously. Unchanged-on-both → no-op regardless of raw-text differences.
3. When pushing local → Notion succeeds, snapshot the canonical form of what was pushed (not the pre-normalization raw), so the next read compares like-for-like.
4. Keep raw local files untouched — normalization is comparison-only, never written back to canonical records.

## Files

- `Sync/Notion/NotionSyncAdapter.cs`, `Sync/Notion/DocsMarkdownNormalizer.cs` (extract shared rules if clean)
- `Sync/BaseSnapshotStore.cs` only if the snapshot shape needs the canonical field
- Tests: `DynaDocs.Tests/Sync/Notion/NotionSyncAdapterTests.cs`

## Success criteria

- New tests: a body whose round-trip differs only in dialect (escape/whitespace/list-marker changes) produces NO drift and NO conflict across two sync passes; a real remote edit still detects; a real dual edit still conflicts (into ns-4's shadow).
- Regression: the 0235-style phantom sequence (sync → no edits → sync) is a no-op for every spine type.
- Full ratchet green; issue 0236 updated (leave open pending ns-10 live pass).
