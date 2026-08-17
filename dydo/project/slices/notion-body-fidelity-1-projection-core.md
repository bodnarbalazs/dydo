---
title: Projection Core
sprint: notion-body-fidelity
seq: 1
status: ready
area: backend
type: context
---

# Slice 1 — Projection Core

Build the bounded, source-preserving Markdown alignment engine that translates observable edits
between the local and external body representations without rewriting untouched bytes.

## Spec fragment

Build DR 043's representation-independent, source-preserving Markdown alignment and patch engine. It
accepts local/external bases plus current bodies and returns a byte-preserving merged local body or a
structured ambiguity/overlap reason. No file, snapshot, runner, or Notion behavior changes here.

Acceptance: unchanged local source and interstitial spans are copied exactly; unique external edits and
disjoint two-sided edits compose; overlapping/repeated ambiguous regions conflict; semantic mutations
remain visible; alignment is bounded and never whole-document quadratic.

## Implementation detail

Create one type per file under `Sync/Projection/`: `DualBodyBase`, `ProjectedBodyConflict`,
`ProjectedBodyResult`, `MarkdownSyntaxNode`, `MarkdownAlignment`, `SemanticTextMap`, and
`ProjectedMarkdownMerge`. Use one pipeline with pipe/grid tables. Obtain raw spans from Markdig and clamp
them with the verified `NotionBlockConverter.ConvertSiblings`/`ClampedSlice` pattern
(`NotionBlockConverter.cs:28-38,279-291`); gaps between clamped siblings are explicit immutable raw spans.

`MarkdownSyntaxNode` identity is `(kind, decoded literal, ordered child identities)`. Decode only syntax:
backslash escapes and equivalent list marker/indent spellings compare equal, while emphasis, link target,
checkbox state, code literal, table cell order, punctuation, and word changes remain distinct. Represent
a local leading H1 as local-only only when the caller supplies the same semantic page-title text; never
drop another heading. `SemanticTextMap` records each decoded character's raw source offset so a leaf text
edit changes only that raw range and preserves surrounding inline markers.

At each sibling level, anchor identities occurring exactly once on both sides. Partition between anchors
and run Myers diff only for partitions of at most 256 nodes; bodies are capped at 2 MiB/20,000 syntax
nodes. Track alignment multiplicity as `0`, `1`, or `many`; a changed hunk touching `many`, an exceeded
bound, or an unsupported Markdig node returns a named conflict. Do not allocate an `N×M` matrix.

Diff external-base→external-current and local-base→local-current, map operations through the unique base
alignment, then apply replacements from end to start to current local raw text. Leaf text changes use the
semantic-to-raw map. A Notion-authored structural insertion/formatting change uses its stable-cleaned raw
external source span with LF endings and exactly one boundary blank line; do not use
`Markdown.Normalize` or any renderer that flattens tables. Overlapping mapped ranges conflict.

Add exact test classes `ProjectedMarkdownAlignmentTests`, `ProjectedMarkdownPatchTests`, and
`ProjectedMarkdownMutationTests` under `DynaDocs.Tests/Sync/Projection/`. Include exact-byte assertions
for blank gaps, markers, escapes, emphasis/links, nested lists, tables, quotes, fenced code, H1 missing on
the external base, repeated headings, disjoint edits, same-node overlap, and a seeded mutation matrix.

## Out of scope for this slice

Persistence, adapter APIs, repo files, Notion transport, migration, and live tests.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~DynaDocs.Tests.Sync.Projection"
if (($listed | Select-String 'ProjectedMarkdown').Count -lt 12) { throw 'Projection gate matched fewer than 12 tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~DynaDocs.Tests.Sync.Projection"
dotnet build DynaDocs.csproj --no-restore
```
